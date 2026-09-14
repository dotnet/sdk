// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class GenerateStaticWebAssetsManifest : Task
{
    [Required]
    public string Source { get; set; }

    [Required]
    public string BasePath { get; set; }

    [Required]
    public string Mode { get; set; }

    [Required]
    public string ManifestType { get; set; }

    [Required]
    public ITaskItem[] ReferencedProjectsConfigurations { get; set; }

    [Required]
    public ITaskItem[] DiscoveryPatterns { get; set; }

    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    [Required]
    public string ManifestPath { get; set; }

    public string ManifestCacheFilePath { get; set; }

    public override bool Execute()
    {
        try
        {
            var assets = StaticWebAsset.FromTaskItemGroup(Assets, validate: true);
            Array.Sort(assets, (l, r) => string.CompareOrdinal(l.Identity, r.Identity));

            var endpoints = FilterPublishEndpointsIfNeeded(assets);
            Array.Sort(endpoints, (l, r) => string.CompareOrdinal(l.Route, r.Route) switch
            {
                0 => string.CompareOrdinal(l.AssetFile, r.AssetFile),
                int result => result,
            });

            Log.LogMessage(MessageImportance.Low, "Generating manifest for '{0}' assets and '{1}' endpoints", assets.Length, endpoints.Length);

            var assetsByTargetPath = GroupAssetsByTargetPath(assets);
            foreach (var group in assetsByTargetPath)
            {
                if (!StaticWebAsset.ValidateAssetGroup(group.Key, group.Value, out var reason))
                {
                    Log.LogError(reason);
                    return false;
                }
            }

            var discoveryPatterns = DiscoveryPatterns
                .OrderBy(a => a.ItemSpec)
                .Select(StaticWebAssetsDiscoveryPattern.FromTaskItem)
                .ToArray();

            var referencedProjectsConfiguration = ReferencedProjectsConfigurations.OrderBy(a => a.ItemSpec)
                .Select(StaticWebAssetsManifest.ReferencedProjectConfiguration.FromTaskItem)
                .ToArray();

            PersistManifest(
                StaticWebAssetsManifest.Create(
                    Source,
                    BasePath,
                    Mode,
                    ManifestType,
                    referencedProjectsConfiguration,
                    discoveryPatterns,
                    [.. assets],
                    endpoints));
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
        }
        return !Log.HasLoggedErrors;
    }

    private StaticWebAssetEndpoint[] FilterPublishEndpointsIfNeeded(StaticWebAsset[] assets)
    {
        // Only include endpoints for assets that are going to be available in production. We do the filtering
        // inside the manifest because its cumbersome to do it in MSBuild directly.
        if (StaticWebAssetsManifest.ManifestTypes.IsPublish(ManifestType))
        {
            var assetsByIdentity = assets.ToDictionary(a => a.Identity, a => a, OSPath.PathComparer);
            var filteredEndpoints = new List<StaticWebAssetEndpoint>();

            foreach (var endpoint in Endpoints.Select(StaticWebAssetEndpoint.FromTaskItem))
            {
                if (assetsByIdentity.ContainsKey(endpoint.AssetFile))
                {
                    filteredEndpoints.Add(endpoint);
                    Log.LogMessage(MessageImportance.Low, $"Accepted endpoint: Route='{endpoint.Route}', AssetFile='{endpoint.AssetFile}'");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"Filtered out endpoint: Endpoint='{endpoint.Route}' AssetFile='{endpoint.AssetFile}'");
                }
            }

            return [.. filteredEndpoints];
        }

        return StaticWebAssetEndpoint.FromItemGroup(Endpoints);
    }

    private void PersistManifest(StaticWebAssetsManifest manifest)
    {
        var cacheFileExists = File.Exists(ManifestCacheFilePath);
        var fileExists = File.Exists(ManifestPath);
        var existingManifestHash = cacheFileExists ?
            File.ReadAllText(ManifestCacheFilePath) :
            fileExists ? StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath)).Hash : "";

        if (!fileExists || !string.Equals(manifest.Hash, existingManifestHash, StringComparison.Ordinal))
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetsManifest);
            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest because manifest file '{ManifestPath}' does not exist.");
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest version '{manifest.Hash}' is different from existing manifest hash '{existingManifestHash}'.");
            }
            File.WriteAllBytes(ManifestPath, data);
            if (!string.IsNullOrEmpty(ManifestCacheFilePath))
            {
                File.WriteAllText(ManifestCacheFilePath, manifest.Hash);
            }
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest version '{manifest.Hash}' has not changed.");
        }
    }

    private static Dictionary<string, (StaticWebAsset First, StaticWebAsset Second, List<StaticWebAsset> Other)> GroupAssetsByTargetPath(StaticWebAsset[] assets)
    {
        var result = new Dictionary<string, (StaticWebAsset First, StaticWebAsset Second, List<StaticWebAsset> Other)>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            var targetPath = asset.ComputeTargetPath("", '/');

            if (result.TryGetValue(targetPath, out var existing))
            {
                if (existing.Second == null)
                {
                    // We have first but not second
                    result[targetPath] = (existing.First, asset, null);
                }
                else
                {
                    // We already have first and second, add to rest
                    existing.Other ??= [];
                    existing.Other.Add(asset);
                }
            }
            else
            {
                // First asset with this path
                result.Add(targetPath, (asset, null, null));
            }
        }

        return result;
    }
}
