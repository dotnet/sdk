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
        var groupLookup = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);
        var allAssets = new List<StaticWebAsset>();
        var allEndpoints = new List<StaticWebAssetEndpoint>();

        foreach (var manifestItem in PackageManifests)
        {
            var manifestPath = manifestItem.ItemSpec;
            var sourceId = manifestItem.GetMetadata("SourceId");
            var contentRoot = manifestItem.GetMetadata("ContentRoot");
            var packageRoot = manifestItem.GetMetadata("PackageRoot");

            if (!File.Exists(manifestPath))
            {
                Log.LogWarning("Package manifest file '{0}' not found.", manifestPath);
                continue;
            }

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
                return false;
            }

            if (manifest.Version != 1)
            {
                Log.LogError("Unsupported package manifest version {0} in '{1}'. Expected version 1.", manifest.Version, manifestPath);
                return false;
            }

            var resolvedManifestAssets = ResolveManifestAssets(manifest.Assets, sourceId, contentRoot, packageRoot);

            // Group filtering + cascading exclusion.
            // Assets are sorted so parents appear before dependents, enabling a single-pass
            // cascade: if a parent was excluded, skip its children without evaluating groups.
            var sortedAssets = StaticWebAsset.SortByRelatedAsset(resolvedManifestAssets);
            var includedAssets = new List<StaticWebAsset>(sortedAssets.Length);
            var excludedAssetPaths = new HashSet<string>(OSPath.PathComparer);

            foreach (var asset in sortedAssets)
            {
                if (!string.IsNullOrEmpty(asset.RelatedAsset) && excludedAssetPaths.Contains(asset.RelatedAsset))
                {
                    excludedAssetPaths.Add(asset.Identity);
                    continue;
                }

                var matches = asset.MatchesGroups(groupLookup, skipDeferred: true);
                if (matches)
                {
                    includedAssets.Add(asset);
                }
                else
                {
                    excludedAssetPaths.Add(asset.Identity);
                }
            }

            // Emit MSBuild items for included assets
            var assetMapping = new Dictionary<string, string>(OSPath.PathComparer);
            foreach (var asset in includedAssets)
            {
                if (StaticWebAsset.SourceTypes.IsFramework(asset.SourceType))
                {
                    var fxDir = Path.Combine(IntermediateOutputPath, "fx", sourceId);
                    var resolvedRelativePath = StaticWebAssetPathPattern.PathWithoutTokens(asset.RelativePath);
                    var destPath = Path.GetFullPath(Path.Combine(fxDir, resolvedRelativePath));

                    if (!File.Exists(asset.Identity))
                    {
                        Log.LogError("Source file '{0}' does not exist for framework asset materialization.", asset.Identity);
                        return false;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(asset.Identity) > File.GetLastWriteTimeUtc(destPath))
                    {
                        File.Copy(asset.Identity, destPath, overwrite: true);
                    }

                    assetMapping[asset.Identity] = destPath;
                    asset.Identity = destPath;
                    asset.OriginalItemSpec = destPath;
                    asset.SourceType = StaticWebAsset.SourceTypes.Discovered;
                    asset.SourceId = ProjectPackageId;
                    asset.ContentRoot = StaticWebAsset.NormalizeContentRootPath(fxDir);
                    asset.BasePath = ProjectBasePath;
                    asset.AssetMode = StaticWebAsset.AssetModes.CurrentProject;
                }

                allAssets.Add(asset);
            }

            if (assetMapping.Count > 0)
            {
                foreach (var asset in allAssets)
                {
                    if (!string.IsNullOrEmpty(asset.RelatedAsset) && assetMapping.TryGetValue(asset.RelatedAsset, out var remappedRelatedAsset))
                    {
                        asset.RelatedAsset = remappedRelatedAsset;
                    }
                }
            }

            // Phase 4: Emit endpoints, filtering out those for excluded assets
            foreach (var endpoint in manifest.Endpoints ?? [])
            {
                var resolvedAssetFile = ResolvePath(packageRoot, endpoint.AssetFile);
                if (excludedAssetPaths.Contains(resolvedAssetFile))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
                        endpoint.Route, resolvedAssetFile);
                    continue;
                }

                if (assetMapping.TryGetValue(resolvedAssetFile, out var remappedAssetFile))
                {
                    resolvedAssetFile = remappedAssetFile;
                }

                endpoint.AssetFile = resolvedAssetFile;
                allEndpoints.Add(endpoint);
            }
        }

        Assets = allAssets.Select(asset => asset.ToTaskItem()).ToArray();
        Endpoints = StaticWebAssetEndpoint.ToTaskItems(allEndpoints);

        return !Log.HasLoggedErrors;
    }

    private static List<StaticWebAsset> ResolveManifestAssets(
        Dictionary<string, StaticWebAsset> manifestAssets,
        string sourceId,
        string contentRoot,
        string packageRoot)
    {
        if (manifestAssets == null || manifestAssets.Count == 0)
        {
            return new List<StaticWebAsset>();
        }

        // Resolve paths, build an identity-keyed index, and eagerly add assets
        // whose dependencies are already satisfied (no related asset, or parent
        // already processed). Deferred assets are resolved in a second pass.
        var byIdentity = new Dictionary<string, StaticWebAsset>(manifestAssets.Count, OSPath.PathComparer);
        var result = new List<StaticWebAsset>(manifestAssets.Count);
        var processed = new HashSet<string>(OSPath.PathComparer);
        var deferred = new List<StaticWebAsset>();

        foreach (var assetEntry in manifestAssets)
        {
            var packagePath = assetEntry.Key;
            var asset = new StaticWebAsset(assetEntry.Value)
            {
                Identity = ResolvePath(packageRoot, packagePath),
                OriginalItemSpec = ResolvePath(packageRoot, packagePath)
            };

            if (string.IsNullOrEmpty(asset.SourceId))
            {
                asset.SourceId = sourceId;
            }

            if (!string.IsNullOrEmpty(contentRoot))
            {
                asset.ContentRoot = contentRoot;
            }

            if (!string.IsNullOrEmpty(asset.RelatedAsset))
            {
                asset.RelatedAsset = ResolvePath(packageRoot, asset.RelatedAsset);
            }

            byIdentity[asset.Identity] = asset;

            // Eagerly add if no parent, or parent already processed.
            if (string.IsNullOrEmpty(asset.RelatedAsset) || processed.Contains(asset.RelatedAsset))
            {
                processed.Add(asset.Identity);
                result.Add(asset);
            }
            else
            {
                deferred.Add(asset);
            }
        }

        // Second pass: only needed for assets whose parent appeared later in the dictionary.
        for (var i = 0; i < deferred.Count; i++)
        {
            AddWithDependencies(deferred[i], byIdentity, processed, result);
        }

        return result;
    }

    private static void AddWithDependencies(
        StaticWebAsset asset,
        Dictionary<string, StaticWebAsset> byIdentity,
        HashSet<string> processed,
        List<StaticWebAsset> result)
    {
        if (!processed.Add(asset.Identity))
        {
            return;
        }

        if (byIdentity.TryGetValue(asset.RelatedAsset, out var parent))
        {
            AddWithDependencies(parent, byIdentity, processed, result);
        }

        result.Add(asset);
    }

    private static string ResolvePath(string packageRoot, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
