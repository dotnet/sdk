// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.

// using Microsoft.NET.Build.Containers.LocalDaemons;
// using Microsoft.NET.Build.Containers.Resources;

// namespace Microsoft.NET.Build.Containers;


// internal static class ImagePublisher
// {
//     public static async Task PublishImage(BuiltImage builtImage, SourceImageReference sourceImageReference,
//         DestinationImageReference destinationImageReference,
//         Telemetry telemetry,
//         CancellationToken cancellationToken)
//     {
//         cancellationToken.ThrowIfCancellationRequested();

//         switch (destinationImageReference.Kind)
//         {
//             case DestinationImageReferenceKind.LocalRegistry:
//                 await PushToLocalRegistryAsync(builtImage,
//                     sourceImageReference,
//                     destinationImageReference,
//                     telemetry,
//                     cancellationToken).ConfigureAwait(false);
//                 break;
//             case DestinationImageReferenceKind.RemoteRegistry:
//                 await PushToRemoteRegistryAsync(builtImage,
//                     sourceImageReference,
//                     destinationImageReference,
//                     cancellationToken).ConfigureAwait(false);
//                 break;
//             default:
//                 throw new ArgumentOutOfRangeException();
//         }

//         telemetry.LogPublishSuccess();
//     }

//     private static async Task PushToLocalRegistryAsync(
//         BuiltImage builtImage,
//         SourceImageReference sourceImageReference,
//         DestinationImageReference destinationImageReference,
//         Telemetry telemetry,
//         CancellationToken cancellationToken)
//     {
//         ILocalRegistry localRegistry = destinationImageReference.LocalRegistry!;
//         if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
//         {
//             telemetry.LogMissingLocalBinary();
//             Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
//             return;
//         }
//         try
//         {
//             await localRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
//             SafeLog(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);

//             if (localRegistry is ArchiveFileRegistry archive)
//             {
//                 GeneratedArchiveOutputPath = archive.ArchiveOutputPath;
//             }
//         }
//         catch (ContainerHttpException e)
//         {
//             if (BuildEngine != null)
//             {
//                 Log.LogErrorFromException(e, true);
//             }
//         }
//         catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
//         {
//             telemetry.LogLocalLoadError();
//             Log.LogErrorFromException(dle, showStackTrace: false);
//         }
//         catch (ArgumentException argEx)
//         {
//             Log.LogErrorFromException(argEx, showStackTrace: false);
//         }
//     }

//     private static async Task PushToRemoteRegistryAsync(
//         BuiltImage builtImage,
//         SourceImageReference sourceImageReference,
//         DestinationImageReference destinationImageReference,
//         CancellationToken cancellationToken)
//     {
//         try
//         {
//             await destinationImageReference.RemoteRegistry!.PushAsync(
//                 builtImage,
//                 sourceImageReference,
//                 destinationImageReference,
//                 cancellationToken).ConfigureAwait(false);
//             SafeLog(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, OutputRegistry);
//         }
//         catch (UnableToAccessRepositoryException)
//         {
//             if (BuildEngine != null)
//             {
//                 Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName);
//             }
//         }
//         catch (ContainerHttpException e)
//         {
//             if (BuildEngine != null)
//             {
//                 Log.LogErrorFromException(e, true);
//             }
//         }
//         catch (Exception e)
//         {
//             if (BuildEngine != null)
//             {
//                 Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
//                 Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
//             }
//         }
//     }
// }