// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
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

    private bool IsLocalPull => string.IsNullOrWhiteSpace(BaseRegistry);

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
        SourceImageReference sourceImageReference = new(sourceRegistry, BaseImageName, BaseImageTag, BaseImageDigest);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            Repository,
            ImageTags,
            msbuildLoggerFactory,
            ArchiveOutputPath,
            OutputRegistry,
            LocalRegistry);

        var images = await ParseImages(GeneratedContainers, cancellationToken);
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        var multiArchImage = CreateMultiArchImage(images, destinationImageReference.Kind);
        using var fileStream = File.OpenWrite(GeneratedManifestPath);
        await JsonSerializer.SerializeAsync(fileStream, multiArchImage.ImageIndex);

        GeneratedImageIndex = JsonSerializer.Serialize(multiArchImage.ImageIndex);
        GeneratedArchiveOutputPath = ArchiveOutputPath;

        logger.LogInformation(Strings.BuildingImageIndex, destinationImageReference, string.Join(", ", images.Select(i => i.ManifestDigest)));

        var telemetry = new Telemetry(sourceImageReference, destinationImageReference, Log);
        // TODO: remove this push and extract to another Task
        await ImagePublisher.PublishImageAsync(multiArchImage, sourceImageReference, destinationImageReference, Log, telemetry, cancellationToken)
            .ConfigureAwait(false);

        return !Log.HasLoggedErrors;
    }

    private async Task<BuiltImage[]> ParseImages(ITaskItem[] containers, CancellationToken ctok)
    {
        var images = await Task.WhenAll(containers.Select(itemDescription => ParseBuiltImage(itemDescription)));
        var validImages = images.Where(image => image is not null).Cast<BuiltImage>().ToArray()!;
        return validImages;

        async Task<BuiltImage?> ParseBuiltImage(ITaskItem itemDescription)
        {
            var configFile = new FileInfo(itemDescription.GetMetadata("ConfigurationPath"));
            var manifestFile = new FileInfo(itemDescription.GetMetadata("ManifestPath"));

            if (!configFile.Exists || !manifestFile.Exists)
            {
                Log.LogError(Strings.InvalidImageMetadata);
                return null;
            }

            if (await GetArchitectureAndOsFromConfig(configFile, ctok) is not (var config, var architecture, var os))
            {
                Log.LogError(Strings.InvalidImageConfig);
                return null;
            }

            ManifestV2 manifestV2 = (await JsonSerializer.DeserializeAsync<ManifestV2>(manifestFile.OpenRead()))!;
            return new BuiltImage()
            {
                Config = config,
                Manifest = manifestV2,
                Layers = manifestV2.Layers,
                OS = os,
                Architecture = architecture
            };
        }
    }

    private async Task<(JsonObject, string, string)?> GetArchitectureAndOsFromConfig(FileInfo config, CancellationToken cTok)
    {
        using var fileStream = config.OpenRead();
        var configJson = await JsonNode.ParseAsync(fileStream, cancellationToken: cTok) as JsonObject;
        if (configJson is null)
        {
            Log.LogError(Strings.InvalidImageConfig);
            return null;
        }
        var architecture = configJson["architecture"]?.ToString();
        if (architecture is null)
        {
            Log.LogError(Strings.ImageConfigMissingArchitecture);
            return null;
        }
        var os = configJson["os"]?.ToString();
        if (os is null)
        {
            Log.LogError(Strings.ImageConfigMissingOs);
            return null;
        }
        return (configJson, architecture, os);
    }

    private static MultiArchImage CreateMultiArchImage(BuiltImage[] images, DestinationImageReferenceKind destinationImageKind)
    {
        switch (destinationImageKind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                return new MultiArchImage()
                {
                    // For multi-arch we publish only oci-formatted image tarballs.
                    ImageIndex = ImageIndexGenerator.GenerateDockerManifestList(images, SchemaTypes.OciManifestV1, SchemaTypes.OciImageIndexV1),
                    Images = images
                };
            case DestinationImageReferenceKind.RemoteRegistry:
                var imageIndex = ImageIndexGenerator.GenerateImageIndex(images);
                return new MultiArchImage()
                {
                    ImageIndex = imageIndex,
                    // For remote registry we don't need individual images, as they should be pushed already
                };
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
