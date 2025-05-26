// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class DownloadContainerManifest : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    [Required]
    public string Registry { get; set; } = string.Empty;

    [Required]
    public string Repository { get; set; } = string.Empty;

    public string? Tag { get; set; }

    public string? Digest { get; set; }

    [Required]
    public string ContentStore { get; set; } = string.Empty;


    [Output]
    public ITaskItem[] Manifests { get; set; } = Array.Empty<ITaskItem>();

    [Output]
    public ITaskItem[] Configs { get; set; } = Array.Empty<ITaskItem>();

    [Output]
    public ITaskItem[] Layers { get; set; } = Array.Empty<ITaskItem>();


    public void Cancel() => _cts.Cancel();

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();
    
    public async Task<bool> ExecuteAsync()
    {
        if (Tag is null && Digest is null)
        {
            throw new ArgumentException("Must provide one of Tag and Digest");
        }

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        var store = new ContentStore(new(ContentStore));
        var registry = new Registry(Registry, logger, RegistryMode.Pull, store: store);

        // download the manifest from the registry (maybe), and download child manifests if it's a multi-image manifest
        // we accept a nullable coercion here for tag/digest because we just checked that at least one is present
        // TODO: set skipCache to false when we have some kind of user-settable semantic for Docker's ímage pull policy concept
        var outerManifest = await registry.GetManifestCore(Repository, (Digest ?? Tag)!, _cts.Token, skipCache: true);
        if (outerManifest is ManifestV2 singleArchManifest)
        {
            var configJson = await registry.GetJsonBlobCore(Repository, singleArchManifest.Config.digest, singleArchManifest.Config.mediaType, _cts.Token);
            var platformData = configJson.Deserialize<PlatformInformation>();
            SetOutputs([(singleArchManifest, platformData)], store);
            return true;
        }
        else if (outerManifest is IMultiImageManifest multiArchManifest)
        {
            if (multiArchManifest is ManifestListV2 manifestList)
            {
                var manifests = await Task.WhenAll(manifestList.manifests.Select(GetPlatformDataForManifest));
                SetOutputs(manifests, store);
                return true;
            }
            else if (multiArchManifest is ImageIndexV1 imageIndex)
            {
                var manifests = await Task.WhenAll(imageIndex.manifests.Select(GetOciPlatformDataForManifest));
                SetOutputs(manifests, store);
                return true;
            }
            else
            {
                throw new InvalidOperationException("Unknown multi-arch manifest type");
            }
        }
        else
        {
            throw new InvalidOperationException("Unknown manifest type");
        }
        async Task<(ManifestV2, PlatformInformation)> GetPlatformDataForManifest(PlatformSpecificManifest p)
        {
            var manifest = await registry.GetManifestCore(Repository, p.digest, _cts.Token);
            if (manifest is not ManifestV2 manifestV2)
            {
                throw new InvalidOperationException("Expected single-arch manifest");
            }
            return (manifestV2, p.platform);
        }

        async Task<(ManifestV2, PlatformInformation)> GetOciPlatformDataForManifest(PlatformSpecificOciManifest p)
        {
            var manifest = await registry.GetManifestCore(Repository, p.digest, _cts.Token);
            if (manifest is not ManifestV2 manifestV2)
            {
                throw new InvalidOperationException("Expected single-arch manifest");
            }
            return (manifestV2, p.platform);
        }
    }

    void SetOutputs(IReadOnlyList<(ManifestV2 manifest, PlatformInformation platformInfo)> manifests, ContentStore store)
    {
        var manifestItems = new List<ITaskItem>(manifests.Count);
        var configItems = new List<ITaskItem>(manifests.Count);
        var layerItems = new List<ITaskItem>(manifests.Count * manifests[0].manifest.Layers.Count); //estimate
        foreach (var (manifest, platform) in manifests)
        {
            var manifestDescriptor = new Descriptor(manifest.MediaType!, manifest.KnownDigest!, 0);
            var manifestLocalPath = store.PathForDescriptor(manifestDescriptor);

            var itemRid = RidMapping.CreateRidForPlatform(platform);
            var manifestItem = new Microsoft.Build.Utilities.TaskItem(manifestLocalPath);
            manifestItem.SetMetadata("MediaType", manifest.MediaType ?? string.Empty);
            manifestItem.SetMetadata("ConfigDigest", manifest.Config.digest);
            manifestItem.SetMetadata("RuntimeIdentifier", itemRid);
            manifestItems.Add(manifestItem);

            var configDescriptor = new Descriptor(manifest.Config.mediaType, manifest.Config.digest, manifest.Config.size);
            var configLocalPath = store.PathForDescriptor(configDescriptor);
            var configItem = new Microsoft.Build.Utilities.TaskItem(configLocalPath);
            configItem.SetMetadata("MediaType", manifest.Config.mediaType ?? string.Empty);
            configItem.SetMetadata("Size", manifest.Config.size.ToString());
            configItem.SetMetadata("Digest", manifest.Config.digest);
            configItem.SetMetadata("RuntimeIdentifier", itemRid);
            configItems.Add(configItem);

            foreach (var layer in manifest.Layers)
            {
                var layerDescriptor = new Descriptor(layer.mediaType!, layer.digest, layer.size);
                var layerLocalPath = store.PathForDescriptor(layerDescriptor);
                var layerItem = new Microsoft.Build.Utilities.TaskItem(layerLocalPath);
                layerItem.SetMetadata("MediaType", layer.mediaType ?? string.Empty);
                layerItem.SetMetadata("Size", layer.size.ToString());
                layerItem.SetMetadata("Digest", layer.digest);
                layerItem.SetMetadata("ConfigDigest", manifest.Config.digest);
                layerItem.SetMetadata("RuntimeIdentifier", itemRid);
                layerItems.Add(layerItem);
            }
        }

        Manifests = manifestItems.ToArray();
        Configs = configItems.ToArray();
        Layers = layerItems.ToArray();
    }
}
