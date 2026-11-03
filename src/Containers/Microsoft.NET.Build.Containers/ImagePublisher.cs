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
        bool noCache,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool published;
        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                published = await PushToLocalRegistryAsync(
                    singleArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    telemetry,
                    cancellationToken,
                    destinationImageReference.LocalRegistry!.LoadAsync).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                published = await PushToRemoteRegistryAsync(
                    singleArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    cancellationToken,
                    (image, source, destination, token) => destinationImageReference.RemoteRegistry!.PushAsync(
                        image,
                        source,
                        destination,
                        noCache,
                        token),
                    Strings.ContainerBuilder_ImageUploadedToRegistry).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (published)
        {
            telemetry.LogPublishSuccess();
        }
    }

    public static async Task PublishImageAsync(
        MultiArchImage multiArchImage,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool published;
        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                published = await PushToLocalRegistryAsync(
                    multiArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    telemetry,
                    cancellationToken,
                    destinationImageReference.LocalRegistry!.LoadAsync).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                published = await PushToRemoteRegistryAsync(
                    multiArchImage,
                    sourceImageReference,
                    destinationImageReference,
                    Log,
                    cancellationToken,
                    destinationImageReference.RemoteRegistry!.PushManifestListAsync,
                    Strings.ImageIndexUploadedToRegistry).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (published)
        {
            telemetry.LogPublishSuccess();
        }
    }

    private static async Task<bool> PushToLocalRegistryAsync<T>(
        T image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
        Telemetry telemetry,
        CancellationToken cancellationToken,
        Func<T, SourceImageReference, DestinationImageReference, CancellationToken, Task> loadFunc)
    {
        ILocalRegistry localRegistry = destinationImageReference.LocalRegistry!;
        if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            telemetry.LogMissingLocalBinary();
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
            return false;
        }
        try
        {
            await loadFunc(image, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            Log.LogMessage(MessageImportance.High, Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);
            return true;
        }
        catch (UnableToDownloadFromRepositoryException)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToDownloadFromRepository), sourceImageReference);
            return false;
        }
        catch (InvalidAuthResponseException e)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidRegistryAuthResponse), e.Registry, e.Reason);
            return false;
        }
        catch (InvalidAuthRealmConfigurationException e)
        {
            Log.LogErrorWithCodeFromResources(e.ResourceName, e.EnvironmentVariableName, e.InvalidValue);
            return false;
        }
        catch (ContainerHttpException e)
        {
            Log.LogErrorFromException(e, true);
            return false;
        }
        catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
            return false;
        }
        catch (ArgumentException argEx)
        {
            Log.LogErrorFromException(argEx, showStackTrace: false);
            return false;
        }
        catch (DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
            return false;
        }
    }

    private static async Task<bool> PushToRemoteRegistryAsync<T>(
        T image,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Microsoft.Build.Utilities.TaskLoggingHelper Log,
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
            Log.LogMessage(MessageImportance.High, successMessage, destinationImageReference, destinationImageReference.RemoteRegistry!.RegistryName);
            return true;
        }
        catch (UnableToAccessRepositoryException)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName);
            return false;
        }
        catch (InvalidAuthResponseException e)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidRegistryAuthResponse), e.Registry, e.Reason);
            return false;
        }
        catch (InvalidAuthRealmConfigurationException e)
        {
            Log.LogErrorWithCodeFromResources(e.ResourceName, e.EnvironmentVariableName, e.InvalidValue);
            return false;
        }
        catch (ContainerHttpException e)
        {
            Log.LogErrorFromException(e, true);
            return false;
        }
        catch (Exception e)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
            Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
            return false;
        }
    }
}
