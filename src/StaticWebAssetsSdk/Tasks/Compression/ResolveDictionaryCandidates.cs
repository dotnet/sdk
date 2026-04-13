// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Extracts a previous asset pack (.zip), reads the embedded SWA manifest, and matches
/// current endpoints to previous endpoints by Route. The output is a set of dictionary
/// candidates that downstream tasks use to produce dictionary-compressed (dcz) assets.
///
/// Dictionary items represent bytes + hash + applicability scope, not full assets:
///   Identity = path to extracted dictionary bytes on disk
///   Hash = structured field ":base64-sha256:" for Available-Dictionary header
///   TargetAsset = Identity of the new asset this dictionary applies to
///   MatchPattern = URL pattern for Use-As-Dictionary: match= header
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
    /// The current publish endpoints, used for route-based matching against previous endpoints.
    /// </summary>
    public ITaskItem[] CurrentEndpoints { get; set; }

    /// <summary>
    /// Directory where matched previous assets will be extracted.
    /// </summary>
    [Required]
    public string OutputPath { get; set; }

    /// <summary>
    /// Dictionary candidates: one per matched (new asset, old asset) pair.
    /// Metadata: Hash, TargetAsset, MatchPattern.
    /// ItemSpec = path to extracted dictionary bytes on disk.
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

        // Build lookup: previous asset Identity → previous asset (only uncompressed originals)
        var previousAssetsById = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var prevAsset in manifest.Assets)
        {
            // Skip compressed assets — we only want originals as dictionary sources
            if (!string.IsNullOrEmpty(prevAsset.AssetTraitName) &&
                string.Equals(prevAsset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }

            if (!previousAssetsById.ContainsKey(prevAsset.Identity))
            {
                previousAssetsById[prevAsset.Identity] = prevAsset;
            }
        }

        // Build route → old endpoint → old asset lookup from the pack manifest
        // old endpoint.Route → (old endpoint, old asset)
        var oldEndpointsByRoute = new Dictionary<string, (StaticWebAssetEndpoint Endpoint, StaticWebAsset Asset)>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Endpoints != null)
        {
            foreach (var oldEndpoint in manifest.Endpoints)
            {
                // Only consider endpoints for uncompressed assets (no Content-Encoding selector)
                if (HasContentEncodingSelector(oldEndpoint))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(oldEndpoint.AssetFile) &&
                    previousAssetsById.TryGetValue(oldEndpoint.AssetFile, out var oldAsset))
                {
                    // First match per route wins
                    if (!oldEndpointsByRoute.ContainsKey(oldEndpoint.Route))
                    {
                        oldEndpointsByRoute[oldEndpoint.Route] = (oldEndpoint, oldAsset);
                    }
                }
            }
        }

        // Build current asset Identity → current asset for reverse lookup
        var currentAssets = StaticWebAsset.FromTaskItemGroup(CurrentAssets);
        var currentAssetsById = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in currentAssets)
        {
            if (!string.IsNullOrEmpty(asset.AssetTraitName) &&
                string.Equals(asset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }
            if (!currentAssetsById.ContainsKey(asset.Identity))
            {
                currentAssetsById[asset.Identity] = asset;
            }
        }

        // Match by route: use current endpoints to find matching old endpoints
        var outputPath = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(outputPath);

        var candidates = new List<ITaskItem>();
        // Track which new assets we've already matched (avoid duplicates from multiple routes)
        var matchedNewAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (CurrentEndpoints != null && CurrentEndpoints.Length > 0 && oldEndpointsByRoute.Count > 0)
        {
            // Route-based matching via endpoints
            foreach (var endpointItem in CurrentEndpoints)
            {
                var newEndpoint = StaticWebAssetEndpoint.FromTaskItem(endpointItem);

                // Only consider non-compressed endpoints
                if (HasContentEncodingSelector(newEndpoint))
                {
                    continue;
                }

                if (!oldEndpointsByRoute.TryGetValue(newEndpoint.Route, out var oldMatch))
                {
                    continue;
                }

                // Find the current asset for this endpoint
                if (!currentAssetsById.TryGetValue(newEndpoint.AssetFile, out var newAsset))
                {
                    continue;
                }

                if (!matchedNewAssets.Add(newAsset.Identity))
                {
                    continue; // Already matched via another route
                }

                var oldAsset = oldMatch.Asset;
                var candidate = TryCreateCandidate(archive, oldAsset, newAsset, outputPath);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }
        else
        {
            // Fallback: match by RelativePath when endpoints are not available
            var previousByRelativePath = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (var prevAsset in previousAssetsById.Values)
            {
                var relativePath = prevAsset.ComputePathWithoutTokens(prevAsset.RelativePath);
                if (!string.IsNullOrEmpty(relativePath) && !previousByRelativePath.ContainsKey(relativePath))
                {
                    previousByRelativePath[relativePath] = prevAsset;
                }
            }

            foreach (var newAsset in currentAssetsById.Values)
            {
                var relativePath = newAsset.ComputePathWithoutTokens(newAsset.RelativePath);
                if (string.IsNullOrEmpty(relativePath) || !previousByRelativePath.TryGetValue(relativePath, out var oldAsset))
                {
                    continue;
                }

                var candidate = TryCreateCandidate(archive, oldAsset, newAsset, outputPath);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        DictionaryCandidates = candidates.ToArray();

        Log.LogMessage(
            "Resolved {0} dictionary candidates from {1} current assets.",
            candidates.Count,
            currentAssets.Length);

        return !Log.HasLoggedErrors;
    }

    private ITaskItem TryCreateCandidate(ZipArchive archive, StaticWebAsset oldAsset, StaticWebAsset newAsset, string outputPath)
    {
        // Read Integrity from the manifest (already SHA-256 base64)
        var integrity = oldAsset.Integrity;
        if (string.IsNullOrEmpty(integrity))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Previous asset '{0}' has no Integrity value in manifest. Skipping.",
                oldAsset.Identity);
            return null;
        }

        // Skip when old and new asset have the same content — using the old version as a
        // dictionary for an identical file is pointless.
        if (!string.IsNullOrEmpty(newAsset.Integrity) &&
            string.Equals(integrity, newAsset.Integrity, StringComparison.Ordinal))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Previous asset '{0}' has the same integrity as current asset '{1}'. Skipping dictionary candidate.",
                oldAsset.Identity,
                newAsset.Identity);
            return null;
        }

        // Find the file in the pack using RelativePath
        var relativePath = oldAsset.ComputePathWithoutTokens(oldAsset.RelativePath);
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var assetEntryPath = "assets/" + relativePath.Replace('\\', '/');
        var zipEntry = archive.GetEntry(assetEntryPath);
        if (zipEntry == null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Previous asset file '{0}' not found in pack for asset '{1}'. Skipping.",
                assetEntryPath,
                newAsset.Identity);
            return null;
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

        // Format as structured field for Available-Dictionary header: :<base64>:
        var hash = ":" + integrity + ":";

        // Compute match pattern from old asset's RelativePath (converts fingerprint tokens to wildcards)
        var matchPattern = oldAsset.ComputeMatchPattern(oldAsset.RelativePath);
        if (string.IsNullOrEmpty(matchPattern))
        {
            matchPattern = relativePath;
        }

        // Dictionary-centric item: Identity = extracted bytes path
        var candidate = new TaskItem(extractedPath);
        candidate.SetMetadata("Hash", hash);
        candidate.SetMetadata("TargetAsset", newAsset.Identity);
        candidate.SetMetadata("MatchPattern", matchPattern);

        Log.LogMessage(
            "Resolved dictionary candidate: target='{0}', dictionary='{1}', hash='{2}', match='{3}'.",
            newAsset.Identity,
            extractedPath,
            hash,
            matchPattern);

        return candidate;
    }

    private static bool HasContentEncodingSelector(StaticWebAssetEndpoint endpoint)
    {
        if (endpoint.Selectors != null)
        {
            for (var i = 0; i < endpoint.Selectors.Length; i++)
            {
                if (string.Equals(endpoint.Selectors[i].Name, "Content-Encoding", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
