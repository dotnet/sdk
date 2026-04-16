// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Extracts a previous asset pack (.zip), reads the embedded SWA manifest, and matches
// current endpoints to previous endpoints by Route. The output is a set of dictionary
// candidates that downstream tasks use to produce dictionary-compressed (dcz) assets.
//
// Dictionary items represent bytes + hash + applicability scope, not full assets:
//   Identity = path to extracted dictionary bytes on disk
//   Hash = structured field ":base64-sha256:" for Available-Dictionary header
//   TargetAsset = Identity of the new asset this dictionary applies to
//   MatchPattern = URL pattern for Use-As-Dictionary: match= header
public class ResolveDictionaryCandidates : Task
{
    [Required]
    public string AssetPackPath { get; set; }

    [Required]
    public ITaskItem[] CurrentAssets { get; set; }

    // Current publish endpoints, used for route-based matching against previous endpoints.
    public ITaskItem[] CurrentEndpoints { get; set; }

    [Required]
    public string OutputPath { get; set; }

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

        var manifest = LoadManifest(archive);
        if (manifest == null)
        {
            return !Log.HasLoggedErrors;
        }

        if (manifest.Assets == null || manifest.Assets.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "Previous asset pack manifest contains no assets.");
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

        if (CurrentEndpoints == null || CurrentEndpoints.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No current endpoints provided. Skipping dictionary candidate resolution.");
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

        var oldEndpointsByRoute = BuildPreviousAssetLookups(manifest);
        if (oldEndpointsByRoute.Count == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No previous endpoints found in asset pack. Skipping dictionary candidate resolution.");
            DictionaryCandidates = Array.Empty<ITaskItem>();
            return true;
        }

        var currentAssetsById = BuildCurrentAssetsLookup();

        var outputPath = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(outputPath);

        var candidates = MatchByRoute(archive, currentAssetsById, oldEndpointsByRoute, outputPath);

        DictionaryCandidates = candidates.ToArray();

        Log.LogMessage(
            "Resolved {0} dictionary candidates from {1} current assets.",
            candidates.Count,
            currentAssetsById.Count);

        return !Log.HasLoggedErrors;
    }

    private StaticWebAssetsManifest LoadManifest(ZipArchive archive)
    {
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null)
        {
            Log.LogError("Asset pack '{0}' does not contain a manifest.json entry.", AssetPackPath);
            return null;
        }

        using var manifestStream = manifestEntry.Open();
        using var memoryStream = new MemoryStream();
        manifestStream.CopyTo(memoryStream);
        return StaticWebAssetsManifest.FromJsonBytes(memoryStream.ToArray());
    }

    private static Dictionary<string, PreviousRouteMatch> BuildPreviousAssetLookups(StaticWebAssetsManifest manifest)
    {
        var previousAssetsById = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var prevAsset in manifest.Assets)
        {
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

        var oldEndpointsByRoute = new Dictionary<string, PreviousRouteMatch>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Endpoints != null)
        {
            foreach (var oldEndpoint in manifest.Endpoints)
            {
                if (HasContentEncodingSelector(oldEndpoint))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(oldEndpoint.AssetFile) &&
                    previousAssetsById.TryGetValue(oldEndpoint.AssetFile, out var oldAsset))
                {
                    if (!oldEndpointsByRoute.ContainsKey(oldEndpoint.Route))
                    {
                        oldEndpointsByRoute[oldEndpoint.Route] = new PreviousRouteMatch(oldAsset);
                    }
                }
            }
        }

        return oldEndpointsByRoute;
    }

    private Dictionary<string, StaticWebAsset> BuildCurrentAssetsLookup()
    {
        var currentAssets = StaticWebAsset.FromTaskItemGroup(CurrentAssets);
        var currentAssetsById = new Dictionary<string, StaticWebAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in currentAssets)
        {
            if (!string.IsNullOrEmpty(asset.AssetTraitName) &&
                string.Equals(asset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(asset.AssetKind, "Build", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!currentAssetsById.ContainsKey(asset.Identity))
            {
                currentAssetsById[asset.Identity] = asset;
            }
        }

        return currentAssetsById;
    }

    private List<ITaskItem> MatchByRoute(
        ZipArchive archive,
        Dictionary<string, StaticWebAsset> currentAssetsById,
        Dictionary<string, PreviousRouteMatch> oldEndpointsByRoute,
        string outputPath)
    {
        var candidates = new List<ITaskItem>();
        var matchedNewAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpointItem in CurrentEndpoints)
        {
            var newEndpoint = StaticWebAssetEndpoint.FromTaskItem(endpointItem);

            if (HasContentEncodingSelector(newEndpoint))
            {
                continue;
            }

            if (!oldEndpointsByRoute.TryGetValue(newEndpoint.Route, out var oldMatch))
            {
                continue;
            }

            if (!currentAssetsById.TryGetValue(newEndpoint.AssetFile, out var newAsset))
            {
                continue;
            }

            if (!matchedNewAssets.Add(newAsset.Identity))
            {
                continue;
            }

            var candidate = TryCreateCandidate(archive, oldMatch.Asset, newAsset, outputPath);
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private ITaskItem TryCreateCandidate(ZipArchive archive, StaticWebAsset oldAsset, StaticWebAsset newAsset, string outputPath)
    {
        var integrity = oldAsset.Integrity;
        if (string.IsNullOrEmpty(integrity))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Previous asset '{0}' has no Integrity value in manifest. Skipping.",
                oldAsset.Identity);
            return null;
        }

        // Using the old version as a dictionary for an identical file is pointless.
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

        // Pack key uses BasePath + RelativePath (matching GeneratePublishAssetPack layout)
        var relativePath = oldAsset.ComputePathWithoutTokens(oldAsset.RelativePath);
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var basePath = oldAsset.BasePath ?? "";
        if (basePath.StartsWith("/", StringComparison.Ordinal))
        {
            basePath = basePath.Substring(1);
        }

        var assetEntryPath = string.IsNullOrEmpty(basePath)
            ? "assets/" + relativePath.Replace('\\', '/')
            : "assets/" + basePath.Replace('\\', '/') + "/" + relativePath.Replace('\\', '/');
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

        var extractedRelativePath = string.IsNullOrEmpty(basePath)
            ? relativePath
            : basePath + "/" + relativePath;
        var extractedPath = Path.Combine(outputPath, extractedRelativePath.Replace('/', Path.DirectorySeparatorChar));
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

        var hash = ":" + integrity + ":";

        // The browser sees full routes (BasePath/RelativePath), so the match pattern
        // must include BasePath to correctly scope dictionary applicability.
        var matchPattern = oldAsset.ComputeMatchPattern(oldAsset.RelativePath);
        if (string.IsNullOrEmpty(matchPattern))
        {
            matchPattern = relativePath;
        }

        if (!string.IsNullOrEmpty(basePath))
        {
            matchPattern = basePath + "/" + matchPattern;
        }

        if (!matchPattern.StartsWith("/", StringComparison.Ordinal))
        {
            matchPattern = "/" + matchPattern;
        }

        var candidate = new TaskItem(extractedPath);
        candidate.SetMetadata("Hash", hash);
        candidate.SetMetadata("TargetAsset", newAsset.Identity);
        candidate.SetMetadata("MatchPattern", matchPattern);
        candidate.SetMetadata("OldFileFingerprint", oldAsset.Fingerprint ?? "");

        Log.LogMessage(
            "Resolved dictionary candidate: target='{0}', dictionary='{1}', hash='{2}', match='{3}', fingerprint='{4}'.",
            newAsset.Identity,
            extractedPath,
            hash,
            matchPattern,
            oldAsset.Fingerprint);

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

    private readonly struct PreviousRouteMatch
    {
        public PreviousRouteMatch(StaticWebAsset asset)
        {
            Asset = asset;
        }

        public StaticWebAsset Asset { get; }
    }
}
