// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Extracts a previous asset pack (.zip), reads the embedded SWA manifest, and matches
/// current assets to previous versions by RelativePath. The output is a set of dictionary
/// candidates that downstream tasks use to produce dictionary-compressed (dcz) assets.
/// </summary>
public class ResolveDictionaryCandidates : Task
{
    /// <summary>
    /// Path to the previous asset pack zip file. Contains a manifest.json and assets/{RelativePath} entries.
    /// </summary>
    [Required]
    public string AssetPackPath { get; set; }

    /// <summary>
    /// The current publish assets to find dictionary candidates for.
    /// </summary>
    [Required]
    public ITaskItem[] CurrentAssets { get; set; }

    /// <summary>
    /// Directory where matched previous assets will be extracted.
    /// </summary>
    [Required]
    public string OutputPath { get; set; }

    /// <summary>
    /// Dictionary candidates: one per current asset that matched a previous version.
    /// Metadata: DictionaryPath, DictionaryHash, RelativePath.
    /// </summary>
    [Output]
    public ITaskItem[] DictionaryCandidates { get; set; }

    public override bool Execute()
    {
        if (string.IsNullOrEmpty(AssetPackPath) || !File.Exists(AssetPackPath))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "No previous asset pack found at '{0}'. Skipping dictionary candidate resolution.",
                AssetPackPath);
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

        if (CurrentAssets is null || CurrentAssets.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No current assets provided. Skipping dictionary candidate resolution.");
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

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
        using var zipStream = File.OpenRead(AssetPackPath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // Read the manifest from the pack
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null)
        {
            Log.LogError("Asset pack '{0}' does not contain a manifest.json entry.", AssetPackPath);
            return false;
        }

        StaticWebAssetsManifest manifest;
        using (var manifestStream = manifestEntry.Open())
        using (var memoryStream = new MemoryStream())
        {
            manifestStream.CopyTo(memoryStream);
            manifest = StaticWebAssetsManifest.FromJsonBytes(memoryStream.ToArray());
        }

        if (manifest.Assets == null || manifest.Assets.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "Previous asset pack manifest contains no assets.");
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

        // Build lookup: RelativePath → previous asset (only uncompressed originals)
        var previousByRelativePath = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var prevAsset in manifest.Assets)
        {
            // Skip compressed assets — we only want originals as dictionary sources
            if (!string.IsNullOrEmpty(prevAsset.AssetTraitName) &&
                string.Equals(prevAsset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = prevAsset.ComputePathWithoutTokens(prevAsset.RelativePath);
            if (!string.IsNullOrEmpty(relativePath))
            {
                // First match wins (shouldn't have duplicates in a well-formed manifest)
                if (!previousByRelativePath.ContainsKey(relativePath))
                {
                    previousByRelativePath[relativePath] = prevAsset;
                }
            }
        }

        // Match current assets to previous versions
        var outputPath = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(outputPath);

        var candidates = new List<ITaskItem>();
        var currentAssets = StaticWebAsset.FromTaskItemGroup(CurrentAssets);

        foreach (var currentAsset in currentAssets)
        {
            // Skip compressed assets
            if (!string.IsNullOrEmpty(currentAsset.AssetTraitName) &&
                string.Equals(currentAsset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = currentAsset.ComputePathWithoutTokens(currentAsset.RelativePath);
            if (string.IsNullOrEmpty(relativePath) || !previousByRelativePath.TryGetValue(relativePath, out var previousAsset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "No previous version found for asset '{0}' (RelativePath: '{1}').",
                    currentAsset.Identity,
                    relativePath);
                continue;
            }

            // Extract the previous asset file from the zip
            var assetEntryPath = "assets/" + relativePath.Replace('\\', '/');
            var zipEntry = archive.GetEntry(assetEntryPath);
            if (zipEntry == null)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Previous asset file '{0}' not found in pack for asset '{1}'. Skipping.",
                    assetEntryPath,
                    currentAsset.Identity);
                continue;
            }

            var extractedPath = Path.Combine(outputPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var extractedDir = Path.GetDirectoryName(extractedPath);
            if (!string.IsNullOrEmpty(extractedDir))
            {
                Directory.CreateDirectory(extractedDir);
            }

            using (var entryStream = zipEntry.Open())
            using (var fileStream = new FileStream(extractedPath, FileMode.Create, FileAccess.Write))
            {
                entryStream.CopyTo(fileStream);
            }

            // Read Integrity from the manifest (already SHA-256 base64)
            var integrity = previousAsset.Integrity;
            if (string.IsNullOrEmpty(integrity))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Previous asset '{0}' has no Integrity value in manifest. Skipping.",
                    relativePath);
                continue;
            }

            // Format as structured field for Available-Dictionary header: :<base64>:
            var dictionaryHash = ":" + integrity + ":";

            var candidate = new TaskItem(currentAsset.Identity);
            candidate.SetMetadata("DictionaryPath", extractedPath);
            candidate.SetMetadata("DictionaryHash", dictionaryHash);
            candidate.SetMetadata("RelativePath", relativePath);

            candidates.Add(candidate);

            Log.LogMessage(
                "Resolved dictionary candidate for '{0}': previous asset at '{1}', hash '{2}'.",
                currentAsset.Identity,
                extractedPath,
                dictionaryHash);
        }

        DictionaryCandidates = candidates.ToArray();

        Log.LogMessage(
            "Resolved {0} dictionary candidates from {1} current assets.",
            candidates.Count,
            currentAssets.Length);

        return !Log.HasLoggedErrors;
    }
}
