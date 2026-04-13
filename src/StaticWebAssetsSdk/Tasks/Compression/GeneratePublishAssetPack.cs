// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Creates a zip archive containing the publish manifest and uncompressed original assets.
/// This pack is used as the "previous version" input for Compression Dictionary Transport
/// in subsequent builds.
/// </summary>
public class GeneratePublishAssetPack : Task
{
    /// <summary>
    /// Path to the publish manifest JSON file.
    /// </summary>
    [Required]
    public string ManifestPath { get; set; }

    /// <summary>
    /// The publish assets to include in the pack.
    /// </summary>
    [Required]
    public ITaskItem[] Assets { get; set; }

    /// <summary>
    /// Output path for the generated zip archive.
    /// </summary>
    [Required]
    public string PackOutputPath { get; set; }

    [Output]
    public string GeneratedPackPath { get; set; }

    public override bool Execute()
    {
        try
        {
            return ExecuteCore();
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
            return false;
        }
    }

    private bool ExecuteCore()
    {
        if (!File.Exists(ManifestPath))
        {
            Log.LogError("Manifest file '{0}' does not exist.", ManifestPath);
            return false;
        }

        var assets = StaticWebAsset.FromTaskItemGroup(Assets);

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(PackOutputPath));
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Delete existing pack to avoid stale entries
        if (File.Exists(PackOutputPath))
        {
            File.Delete(PackOutputPath);
        }

        using var zipStream = new FileStream(PackOutputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // Add the manifest
        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using (var manifestSource = File.OpenRead(ManifestPath))
        using (var manifestTarget = manifestEntry.Open())
        {
            manifestSource.CopyTo(manifestTarget);
        }

        // Add uncompressed original assets only
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assetCount = 0;

        foreach (var asset in assets)
        {
            // Skip compressed assets — only include originals
            if (!string.IsNullOrEmpty(asset.AssetTraitName) &&
                string.Equals(asset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Skipping compressed asset '{0}' from pack.",
                    asset.Identity);
                continue;
            }

            var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
            if (string.IsNullOrEmpty(relativePath))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Skipping asset '{0}' with empty relative path.",
                    asset.Identity);
                continue;
            }

            // Normalize path for zip entry
            var entryPath = "assets/" + relativePath.Replace('\\', '/');

            // Avoid duplicate entries
            if (!addedPaths.Add(entryPath))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Skipping duplicate asset entry '{0}' for asset '{1}'.",
                    entryPath,
                    asset.Identity);
                continue;
            }

            // Resolve the actual file path
            var filePath = asset.Identity;
            if (!File.Exists(filePath))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset file '{0}' does not exist on disk. Skipping.",
                    filePath);
                continue;
            }

            var assetEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using (var sourceStream = File.OpenRead(filePath))
            using (var targetStream = assetEntry.Open())
            {
                sourceStream.CopyTo(targetStream);
            }

            assetCount++;
            Log.LogMessage(
                MessageImportance.Low,
                "Added asset '{0}' to pack as '{1}'.",
                asset.Identity,
                entryPath);
        }

        GeneratedPackPath = Path.GetFullPath(PackOutputPath);

        Log.LogMessage(
            "Generated asset pack at '{0}' with {1} assets.",
            GeneratedPackPath,
            assetCount);

        return !Log.HasLoggedErrors;
    }
}
