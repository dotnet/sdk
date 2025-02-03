// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal static class ImagePublisher
{
    public static async Task PublishImageAsync(
        BuiltImage singleArchImage,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                await PushToLocalRegistryAsync(
                    singleArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    telemetry,
                    cancellationToken,
                    destinationImageReference.LocalRegistry!.LoadAsync).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(
                    singleArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    cancellationToken,
                    destinationImageReference.RemoteRegistry!.PushAsync,
                    Strings.ContainerBuilder_ImageUploadedToRegistry).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();
    }

    public static async Task PublishImageAsync(
        BuiltImage[] multiArchImage,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                await PushToLocalRegistryAsync(
                    multiArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    telemetry,
                    cancellationToken,
                    destinationImageReference.LocalRegistry!.LoadAsync).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(
                    multiArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    cancellationToken,
                    async (images, source, destination, token) =>
                    {
                        (string imageIndex, string mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
                        await destinationImageReference.RemoteRegistry!.PushManifestListAsync(
                            destinationImageReference.Repository,
                            destinationImageReference.Tags,
                            imageIndex,
                            mediaType,
                            cancellationToken).ConfigureAwait(false);
                    },
                    Strings.ImageIndexUploadedToRegistry).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();
    }

    private static async Task PushToLocalRegistryAsync<T>(
        T image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        Telemetry telemetry,
        CancellationToken cancellationToken,
        Func<T, SourceImageReference, DestinationImageReference, CancellationToken, Task> loadFunc)
    {
        ILocalRegistry localRegistry = destinationImageReference.LocalRegistry!;
        if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            telemetry.LogMissingLocalBinary();
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
            return;
        }
        try
        {
            await loadFunc(image, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            if (BuildEngine != null) 
            {
                Log.LogMessage(MessageImportance.High, Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
        }
        catch (ArgumentException argEx)
        {
            Log.LogErrorFromException(argEx, showStackTrace: false);
        }
        catch (DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
        }
    }

    private static async Task PushToRemoteRegistryAsync<T>(
        T image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        CancellationToken cancellationToken,
        Func<T, SourceImageReference, DestinationImageReference, CancellationToken, Task> pushFunc,
        string successMessage)
    {
        try
        {
            await pushFunc(
                image,
                sourceImageReference,
                destinationImageReference,
                cancellationToken).ConfigureAwait(false);
            if (BuildEngine != null) 
            {
                Log.LogMessage(MessageImportance.High, successMessage, destinationImageReference, destinationImageReference.RemoteRegistry!.RegistryName);
            }
        }
        catch (UnableToAccessRepositoryException)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName);
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
                Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
            }
        }
    }
}
