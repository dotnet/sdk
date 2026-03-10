// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
        var allAssets = new List<ITaskItem>();
        var allEndpoints = new List<ITaskItem>();

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

            // Phase 1: Group filtering on assets
            var includedAssets = new List<PackageManifestAsset>();
            var excludedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in manifest.Assets)
            {
                if (StaticWebAssetGroupFilter.IsAssetIncludedByGroups(asset.AssetGroups, sourceId, StaticWebAssetGroups))
                {
                    includedAssets.Add(asset);
                }
                else
                {
                    // Track the package-relative path that we would have resolved for this asset
                    var resolvedPath = ResolveAssetPath(asset, packageRoot);
                    excludedAssetPaths.Add(resolvedPath);
                }
            }

            // Phase 2: Cascading exclusion of related assets
            StaticWebAssetGroupFilter.CascadeExclusions(
                includedAssets,
                excludedAssetPaths,
                a => ResolveAssetPath(a, packageRoot),
                a => string.IsNullOrEmpty(a.RelatedAsset) ? null : ResolvePath(packageRoot, a.RelatedAsset));

            // Phase 3: Emit MSBuild items for included assets
            var assetMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in includedAssets)
            {
                var resolvedIdentity = ResolveAssetPath(asset, packageRoot);

                var identity = resolvedIdentity;
                var emittedSourceType = asset.SourceType;
                var emittedSourceId = sourceId;
                var emittedContentRoot = contentRoot;
                var emittedBasePath = asset.BasePath;
                var emittedAssetMode = asset.AssetMode;

                if (StaticWebAsset.SourceTypes.IsFramework(asset.SourceType))
                {
                    var fxDir = Path.Combine(IntermediateOutputPath, "fx", sourceId);
                    var resolvedRelativePath = StaticWebAssetPathPattern.PathWithoutTokens(asset.RelativePath);
                    var destPath = Path.GetFullPath(Path.Combine(fxDir, resolvedRelativePath));

                    if (!File.Exists(resolvedIdentity))
                    {
                        Log.LogError("Source file '{0}' does not exist for framework asset materialization.", resolvedIdentity);
                        return false;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(resolvedIdentity) > File.GetLastWriteTimeUtc(destPath))
                    {
                        File.Copy(resolvedIdentity, destPath, overwrite: true);
                    }

                    assetMapping[resolvedIdentity] = destPath;
                    identity = destPath;
                    emittedSourceType = StaticWebAsset.SourceTypes.Discovered;
                    emittedSourceId = ProjectPackageId;
                    emittedContentRoot = StaticWebAsset.NormalizeContentRootPath(fxDir);
                    emittedBasePath = ProjectBasePath;
                    emittedAssetMode = StaticWebAsset.AssetModes.CurrentProject;
                }

                var taskItem = new TaskItem(identity);
                taskItem.SetMetadata("SourceType", emittedSourceType);
                taskItem.SetMetadata("SourceId", emittedSourceId);
                taskItem.SetMetadata("ContentRoot", emittedContentRoot);
                taskItem.SetMetadata("BasePath", emittedBasePath);
                taskItem.SetMetadata("RelativePath", asset.RelativePath);
                taskItem.SetMetadata("AssetKind", asset.AssetKind);
                taskItem.SetMetadata("AssetMode", emittedAssetMode);
                taskItem.SetMetadata("AssetRole", asset.AssetRole);
                taskItem.SetMetadata("AssetTraitName", asset.AssetTraitName);
                taskItem.SetMetadata("AssetTraitValue", asset.AssetTraitValue);
                taskItem.SetMetadata("AssetGroups", asset.AssetGroups);
                taskItem.SetMetadata("Fingerprint", asset.Fingerprint);
                taskItem.SetMetadata("Integrity", asset.Integrity);
                taskItem.SetMetadata("CopyToOutputDirectory", asset.CopyToOutputDirectory);
                taskItem.SetMetadata("CopyToPublishDirectory", asset.CopyToPublishDirectory);
                taskItem.SetMetadata("FileLength", asset.FileLength);
                taskItem.SetMetadata("LastWriteTime", asset.LastWriteTime);
                taskItem.SetMetadata("OriginalItemSpec", identity);

                // Remap RelatedAsset to resolved absolute path
                if (!string.IsNullOrEmpty(asset.RelatedAsset))
                {
                    taskItem.SetMetadata("RelatedAsset", ResolvePath(packageRoot, asset.RelatedAsset));
                }
                else
                {
                    taskItem.SetMetadata("RelatedAsset", "");
                }

                allAssets.Add(taskItem);
            }

            // Phase 4: Emit endpoints, filtering out those for excluded assets
            foreach (var endpoint in manifest.Endpoints)
            {
                var resolvedAssetFile = ResolvePath(packageRoot, endpoint.AssetFile);
                if (excludedAssetPaths.Contains(resolvedAssetFile))
                {
                    continue;
                }

                if (assetMapping.TryGetValue(resolvedAssetFile, out var remappedAssetFile))
                {
                    resolvedAssetFile = remappedAssetFile;
                }

                var taskItem = new TaskItem(endpoint.Route);
                taskItem.SetMetadata("AssetFile", resolvedAssetFile);

                // Serialize selectors, properties, headers as JSON strings for MSBuild metadata
                taskItem.SetMetadata("Selectors",
                    JsonSerializer.Serialize(endpoint.Selectors, StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointSelectorArray));
                taskItem.SetMetadata("EndpointProperties",
                    JsonSerializer.Serialize(endpoint.EndpointProperties, StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointPropertyArray));
                taskItem.SetMetadata("ResponseHeaders",
                    JsonSerializer.Serialize(endpoint.ResponseHeaders, StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointResponseHeaderArray));

                allEndpoints.Add(taskItem);
            }
        }

        Assets = allAssets.ToArray();
        Endpoints = allEndpoints.ToArray();

        return !Log.HasLoggedErrors;
    }

    private static string ResolveAssetPath(PackageManifestAsset asset, string packageRoot)
    {
        // PackagePath is the pre-computed resolved package-relative path (e.g. "staticwebassets/css/site.css").
        // Resolve it against PackageRoot to get the absolute Identity path.
        return ResolvePath(packageRoot, asset.PackagePath);
    }

    private static string ResolvePath(string packageRoot, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
