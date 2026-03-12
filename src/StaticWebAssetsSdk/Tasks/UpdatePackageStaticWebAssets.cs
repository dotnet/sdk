// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdatePackageStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    public string IntermediateOutputPath { get; set; }

    public string ProjectPackageId { get; set; }

    public string ProjectBasePath { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    [Output]
    public ITaskItem[] UpdatedAssets { get; set; }

    [Output]
    public ITaskItem[] OriginalAssets { get; set; }

    [Output]
    public ITaskItem[] RemappedEndpoints { get; set; }

    [Output]
    public ITaskItem[] FilteredEndpoints { get; set; }

    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        try
        {
            var groupLookup = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);
            var originalAssets = new List<ITaskItem>();
            var updatedAssets = new List<StaticWebAsset>();
            var assetMapping = new Dictionary<string, string>(OSPath.PathComparer);
            var excludedAssetFiles = new HashSet<string>(OSPath.PathComparer);

            for (var i = 0; i < Assets.Length; i++)
            {
                var candidate = Assets[i];
                var candidateAsset = StaticWebAsset.FromV1TaskItem(candidate);
                var sourceType = candidateAsset.SourceType;

                if (StaticWebAsset.SourceTypes.IsPackage(sourceType))
                {
                    if (!candidateAsset.MatchesGroups(groupLookup, skipDeferred: true))
                    {
                        excludedAssetFiles.Add(candidateAsset.Identity);
                        // Add to originalAssets so the target removes it from @(StaticWebAsset),
                        // but do NOT add to updatedAssets so it doesn't get re-added.
                        originalAssets.Add(candidate);
                        continue;
                    }

                    originalAssets.Add(candidate);
                    updatedAssets.Add(candidateAsset);
                }
                else if (StaticWebAsset.SourceTypes.IsFramework(sourceType))
                {
                    if (!candidateAsset.MatchesGroups(groupLookup, skipDeferred: true))
                    {
                        excludedAssetFiles.Add(candidateAsset.Identity);
                        originalAssets.Add(candidate);
                        continue;
                    }

                    originalAssets.Add(candidate);
                    var (transformed, oldPath) = MaterializeFrameworkAsset(candidate);
                    if (transformed != null)
                    {
                        updatedAssets.Add(transformed);
                        assetMapping[oldPath] = transformed.Identity;
                    }
                }
            }

            // Cascading exclusion: sort ensures parents before dependents,
            // then a single pass removes related assets whose primary was excluded.
            var sortedUpdatedAssets = StaticWebAsset.SortByRelatedAsset(updatedAssets);
            updatedAssets.Clear();
            foreach (var asset in sortedUpdatedAssets)
            {
                if (!string.IsNullOrEmpty(asset.RelatedAsset) && excludedAssetFiles.Contains(asset.RelatedAsset))
                {
                    excludedAssetFiles.Add(asset.Identity);
                    continue;
                }

                if (excludedAssetFiles.Contains(asset.Identity))
                {
                    continue;
                }

                updatedAssets.Add(asset);
            }

            OriginalAssets = [.. originalAssets];
            UpdatedAssets = updatedAssets.Select(asset => asset.ToTaskItem()).ToArray();

            if (Endpoints != null && (assetMapping.Count > 0 || excludedAssetFiles.Count > 0))
            {
                RemapEndpoints(assetMapping, excludedAssetFiles);
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
        }

        return !Log.HasLoggedErrors;
    }

    private void RemapEndpoints(Dictionary<string, string> assetMapping, HashSet<string> excludedAssetFiles)
    {
        var remappedEndpoints = new List<ITaskItem>();
        var filteredEndpoints = new List<ITaskItem>();

        foreach (var endpoint in Endpoints)
        {
            var assetFile = endpoint.GetMetadata("AssetFile");

            // Exclude endpoints whose asset file was filtered out by group definitions
            if (!string.IsNullOrEmpty(assetFile) && excludedAssetFiles.Contains(assetFile))
            {
                Log.LogMessage(MessageImportance.Low, "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
                    endpoint.ItemSpec, assetFile);
                continue;
            }

            // Remap endpoints for materialized framework assets
            if (!string.IsNullOrEmpty(assetFile) && assetMapping.TryGetValue(assetFile, out var newAssetFile))
            {
                var newEndpoint = new TaskItem(endpoint);
                newEndpoint.SetMetadata("AssetFile", newAssetFile);
                Log.LogMessage(MessageImportance.Low, "Remapped endpoint '{0}' AssetFile from '{1}' to '{2}'.",
                    endpoint.ItemSpec, assetFile, newAssetFile);
                remappedEndpoints.Add(newEndpoint);
                filteredEndpoints.Add(newEndpoint);
            }
            else
            {
                // Pass through unchanged
                filteredEndpoints.Add(endpoint);
            }
        }

        RemappedEndpoints = [.. remappedEndpoints];
        FilteredEndpoints = [.. filteredEndpoints];
    }

    private (StaticWebAsset, string) MaterializeFrameworkAsset(ITaskItem candidate)
    {
        var asset = StaticWebAsset.FromV1TaskItem(candidate);

        var originalSourceId = asset.SourceId;
        var relativePath = asset.RelativePath;
        var oldIdentity = asset.Identity;

        var fxDir = Path.Combine(IntermediateOutputPath, "fx", originalSourceId);
        var destPath = Path.Combine(fxDir, StaticWebAsset.Normalize(relativePath));
        destPath = Path.GetFullPath(destPath);

        var sourceFile = asset.Identity;
        if (!File.Exists(sourceFile))
        {
            Log.LogError("Source file '{0}' does not exist for framework asset materialization.", sourceFile);
            return (null, null);
        }

        var destDir = Path.GetDirectoryName(destPath);
        Directory.CreateDirectory(destDir);

        if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destPath))
        {
            File.Copy(sourceFile, destPath, overwrite: true);
            Log.LogMessage(MessageImportance.Low, "Materialized framework asset '{0}' to '{1}'.", sourceFile, destPath);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "Framework asset '{0}' already up to date at '{1}'.", sourceFile, destPath);
        }

        asset.Identity = destPath;
        asset.OriginalItemSpec = destPath;
        asset.ContentRoot = StaticWebAsset.NormalizeContentRootPath(fxDir);
        asset.SourceType = StaticWebAsset.SourceTypes.Discovered;
        asset.SourceId = ProjectPackageId;
        asset.BasePath = ProjectBasePath;
        asset.AssetMode = StaticWebAsset.AssetModes.CurrentProject;
        asset.Normalize();

        return (asset, oldIdentity);
    }

}
