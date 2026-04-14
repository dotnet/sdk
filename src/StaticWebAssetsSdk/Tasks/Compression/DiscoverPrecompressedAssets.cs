// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class DiscoverPrecompressedAssets : Task
{
    public ITaskItem[] CandidateAssets { get; set; }

    // The compression formats to recognize. Each item's Identity is the format name.
    // Required metadata: FileExtension (e.g., ".gz"), ContentEncoding (e.g., "gzip", "br").
    public ITaskItem[] CompressionFormats { get; set; }

    [Output]
    public ITaskItem[] DiscoveredCompressedAssets { get; set; }

    public override bool Execute()
    {
        if (CandidateAssets is null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no candidate assets for compression were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        // Build extension → contentEncoding lookup from CompressionFormats items, sorted by descending extension length for longest-match.
        var formatsByExtension = BuildFormatsByExtension();
        if (formatsByExtension is null)
        {
            // Error already logged
            return false;
        }

        var candidates = StaticWebAsset.FromTaskItemGroup(CandidateAssets);
        var assetsToUpdate = new List<ITaskItem>();

        var candidatesByIdentity = candidates.ToDictionary(asset => asset.Identity, OSPath.PathComparer);

        foreach (var candidate in candidates)
        {
            if (TryGetCompressionExtension(candidate.RelativePath, formatsByExtension, out var matchedExtension, out var contentEncoding) &&
                // We only care about assets that are not already considered compressed
                !IsCompressedAsset(candidate) &&
                // The candidate doesn't already have a related asset
                string.IsNullOrEmpty(candidate.RelatedAsset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "The asset '{0}' was detected as compressed but it didn't specify a related asset.",
                    candidate.Identity);
                var relatedAsset = FindRelatedAsset(candidate, candidatesByIdentity, matchedExtension);
                if (relatedAsset is null)
                {
                    Log.LogMessage(
                        MessageImportance.Low,
                        "The asset '{0}' was detected as compressed but the related asset with relative path '{1}' was not found.",
                        candidate.Identity,
                        Path.GetFileNameWithoutExtension(candidate.RelativePath));
                    continue;
                }

                Log.LogMessage(
                    "The asset '{0}' was detected as compressed and the related asset '{1}' was found.",
                    candidate.Identity,
                    relatedAsset.Identity);
                UpdateCompressedAsset(candidate, relatedAsset, matchedExtension, contentEncoding);
                assetsToUpdate.Add(candidate.ToTaskItem());
            }
        }

        DiscoveredCompressedAssets = [.. assetsToUpdate];

        return !Log.HasLoggedErrors;
    }

    private List<(string Extension, string ContentEncoding)> BuildFormatsByExtension()
    {
        if (CompressionFormats is null || CompressionFormats.Length == 0)
        {
            return [];
        }

        var formats = new List<(string Extension, string ContentEncoding)>(CompressionFormats.Length);
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in CompressionFormats)
        {
            var extension = format.GetMetadata("FileExtension");
            var contentEncoding = format.GetMetadata("ContentEncoding");

            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentEncoding))
            {
                Log.LogError(
                    "Compression format '{0}' is missing required metadata. Extension='{1}', ContentEncoding='{2}'.",
                    format.ItemSpec, extension, contentEncoding);
                return null;
            }

            if (!seenExtensions.Add(extension))
            {
                Log.LogError(
                    "Duplicate file extension '{0}' found in CompressionFormats (format '{1}'). Each extension must be unique.",
                    extension, format.ItemSpec);
                return null;
            }

            formats.Add((extension, contentEncoding));
        }

        // Sort by descending extension length for longest-match-first semantics.
        formats.Sort((a, b) => b.Extension.Length.CompareTo(a.Extension.Length));
        return formats;
    }

    private static StaticWebAsset FindRelatedAsset(
        StaticWebAsset candidate,
        IDictionary<string, StaticWebAsset> candidates,
        string matchedExtension)
    {
        // The only pattern that we support is a related asset that lives in the same directory, with the same name,
        // but without the compression extension. In any other case we are not going to consider the assets related
        // and an error will occur.
        var identityWithoutExtension = candidate.Identity.Substring(0, candidate.Identity.Length - matchedExtension.Length);
        return candidates.TryGetValue(identityWithoutExtension, out var relatedAsset) ? relatedAsset : null;
    }

    private static bool TryGetCompressionExtension(
        string relativePath,
        List<(string Extension, string ContentEncoding)> formatsByExtension,
        out string matchedExtension,
        out string contentEncoding)
    {
        foreach (var (ext, encoding) in formatsByExtension)
        {
            if (relativePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                matchedExtension = ext;
                contentEncoding = encoding;
                return true;
            }
        }

        matchedExtension = null;
        contentEncoding = null;
        return false;
    }

    private static bool IsCompressedAsset(StaticWebAsset asset)
        => string.Equals("Content-Encoding", asset.AssetTraitName, StringComparison.Ordinal);

    private static void UpdateCompressedAsset(StaticWebAsset asset, StaticWebAsset relatedAsset, string fileExtension, string assetTraitValue)
    {
        var relativePath = relatedAsset.EmbedTokens(relatedAsset.RelativePath);

        asset.RelativePath = $"{relativePath}{fileExtension}";
        asset.OriginalItemSpec = relatedAsset.Identity;
        asset.RelatedAsset = relatedAsset.Identity;
        asset.AssetRole = "Alternative";
        asset.AssetTraitName = "Content-Encoding";
        asset.AssetTraitValue = assetTraitValue;
    }
}
