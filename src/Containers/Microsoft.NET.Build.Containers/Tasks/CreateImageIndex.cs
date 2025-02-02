// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateImageIndex : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
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

        if (LocalRegistry == "Podman")
        {
            Log.LogError(Strings.ImageIndex_PodmanNotSupported);
            return false;
        }

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

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

        logger.LogInformation(Strings.BuildingImageIndex, destinationImageReference, string.Join(", ", images.Select(i => i.ManifestDigest)));

        var telemetry = new Telemetry(sourceImageReference, destinationImageReference, Log);

        await ImagePublisher.PublishImageAsync(images, sourceImageReference, destinationImageReference, Log, BuildEngine, telemetry, cancellationToken)
            .ConfigureAwait(false);

        GeneratedArchiveOutputPath = ArchiveOutputPath;

        return !Log.HasLoggedErrors;
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

            if (string.IsNullOrEmpty(config) || string.IsNullOrEmpty(manifestDigest) || string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(manifestMediaType))
            {
                Log.LogError(Strings.InvalidImageMetadata);
                break;
            }

            var manifestV2 = JsonSerializer.Deserialize<ManifestV2>(manifest);
            if (manifestV2 == null)
            {
                Log.LogError(Strings.InvalidImageManifest);
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
}
