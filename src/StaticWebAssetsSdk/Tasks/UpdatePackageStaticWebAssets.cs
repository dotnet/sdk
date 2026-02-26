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
            var originalAssets = new List<ITaskItem>();
            var updatedAssets = new List<ITaskItem>();
            var assetMapping = new Dictionary<string, string>(OSPath.PathComparer);
            var excludedAssetFiles = new HashSet<string>(OSPath.PathComparer);

            for (var i = 0; i < Assets.Length; i++)
            {
                var candidate = Assets[i];
                var sourceType = candidate.GetMetadata(nameof(StaticWebAsset.SourceType));

                if (StaticWebAsset.SourceTypes.IsPackage(sourceType))
                {
                    if (!IsAssetIncludedByGroups(candidate))
                    {
                        excludedAssetFiles.Add(candidate.GetMetadata("FullPath"));
                        // Add to originalAssets so the target removes it from @(StaticWebAsset),
                        // but do NOT add to updatedAssets so it doesn't get re-added.
                        originalAssets.Add(candidate);
                        continue;
                    }

                    originalAssets.Add(candidate);
                    updatedAssets.Add(StaticWebAsset.FromV1TaskItem(candidate).ToTaskItem());
                }
                else if (StaticWebAsset.SourceTypes.IsFramework(sourceType))
                {
                    if (!IsAssetIncludedByGroups(candidate))
                    {
                        excludedAssetFiles.Add(candidate.GetMetadata("FullPath"));
                        originalAssets.Add(candidate);
                        continue;
                    }

                    originalAssets.Add(candidate);
                    var (transformed, oldPath) = MaterializeFrameworkAsset(candidate);
                    if (transformed != null)
                    {
                        updatedAssets.Add(transformed.ToTaskItem());
                        assetMapping[oldPath] = transformed.Identity;
                    }
                }
            }

            // Cascading exclusion: exclude related/alternative assets whose primary was excluded.
            // Repeat until no new exclusions are found (for recursive chains).
            if (excludedAssetFiles.Count > 0)
            {
                bool changed;
                do
                {
                    changed = false;
                    for (var i = updatedAssets.Count - 1; i >= 0; i--)
                    {
                        var asset = updatedAssets[i];
                        var relatedAsset = asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset));
                        if (!string.IsNullOrEmpty(relatedAsset) && excludedAssetFiles.Contains(relatedAsset))
                        {
                            var assetFullPath = asset.GetMetadata("FullPath");
                            if (!string.IsNullOrEmpty(assetFullPath))
                            {
                                excludedAssetFiles.Add(assetFullPath);
                            }
                            Log.LogMessage(MessageImportance.Low,
                                "Excluding related asset '{0}' because its primary '{1}' was excluded by group filtering.",
                                asset.ItemSpec, relatedAsset);
                            updatedAssets.RemoveAt(i);
                            changed = true;
                        }
                    }
                } while (changed);
            }

            OriginalAssets = [.. originalAssets];
            UpdatedAssets = [.. updatedAssets];

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

    private bool IsAssetIncludedByGroups(ITaskItem candidate)
    {
        var assetGroups = candidate.GetMetadata("AssetGroups");
        if (string.IsNullOrEmpty(assetGroups))
        {
            // Assets without AssetGroups are unconditional — always included.
            return true;
        }

        var groupEntries = assetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var assetGroupDict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in groupEntries)
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex > 0)
            {
                var name = entry.Substring(0, eqIndex);
                var value = entry.Substring(eqIndex + 1);
                assetGroupDict[name] = value;
            }
        }

        var sourceId = candidate.GetMetadata(nameof(StaticWebAsset.SourceId));

        // AND-matching: every name=value entry on the asset must be satisfied by at least one
        // applicable StaticWebAssetGroup declaration. If no declarations exist at all, grouped
        // assets are excluded (no declaration can satisfy any requirement).
        foreach (var kvp in assetGroupDict)
        {
            var entryName = kvp.Key;
            var entryValue = kvp.Value;
            var satisfied = false;

            if (StaticWebAssetGroups != null)
            {
                foreach (var group in StaticWebAssetGroups)
                {
                    var groupSourceId = group.GetMetadata("SourceId");
                    if (!string.IsNullOrEmpty(groupSourceId) &&
                        !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(group.ItemSpec, entryName, StringComparison.Ordinal) &&
                        string.Equals(group.GetMetadata("Value"), entryValue, StringComparison.Ordinal))
                    {
                        satisfied = true;
                        break;
                    }
                }
            }

            if (!satisfied)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Excluding asset '{0}' because group requirement '{1}={2}' has no matching StaticWebAssetGroup declaration.",
                    candidate.ItemSpec, entryName, entryValue);
                return false;
            }
        }

        return true;
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
