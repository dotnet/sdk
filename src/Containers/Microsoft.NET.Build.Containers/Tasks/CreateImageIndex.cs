// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.LocalDaemons;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed class CreateImageIndex : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    #region Parameters
    /// <summary>
    /// The base registry to pull from.
    /// Ex: mcr.microsoft.com
    /// </summary>
    [Required]
    public string BaseRegistry { get; set; }

    /// <summary>
    /// The base image to pull.
    /// Ex: dotnet/runtime
    /// </summary>
    [Required]
    public string BaseImageName { get; set; }

    /// <summary>
    /// The base image tag.
    /// Ex: 6.0
    /// </summary>
    [Required]
    public string BaseImageTag { get; set; }

    /// <summary>
    /// Manifests to include in the image index.
    /// </summary>
    [Required]
    public ITaskItem[] GeneratedContainers { get; set; }

    /// <summary>
    /// The registry to push the image index to.
    /// </summary>
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The file path to which to write a tar.gz archive of the container image.
    /// </summary>
    public string ArchiveOutputPath { get; set; }

    /// <summary>
    /// The kind of local registry to use, if any.
    /// </summary>
    public string LocalRegistry { get; set; }

    /// <summary>
    /// The name of the output image index (manifest list) that will be pushed to the registry.
    /// </summary>
    [Required]
    public string Repository { get; set; }

    /// <summary>
    /// The tag to associate with the new image index (manifest list).
    /// </summary>
    [Required]
    public string[] ImageTags { get; set; }

    [Output]
    public string GeneratedArchiveOutputPath { get; set; }

    /// <summary>
    /// The generated image index (manifest list) in JSON format.
    /// </summary>
    [Output]
    public string GeneratedImageIndex { get; set; }

    public CreateImageIndex()
    {
        BaseRegistry = string.Empty;
        BaseImageName = string.Empty;
        BaseImageTag = string.Empty;
        GeneratedContainers = Array.Empty<ITaskItem>();
        OutputRegistry = string.Empty;
        ArchiveOutputPath = string.Empty;
        LocalRegistry = string.Empty;
        Repository = string.Empty;
        ImageTags = Array.Empty<string>();
        GeneratedArchiveOutputPath = string.Empty;
        GeneratedImageIndex = string.Empty;
    }
    #endregion

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public void Cancel() => _cancellationTokenSource.Cancel();

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

    private bool IsLocalPull => string.IsNullOrEmpty(BaseRegistry);

    public override bool Execute()
    {
        try
        {
            Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        catch (OperationCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        return !Log.HasLoggedErrors;
    }

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        // Look up in CreateNewImage how the image is published to registry/local daemon/tarball

        RegistryMode sourceRegistryMode = BaseRegistry.Equals(OutputRegistry, StringComparison.InvariantCultureIgnoreCase) ? RegistryMode.PullFromOutput : RegistryMode.Pull;
        Registry? sourceRegistry = IsLocalPull ? null : new Registry(BaseRegistry, logger, sourceRegistryMode);
        SourceImageReference sourceImageReference = new(sourceRegistry, BaseImageName, BaseImageTag);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            Repository,
            ImageTags,
            msbuildLoggerFactory,
            ArchiveOutputPath,
            OutputRegistry,
            LocalRegistry);

        var images = ParseImages(destinationImageReference.Kind);

        if (Log.HasLoggedErrors)
        {
            return false;
        }

        var telemetry = CreateTelemetryContext(sourceImageReference, destinationImageReference);

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                await PushToLocalRegistryAsync(images,
                    sourceImageReference,
                    destinationImageReference,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(images,
                    sourceImageReference,
                    destinationImageReference,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();

        return !Log.HasLoggedErrors;
    }

    private async Task PushToLocalRegistryAsync(BuiltImage[] images, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
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
            SafeLog(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);

            if (localRegistry is ArchiveFileRegistry archive)
            {
                GeneratedArchiveOutputPath = archive.ArchiveOutputPath;
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

    private async Task PushToRemoteRegistryAsync(BuiltImage[] images, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Telemetry telemetry,
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
            SafeLog(Strings.ImageIndexUploadedToRegistry, destinationImageReference, OutputRegistry);
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

    private void SafeLog(string message, params object[] formatParams)
    {
        if (BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }

    private BuiltImage[] ParseImages(DestinationImageReferenceKind destinationKind)
    {
        var images = new BuiltImage[GeneratedContainers.Length];

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var unparsedImage = GeneratedContainers[i];

            string config = unparsedImage.GetMetadata("Configuration");
            string manifestDigest = unparsedImage.GetMetadata("ManifestDigest");
            string manifest = unparsedImage.GetMetadata("Manifest");
            string manifestMediaType = unparsedImage.GetMetadata("ManifestMediaType");

            //TODO: add manifestmedia type to the error message
            if (string.IsNullOrEmpty(config) || string.IsNullOrEmpty(manifestDigest) || string.IsNullOrEmpty(manifest))
            {
                Log.LogError(Strings.InvalidImageMetadata, unparsedImage.ItemSpec);
                break;
            }

            var manifestV2 = JsonSerializer.Deserialize<ManifestV2>(manifest);
            if (manifestV2 == null)
            {
                //TODO: log new error about manifest not deserealized
                Log.LogError(Strings.InvalidImageMetadata, unparsedImage.ItemSpec);
                break;
            }

            string imageDigest = manifestV2.Config.digest;
            string imageSha = DigestUtils.GetShaFromDigest(imageDigest);
            // We don't need layers for remote registry, as the individual images should be pushed already
            var layers = destinationKind == DestinationImageReferenceKind.RemoteRegistry ? null : manifestV2.Layers;
            (string architecture, string os) = GetArchitectureAndOsFromConfig(config);

            images[i] = new BuiltImage()
            {
                Config = config,
                ImageDigest = imageDigest,
                ImageSha = imageSha,
                Manifest = manifest,
                ManifestDigest = manifestDigest,
                ManifestMediaType = manifestMediaType,
                Layers = layers,
                OS = os,
                Architecture = architecture
            };
        }

        return images;
    }

    private static (string, string) GetArchitectureAndOsFromConfig(string config)
    {
        var configJson = JsonNode.Parse(config) as JsonObject ??
            throw new ArgumentException("Image config should be a JSON object.");

        var architecture = configJson["architecture"]?.ToString() ??
            throw new ArgumentException("Image config should contain 'architecture'.");

        var os = configJson["os"]?.ToString() ??
            throw new ArgumentException("Image config should contain 'os'.");

        return (architecture, os);
    }

    private Telemetry CreateTelemetryContext(SourceImageReference source, DestinationImageReference destination)
    {
        var context = new PublishTelemetryContext(
            source.Registry is not null ? GetRegistryType(source.Registry) : null,
            null, // we don't support local pull yet, but we may in the future
            destination.RemoteRegistry is not null ? GetRegistryType(destination.RemoteRegistry) : null,
            destination.LocalRegistry is not null ? GetLocalStorageType(destination.LocalRegistry) : null);
        return new Telemetry(Log, context);
    }

    private RegistryType GetRegistryType(Registry r)
    {
        if (r.IsMcr) return RegistryType.MCR;
        if (r.IsGithubPackageRegistry) return RegistryType.GitHub;
        if (r.IsAmazonECRRegistry) return RegistryType.AWS;
        if (r.IsAzureContainerRegistry) return RegistryType.Azure;
        if (r.IsGoogleArtifactRegistry) return RegistryType.Google;
        if (r.IsDockerHub) return RegistryType.DockerHub;
        return RegistryType.Other;
    }

    private LocalStorageType GetLocalStorageType(ILocalRegistry r)
    {
        if (r is ArchiveFileRegistry) return LocalStorageType.Tarball;
        var d = r as DockerCli;
        System.Diagnostics.Debug.Assert(d != null, "Unknown local registry type");
        if (d.GetCommand() == DockerCli.DockerCommand) return LocalStorageType.Docker;
        else return LocalStorageType.Podman;
    }

    /// <summary>
    /// Interesting data about the container publish - used to track the usage rates of various sources/targets of the process
    /// and to help diagnose issues with the container publish overall.
    /// </summary>
    /// <param name="RemotePullType">If the base image came from a remote registry, what kind of registry was it?</param>
    /// <param name="LocalPullType">If the base image came from a local store of some kind, what kind of store was it?</param>
    /// <param name="RemotePushType">If the new image is being pushed to a remote registry, what kind of registry is it?</param>
    /// <param name="LocalPushType">If the new image is being stored in a local store of some kind, what kind of store is it?</param>
    private record class PublishTelemetryContext(RegistryType? RemotePullType, LocalStorageType? LocalPullType, RegistryType? RemotePushType, LocalStorageType? LocalPushType);
    private enum RegistryType { Azure, AWS, Google, GitHub, DockerHub, MCR, Other }
    private enum LocalStorageType { Docker, Podman, Tarball }

    private class Telemetry(Microsoft.Build.Utilities.TaskLoggingHelper Log, PublishTelemetryContext context)
    {
        private IDictionary<string, string?> ContextProperties() => new Dictionary<string, string?>
            {
                { nameof(context.RemotePullType), context.RemotePullType?.ToString() },
                { nameof(context.LocalPullType), context.LocalPullType?.ToString() },
                { nameof(context.RemotePushType), context.RemotePushType?.ToString() },
                { nameof(context.LocalPushType), context.LocalPushType?.ToString() }
            };

        public void LogPublishSuccess()
        {
            Log.LogTelemetry("sdk/container/publish/success", ContextProperties());
        }

        public void LogUnknownRepository()
        {
            var props = ContextProperties();
            props.Add("error", "unknown_repository");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogCredentialFailure(SourceImageReference _)
        {
            var props = ContextProperties();
            props.Add("error", "credential_failure");
            props.Add("direction", "pull");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogCredentialFailure(DestinationImageReference d)
        {
            var props = ContextProperties();
            props.Add("error", "credential_failure");
            props.Add("direction", "push");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogRidMismatch(string desiredRid, string[] availableRids)
        {
            var props = ContextProperties();
            props.Add("error", "rid_mismatch");
            props.Add("target_rid", desiredRid);
            props.Add("available_rids", string.Join(",", availableRids));
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogMissingLocalBinary()
        {
            var props = ContextProperties();
            props.Add("error", "missing_binary");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogLocalLoadError()
        {
            var props = ContextProperties();
            props.Add("error", "local_load");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

    }
}
