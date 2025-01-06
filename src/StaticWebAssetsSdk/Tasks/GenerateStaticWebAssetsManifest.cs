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
            var assets = Assets.OrderBy(a => a.GetMetadata("FullPath")).Select(StaticWebAsset.FromTaskItem).ToArray();

            var endpoints = FilterPublishEndpointsIfNeeded(assets)
                .OrderBy(a => a.Route)
                .ThenBy(a => a.AssetFile)
                .ToArray();

            Log.LogMessage(MessageImportance.Low, "Generating manifest for '{0}' assets and '{1}' endpoints", assets.Length, endpoints.Length);

            var assetsByTargetPath = assets.GroupBy(a => a.ComputeTargetPath("", '/'), StringComparer.OrdinalIgnoreCase);
            foreach (var group in assetsByTargetPath)
            {
                if (!StaticWebAsset.ValidateAssetGroup(group.Key, [.. group], out var reason))
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

    private IEnumerable<StaticWebAssetEndpoint> FilterPublishEndpointsIfNeeded(IEnumerable<StaticWebAsset> assets)
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

            return filteredEndpoints;
        }

        return Endpoints.Select(StaticWebAssetEndpoint.FromTaskItem);
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
}
