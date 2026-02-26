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
            return true;
        }

        if (StaticWebAssetGroups == null || StaticWebAssetGroups.Length == 0)
        {
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

        foreach (var group in StaticWebAssetGroups)
        {
            var groupName = group.ItemSpec;
            var groupValue = group.GetMetadata("Value");
            var groupSourceId = group.GetMetadata("SourceId");

            if (!string.IsNullOrEmpty(groupSourceId) &&
                !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (assetGroupDict.TryGetValue(groupName, out var assetValue))
            {
                if (!string.Equals(assetValue, groupValue, StringComparison.Ordinal))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding asset '{0}' because group '{1}' value '{2}' does not match required '{3}'.",
                        candidate.ItemSpec, groupName, assetValue, groupValue);
                    return false;
                }
            }
        }

        return true;
    }

    private void RemapEndpoints(Dictionary<string, string> assetMapping, HashSet<string> excludedAssetFiles)
    {
        var remappedEndpoints = new List<ITaskItem>();

        var endpointsByIdentity = new Dictionary<string, List<ITaskItem>>(StringComparer.Ordinal);
        foreach (var endpoint in Endpoints)
        {
            var identity = endpoint.ItemSpec;
            if (!endpointsByIdentity.TryGetValue(identity, out var group))
            {
                group = new List<ITaskItem>();
                endpointsByIdentity[identity] = group;
            }
            group.Add(endpoint);
        }

        foreach (var kvp in endpointsByIdentity)
        {
            var identity = kvp.Key;
            var group = kvp.Value;

            var groupExcluded = false;
            var groupNeedsRemapping = false;

            foreach (var endpoint in group)
            {
                var assetFile = endpoint.GetMetadata("AssetFile");
                if (!string.IsNullOrEmpty(assetFile))
                {
                    if (excludedAssetFiles.Contains(assetFile))
                    {
                        groupExcluded = true;
                        break;
                    }
                    if (assetMapping.ContainsKey(assetFile))
                    {
                        groupNeedsRemapping = true;
                    }
                }
            }

            if (groupExcluded)
            {
                Log.LogMessage(MessageImportance.Low, "Excluding endpoints for excluded asset group identity '{0}'.", identity);
                continue;
            }

            if (groupNeedsRemapping)
            {
                foreach (var endpoint in group)
                {
                    var newEndpoint = new TaskItem(endpoint);
                    var assetFile = endpoint.GetMetadata("AssetFile");
                    if (!string.IsNullOrEmpty(assetFile) && assetMapping.TryGetValue(assetFile, out var newAssetFile))
                    {
                        newEndpoint.SetMetadata("AssetFile", newAssetFile);
                        Log.LogMessage(MessageImportance.Low, "Remapped endpoint '{0}' AssetFile from '{1}' to '{2}'.",
                            identity, assetFile, newAssetFile);
                    }
                    remappedEndpoints.Add(newEndpoint);
                }
            }
        }

        RemappedEndpoints = [.. remappedEndpoints];
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
