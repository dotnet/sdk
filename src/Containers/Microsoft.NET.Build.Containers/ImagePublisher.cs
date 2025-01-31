// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal static class ImagePublisher
{
    public static async Task PublishImage(
        BuiltImage image,
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
                    image,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(
                    image,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();
    }

    public static async Task PublishImage(
        BuiltImage[] images,
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
                    images,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(
                    images,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    BuildEngine,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();
    }

    private static async Task PushToLocalRegistryAsync(
        BuiltImage image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        Telemetry telemetry,
        CancellationToken cancellationToken)
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
            await localRegistry.LoadAsync(image, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
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
    }

    private static async Task PushToLocalRegistryAsync(
        BuiltImage[] images,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        Telemetry telemetry,
        CancellationToken cancellationToken)
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
            await localRegistry.LoadAsync(images, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
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
    }

    private static async Task PushToRemoteRegistryAsync(
        BuiltImage image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        CancellationToken cancellationToken)
    {
        try
        {
            await destinationImageReference.RemoteRegistry!.PushAsync(
                image,
                sourceImageReference,
                destinationImageReference,
                cancellationToken).ConfigureAwait(false);
            if (BuildEngine != null) 
            {
                Log.LogMessage(MessageImportance.High, Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, destinationImageReference.RemoteRegistry!.RegistryName);
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

    private static async Task PushToRemoteRegistryAsync(
        BuiltImage[] images,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        IBuildEngine? BuildEngine,
        CancellationToken cancellationToken)
    {
        try
        {
            (string imageIndex, string mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
            await destinationImageReference.RemoteRegistry!.PushManifestListAsync(
                destinationImageReference.Repository,
                destinationImageReference.Tags,
                imageIndex,
                mediaType,
                cancellationToken).ConfigureAwait(false);
            if (BuildEngine != null) 
            {
                Log.LogMessage(MessageImportance.High, Strings.ImageIndexUploadedToRegistry, destinationImageReference, destinationImageReference.RemoteRegistry!.RegistryName);
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
