// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Reads StaticWebAssetPackageManifest items, deserializes the JSON manifests,
// applies group filtering, and emits matching assets and endpoints as MSBuild items.
// This replaces the eager import of XML .props files with a task-based read-and-filter approach.
public class ReadPackageAssetsManifest : Task
{
    [Required]
    public ITaskItem[] PackageManifests { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    public string IntermediateOutputPath { get; set; }

    public string ProjectPackageId { get; set; }

    public string ProjectBasePath { get; set; }

    [Output]
    public ITaskItem[] Assets { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        if (string.IsNullOrEmpty(IntermediateOutputPath))
        {
            Log.LogError("IntermediateOutputPath is required.");
            return false;
        }

        var groupLookup = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);
        var allAssets = new List<StaticWebAsset>();
        var allEndpoints = new List<StaticWebAssetEndpoint>();

        foreach (var manifestItem in PackageManifests)
        {
            var manifestPath = manifestItem.ItemSpec;
            var packageRoot = manifestItem.GetMetadata("PackageRoot");
            var contentRoot = manifestItem.GetMetadata("ContentRoot");

            if (!File.Exists(manifestPath))
            {
                Log.LogError("Package manifest file '{0}' not found.", manifestPath);
                return false;
            }

            var manifest = ReadManifest(manifestPath);
            if (manifest == null)
            {
                return false;
            }

            if (manifest.Assets == null || manifest.Assets.Count == 0)
            {
                continue;
            }

            // Copy manifest assets — Identity and RelatedAsset are already
            // package-relative paths as written by GeneratePackageAssetsManifestFile.
            var assets = new StaticWebAsset[manifest.Assets.Count];
            var index = 0;
            foreach (var entry in manifest.Assets)
            {
                var asset = new StaticWebAsset(entry.Value);
                assets[index++] = asset;
            }

            var (includedAssets, excludedPaths) = StaticWebAsset.FilterByGroup(assets, groupLookup, skipDeferred: true);

            // Filter endpoints on raw package-relative paths before resolving anything.
            var endpointGroups = StaticWebAssetEndpointGroup.CreateEndpointGroups(manifest.Endpoints ?? []);
            var (_, includedEndpoints) = StaticWebAssetEndpointGroup.ComputeFilteredEndpoints(endpointGroups, excludedPaths);

            if (!ResolveAssetsAndEndpoints(includedAssets, includedEndpoints, packageRoot, contentRoot))
            {
                return false;
            }

            allAssets.AddRange(includedAssets);
            allEndpoints.AddRange(includedEndpoints);
        }

        Assets = allAssets.Select(asset => asset.ToTaskItem()).ToArray();
        Endpoints = StaticWebAssetEndpoint.ToTaskItems(allEndpoints);

        return !Log.HasLoggedErrors;
    }

    // Resolve paths — framework assets materialize into the fx intermediate
    // folder; everything else resolves against the package root.
    // Build a lookup for framework asset identities so RelatedAsset and
    // endpoint.AssetFile references can resolve to the materialized path.
    private bool ResolveAssetsAndEndpoints(
        List<StaticWebAsset> assets,
        List<StaticWebAssetEndpoint> endpoints,
        string packageRoot,
        string contentRoot)
    {
        var frameworkPaths = new Dictionary<string, string>(OSPath.PathComparer);
        var normalizedContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot);

        foreach (var asset in assets)
        {
            if (StaticWebAsset.SourceTypes.IsFramework(asset.SourceType))
            {
                // Materialize framework assets into the fx intermediate folder using the shared
                // routine so they are transformed identically across all consumption paths
                // (package manifest, P2P, and project). This clears AssetGroups so endpoint
                // generation does not skip the materialized asset, and normalizes it.
                var originalIdentity = asset.Identity;
                asset.Identity = ResolvePath(packageRoot, asset.Identity);
                var (materialized, _, _) = StaticWebAsset.MaterializeFrameworkAsset(
                    asset, IntermediateOutputPath, ProjectPackageId, ProjectBasePath, Log);
                if (materialized is null || Log.HasLoggedErrors)
                {
                    return false;
                }
                frameworkPaths[originalIdentity] = materialized.Identity;
            }
            else
            {
                asset.Identity = ResolvePath(packageRoot, asset.Identity);
                asset.OriginalItemSpec = asset.Identity;
                asset.ContentRoot = normalizedContentRoot;
            }

            if (!string.IsNullOrEmpty(asset.RelatedAsset))
            {
                asset.RelatedAsset = frameworkPaths.TryGetValue(asset.RelatedAsset, out var fxRelated)
                    ? fxRelated
                    : ResolvePath(packageRoot, asset.RelatedAsset);
            }
        }

        foreach (var endpoint in endpoints)
        {
            endpoint.AssetFile = frameworkPaths.TryGetValue(endpoint.AssetFile, out var fxEp)
                ? fxEp
                : ResolvePath(packageRoot, endpoint.AssetFile);
        }

        return true;
    }

    private StaticWebAssetPackageManifest ReadManifest(string manifestPath)
    {
        StaticWebAssetPackageManifest manifest;
        try
        {
            var json = File.ReadAllBytes(manifestPath);
            manifest = JsonSerializer.Deserialize(json,
                StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetPackageManifest);
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to read package manifest '{0}': {1}", manifestPath, ex.Message);
            return null;
        }

        if (manifest is null)
        {
            Log.LogError("Package manifest '{0}' deserialized to null.", manifestPath);
            return null;
        }

        if (manifest.Version != StaticWebAssetPackageManifest.CurrentVersion)
        {
            Log.LogError("Unsupported package manifest version {0} in '{1}'. Expected version {2}.", manifest.Version, manifestPath, StaticWebAssetPackageManifest.CurrentVersion);
            return null;
        }

        if (!string.Equals(manifest.ManifestType, StaticWebAssetPackageManifest.PackageManifestType, StringComparison.Ordinal))
        {
            Log.LogError("Unexpected manifest type '{0}' in '{1}'. Expected '{2}'.", manifest.ManifestType, manifestPath, StaticWebAssetPackageManifest.PackageManifestType);
            return null;
        }

        return manifest;
    }

    private static string ResolvePath(string packageRoot, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
