// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdatePackageStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    // The intermediate output path for materializing framework assets (e.g., $(IntermediateOutputPath)staticwebassets\)
    public string IntermediateOutputPath { get; set; }

    // The consuming project's PackageId
    public string ProjectPackageId { get; set; }

    // The consuming project's StaticWebAssetBasePath
    public string ProjectBasePath { get; set; }

    [Output]
    public ITaskItem[] UpdatedAssets { get; set; }

    [Output]
    public ITaskItem[] OriginalAssets { get; set; }

    // Framework assets that were materialized (original items, for endpoint remapping)
    // ItemSpec = old asset Identity, NewPath metadata = new materialized path
    [Output]
    public ITaskItem[] MaterializedFrameworkAssets { get; set; }

    // Endpoints with AssetFile remapped for materialized framework assets
    [Output]
    public ITaskItem[] RemappedEndpoints { get; set; }

    // Original endpoints that were remapped (to remove from the endpoint list)
    [Output]
    public ITaskItem[] OriginalRemappedEndpoints { get; set; }

    // All endpoints for the consuming project (needed to remap framework asset endpoints)
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        try
        {
            var originalAssets = new List<ITaskItem>();
            var updatedAssets = new List<ITaskItem>();
            var materializedFrameworkAssets = new List<ITaskItem>();

            for (var i = 0; i < Assets.Length; i++)
            {
                var candidate = Assets[i];
                var sourceType = candidate.GetMetadata(nameof(StaticWebAsset.SourceType));

                if (StaticWebAsset.SourceTypes.IsPackage(sourceType))
                {
                    originalAssets.Add(candidate);
                    updatedAssets.Add(StaticWebAsset.FromV1TaskItem(candidate).ToTaskItem());
                }
                else if (StaticWebAsset.SourceTypes.IsFramework(sourceType))
                {
                    originalAssets.Add(candidate);
                    var (transformed, mapping) = MaterializeFrameworkAsset(candidate);
                    if (transformed != null)
                    {
                        updatedAssets.Add(transformed);
                        materializedFrameworkAssets.Add(mapping);
                    }
                }
            }

            OriginalAssets = [.. originalAssets];
            UpdatedAssets = [.. updatedAssets];
            MaterializedFrameworkAssets = [.. materializedFrameworkAssets];

            // Remap endpoints for materialized framework assets
            var remappedEndpoints = new List<ITaskItem>();
            var originalRemappedEndpoints = new List<ITaskItem>();
            if (Endpoints != null && materializedFrameworkAssets.Count > 0)
            {
                // Build a mapping from old identity to new materialized path
                var assetMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mapping in materializedFrameworkAssets)
                {
                    var oldPath = mapping.ItemSpec;
                    var newPath = mapping.GetMetadata("NewPath");
                    if (!string.IsNullOrEmpty(oldPath) && !string.IsNullOrEmpty(newPath))
                    {
                        assetMapping[oldPath] = newPath;
                    }
                }

                foreach (var endpoint in Endpoints)
                {
                    var assetFile = endpoint.GetMetadata("AssetFile");
                    if (!string.IsNullOrEmpty(assetFile) && assetMapping.TryGetValue(assetFile, out var newAssetFile))
                    {
                        originalRemappedEndpoints.Add(endpoint);
                        var remapped = new Microsoft.Build.Utilities.TaskItem(endpoint);
                        remapped.SetMetadata("AssetFile", newAssetFile);
                        remappedEndpoints.Add(remapped);
                        Log.LogMessage(MessageImportance.Low, "Remapped endpoint '{0}' AssetFile from '{1}' to '{2}'.",
                            endpoint.ItemSpec, assetFile, newAssetFile);
                    }
                }
            }

            RemappedEndpoints = [.. remappedEndpoints];
            OriginalRemappedEndpoints = [.. originalRemappedEndpoints];
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
        }

        return !Log.HasLoggedErrors;
    }

    private (ITaskItem, ITaskItem) MaterializeFrameworkAsset(ITaskItem candidate)
    {
        // Parse the asset from V1 task item format (applies defaults, normalizes, validates)
        var asset = StaticWebAsset.FromV1TaskItem(candidate);

        var originalSourceId = asset.SourceId;
        var relativePath = asset.RelativePath;
        var oldIdentity = asset.Identity;

        // Compute materialized destination path: {IntermediateOutputPath}fx/{OriginalSourceId}/{RelativePath}
        var fxDir = Path.Combine(IntermediateOutputPath, "fx", originalSourceId);
        var destPath = Path.Combine(fxDir, StaticWebAsset.Normalize(relativePath));
        destPath = Path.GetFullPath(destPath);

        // Copy the file from the package cache to the intermediate output
        var sourceFile = asset.Identity;
        if (!File.Exists(sourceFile))
        {
            // Let it throw naturally per the decisions document
            Log.LogMessage(MessageImportance.Low, "Source file '{0}' does not exist for framework asset materialization.", sourceFile);
            File.Copy(sourceFile, destPath); // This will throw FileNotFoundException
            return (null, null);
        }

        var destDir = Path.GetDirectoryName(destPath);
        Directory.CreateDirectory(destDir);

        // Only copy if source is newer or dest doesn't exist
        if (!File.Exists(destPath) || File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destPath))
        {
            File.Copy(sourceFile, destPath, overwrite: true);
            Log.LogMessage(MessageImportance.Low, "Materialized framework asset '{0}' to '{1}'.", sourceFile, destPath);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "Framework asset '{0}' already up to date at '{1}'.", sourceFile, destPath);
        }

        // Transform the asset metadata to adopt it into the current project
        asset.Identity = destPath;
        asset.OriginalItemSpec = destPath;
        asset.ContentRoot = EnsureTrailingSlash(Path.GetDirectoryName(Path.Combine(fxDir, "placeholder")));
        asset.SourceType = StaticWebAsset.SourceTypes.Discovered;
        asset.SourceId = ProjectPackageId;
        asset.BasePath = ProjectBasePath;
        asset.AssetMode = StaticWebAsset.AssetModes.CurrentProject;

        // Recompute fingerprint/integrity since the file was copied (may have different metadata)
        var fileInfo = new FileInfo(destPath);
        var (fingerprint, integrity) = StaticWebAsset.ComputeFingerprintAndIntegrity(fileInfo);
        asset.Fingerprint = fingerprint;
        asset.Integrity = integrity;
        asset.FileLength = fileInfo.Length;
        asset.LastWriteTime = fileInfo.LastWriteTimeUtc;

        // Create mapping item for endpoint remapping (old identity -> new identity)
        var mapping = new Microsoft.Build.Utilities.TaskItem(oldIdentity);
        mapping.SetMetadata("NewPath", destPath);

        return (asset.ToTaskItem(), mapping);
    }

    private static string EnsureTrailingSlash(string path)
    {
        if (!string.IsNullOrEmpty(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path + Path.DirectorySeparatorChar;
        }
        return path;
    }
}
