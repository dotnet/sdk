// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
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
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        var destinationRegistry = new Registry(Registry, logger, RegistryMode.Push);

        // functionally, we need to
        // * upload the layers
        var layerUploadTasks = Layers.Select(l => new Layer(new(l.ItemSpec), GetDescriptor(l))).Select(l => destinationRegistry.PushLayerAsync(l, Repository, _cts.Token)).ToArray();
        await Task.WhenAll(layerUploadTasks);

        // * upload the config
        var (size, digest, manifestStructure) = await ReadManifest().ConfigureAwait(false);
        _cts.Token.ThrowIfCancellationRequested();
        var configText = await File.ReadAllTextAsync(Configuration.ItemSpec, _cts.Token);
        var configBytes = Encoding.UTF8.GetBytes(configText);
        var configDigest = DigestUtils.GetDigest(configText);
        System.Diagnostics.Debug.Assert(configDigest == manifestStructure.Config.digest, "Manifest config digest does not match the computed digest from the configuration file.");
        using (MemoryStream configStream = new(configBytes))
        {
            logger.LogInformation(Strings.Registry_ConfigUploadStarted, manifestStructure.Config.digest);
            await destinationRegistry.UploadBlobAsync(Repository, manifestStructure.Config.digest, configStream, _cts.Token);
            logger.LogInformation(Strings.Registry_ConfigUploaded);
        }

        // * upload the manifest as a digest
        _cts.Token.ThrowIfCancellationRequested();
        logger.LogInformation(Strings.Registry_ManifestUploadStarted, Registry, manifestStructure.GetDigest());
        await destinationRegistry.UploadManifestAsync(Repository, manifestStructure.GetDigest(), manifestStructure, _cts.Token);
        logger.LogInformation(Strings.Registry_ManifestUploaded, Registry);

        // * upload the manifest as tags
        foreach (var tag in Tags)
        {
            _cts.Token.ThrowIfCancellationRequested();
            logger.LogInformation(Strings.Registry_TagUploadStarted, tag, Registry);
            await destinationRegistry.UploadManifestAsync(Repository, tag, manifestStructure, _cts.Token);
            logger.LogInformation(Strings.Registry_TagUploaded, tag, Registry);
        }
        return true;
    }

    private Descriptor GetDescriptor(ITaskItem item)
    {
        var mediaType = item.GetMetadata("MediaType");
        var digest = item.GetMetadata("Digest");
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
        var manifestStructure = await JsonSerializer.DeserializeAsync<ManifestV2>(File.OpenRead(Manifest.ItemSpec), cancellationToken: _cts.Token);
        return (size, digest, manifestStructure!);
    }
}
