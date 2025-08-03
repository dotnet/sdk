// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class PushContainerToRemoteRegistry : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    [Required]
    public string Registry { get; set; } = string.Empty;

    [Required]
    public string Repository { get; set; } = string.Empty;

    [Required]
    public string[] Tags { get; set; } = [];

    [Required]
    public ITaskItem Manifest { get; set; } = null!;

    [Required]
    public ITaskItem Configuration { get; set; } = null!;

    [Required]
    public ITaskItem[] Layers { get; set; } = [];

    public void Cancel() => _cts.Cancel();

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public async Task<bool> ExecuteAsync()
    {
        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger(nameof(PushContainerToRemoteRegistry));
        var destinationRegistry = new Registry(Registry, msbuildLoggerFactory.CreateLogger(Registry), RegistryMode.Push);

        var telemetry = new Telemetry(new(null, null, Telemetry.GetRegistryType(destinationRegistry), null), Log);
        // functionally, we need to
        // * upload the layers
        using var _repositoryScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Repository"] = Repository,
            ["Registry"] = Registry
        });
        var layerUploadTasks = Layers.Select(l => new Layer(new(l.ItemSpec), GetDescriptor(l))).Select(async l =>
        {
            using var _layerScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["Layer"] = l.Descriptor.Digest,
                ["MediaType"] = l.Descriptor.MediaType,
                ["Digest"] = l.Descriptor.Digest,
                ["Size"] = l.Descriptor.Size
            });
            logger.LogTrace($"Pushing layer to {Registry}.");
            await destinationRegistry.PushLayerAsync(l, Repository, _cts.Token);
        }).ToArray();
        await Task.WhenAll(layerUploadTasks);

        // * upload the config
        var (size, digest, manifestStructure) = await ReadManifest().ConfigureAwait(false);
        _cts.Token.ThrowIfCancellationRequested();
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["MediaType"] = Configuration.GetMetadata("MediaType")!,
            ["Digest"] = Configuration.GetMetadata("Digest")!,
            ["Size"] = Configuration.GetMetadata("Size")!
        }))
        {
            logger.LogTrace($"Pushing config to {Registry}.");
            var configText = await File.ReadAllTextAsync(Configuration.ItemSpec, _cts.Token);
            var configBytes = Encoding.UTF8.GetBytes(configText);
            var configDigest = Digest.FromContentString(DigestAlgorithm.sha256, configText);
            var msbuildConfigDigest = Digest.Parse(Configuration.GetMetadata("Digest")!);
            if (msbuildConfigDigest != configDigest)
            {
                logger.LogError($"Configuration digest {msbuildConfigDigest} does not match the computed digest {configDigest} from the configuration file itself.");
            }
            if (configDigest == manifestStructure.Config.Digest)
            {
                logger.LogError($"Manifest config digest {manifestStructure.Config.Digest} does not match the computed digest {configDigest} from the configuration file itself.");
            }

            using (MemoryStream configStream = new(configBytes))
            {
                logger.LogInformation(Strings.Registry_ConfigUploadStarted, manifestStructure.Config.Digest);
                await destinationRegistry.UploadBlobAsync(Repository, manifestStructure.Config.Digest, configStream, _cts.Token);
                logger.LogInformation(Strings.Registry_ConfigUploaded);
            }
        }

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["MediaType"] = Manifest.GetMetadata("MediaType")!,
            ["Digest"] = Manifest.GetMetadata("Digest")!,
            ["Size"] = Manifest.GetMetadata("Size")!
        }))
        {
            var msbuildManifestDigest = Digest.Parse(Manifest.GetMetadata("Digest")!);
            if (manifestStructure.GetDigest() != msbuildManifestDigest)
            {
                logger.LogError($"Manifest structure digest {manifestStructure.GetDigest()} does not match the computed digest {msbuildManifestDigest} from the MSBuild/the manifest file itself.");
            }
            // * upload the manifest as a digest
            _cts.Token.ThrowIfCancellationRequested();
            logger.LogInformation(Strings.Registry_ManifestUploadStarted, Registry, manifestStructure.GetDigest());
            await destinationRegistry.UploadManifestAsync(Repository, Manifest.GetMetadata("Digest"), manifestStructure, _cts.Token);
            logger.LogInformation(Strings.Registry_ManifestUploaded, Registry);
        }

        // * upload the manifest as tags
        foreach (var tag in Tags)
        {
            using var _manifestTagScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["MediaType"] = Manifest.GetMetadata("MediaType")!,
                ["Digest"] = Manifest.GetMetadata("Digest")!,
                ["Size"] = Manifest.GetMetadata("Size")!,
                ["Tag"] = tag
            });
            _cts.Token.ThrowIfCancellationRequested();
            logger.LogInformation(Strings.Registry_TagUploadStarted, tag, Registry);
            await destinationRegistry.UploadManifestAsync(Repository, tag, manifestStructure, _cts.Token);
            logger.LogInformation(Strings.Registry_TagUploaded, tag, Registry);
        }
        telemetry.LogPublishSuccess();
        return true;
    }

    private Descriptor GetDescriptor(ITaskItem item)
    {
        var mediaType = item.GetMetadata("MediaType");
        var digest = Digest.Parse(item.GetMetadata("Digest")!);
        var size = long.Parse(item.GetMetadata("Size")!);
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = digest,
            Size = size
        };
    }

    private async Task<(long size, string digest, ManifestV2 manifest)> ReadManifest()
    {
        var size = long.Parse(Manifest.GetMetadata("Size")!);
        var digest = Manifest.GetMetadata("Digest")!;
        var manifestStructure = await Json.DeserializeAsync<ManifestV2>(File.OpenRead(Manifest.ItemSpec), cancellationToken: _cts.Token);
        return (size, digest, manifestStructure!);
    }
}
