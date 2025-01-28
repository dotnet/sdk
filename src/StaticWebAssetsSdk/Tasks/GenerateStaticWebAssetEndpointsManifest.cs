// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class GenerateStaticWebAssetEndpointsManifest : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; } = [];

    [Required]
    public ITaskItem[] Endpoints { get; set; } = [];

    [Required]
    public string ManifestType { get; set; }

    [Required]
    public string Source { get; set; }

    [Required]
    public string ManifestPath { get; set; }

    public string CacheFilePath { get; set; }

    public override bool Execute()
    {
        if (!string.IsNullOrEmpty(CacheFilePath) && File.Exists(ManifestPath) && File.GetLastWriteTimeUtc(ManifestPath) > File.GetLastWriteTimeUtc(CacheFilePath))
        {
            Log.LogMessage(MessageImportance.Low, "Skipping manifest generation because manifest file '{0}' is up to date.", ManifestPath);
            return true;
        }

        try
        {
            var manifest = CreateManifest(Log, Assets, Endpoints, ManifestType, Source);

            this.PersistFileIfChanged(manifest, ManifestPath, StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetEndpointsManifest);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, null);
            return false;
        }

        return !Log.HasLoggedErrors;
    }

    public static StaticWebAssetEndpointsManifest CreateManifest(TaskLoggingHelper log, ITaskItem[] assets, ITaskItem[] endpointItems, string manifestType, string Source)
    {
        // Get the list of the asset that need to be part of the manifest (this is similar to GenerateStaticWebAssetsDevelopmentManifest)
        var manifestAssets = ComputeManifestAssets(log, assets.Select(StaticWebAsset.FromTaskItem), manifestType, Source)
            .ToDictionary(a => a.ResolvedAsset.Identity, a => a, OSPath.PathComparer);

        // Filter out the endpoints to those that point to the assets that are part of the manifest
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(endpointItems);
        var filteredEndpoints = new List<StaticWebAssetEndpoint>();

        foreach (var endpoint in endpoints)
        {
            if (!manifestAssets.TryGetValue(endpoint.AssetFile, out var asset))
            {
                log.LogMessage(MessageImportance.Low, "Skipping endpoint '{0}' because the asset '{1}' is not part of the manifest", endpoint.Route, endpoint.AssetFile);
                continue;
            }

            filteredEndpoints.Add(endpoint);
            // Update the endpoint to use the target path of the asset, this will be relative to the wwwroot
            var path = endpoint.AssetFile;

            endpoint.AssetFile = asset.ResolvedAsset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);
            endpoint.Route = asset.ResolvedAsset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);

            log.LogMessage(MessageImportance.Low, "Including endpoint '{0}' for asset '{1}' with final location '{2}'", endpoint.Route, path, asset.TargetPath);
        }

        var manifest = new StaticWebAssetEndpointsManifest()
        {
            Version = 1,
            ManifestType = manifestType,
            Endpoints = [.. filteredEndpoints]
        };
        return manifest;
    }

    private static IEnumerable<TargetPathAssetPair> ComputeManifestAssets(TaskLoggingHelper Log, IEnumerable<StaticWebAsset> assets, string kind, string source)
    {
        var assetsByTargetPath = assets
            .GroupBy(a => a.ComputeTargetPath("", '/'));

        foreach (var group in assetsByTargetPath)
        {
            var asset = StaticWebAsset.ChooseNearestAssetKind(group, kind).SingleOrDefault();

            if (asset == null)
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because it is not a '{1}' or 'All' asset.", group.Key, kind);
                continue;
            }

            if (asset.HasSourceId(source) && !StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetInCurrentProject(asset, StaticWebAssetsManifest.ManifestModes.Root))
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because asset mode is '{1}'",
                    asset.Identity,
                    asset.AssetMode);

                continue;
            }

            yield return new TargetPathAssetPair(group.Key, asset);
        }
    }

    private sealed class TargetPathAssetPair(string targetPath, StaticWebAsset asset)
    {
        public string TargetPath { get; } = targetPath;
        public StaticWebAsset ResolvedAsset { get; } = asset;
    }
}
