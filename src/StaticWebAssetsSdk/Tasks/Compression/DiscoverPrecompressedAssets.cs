// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class DiscoverPrecompressedAssets : Task
{
    private const string GzipAssetTraitValue = "gzip";
    private const string BrotliAssetTraitValue = "br";

    public ITaskItem[] CandidateAssets { get; set; }

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

        var candidates = CandidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray();
        var assetsToUpdate = new List<ITaskItem>();

        var candidatesByIdentity = new Dictionary<string, StaticWebAsset>(OSPath.PathComparer);

        foreach (var asset in candidates)
        {
            // Assets might contain duplicated keys, use the first occurance 
            if (!candidatesByIdentity.ContainsKey(asset.Identity))
            {
                candidatesByIdentity[asset.Identity] = asset;
            }
        }

        foreach (var candidate in candidates)
        {
            if (HasCompressionExtension(candidate.RelativePath) &&
                // We only care about assets that are not already considered compressed
                !IsCompressedAsset(candidate) &&
                // The candidate doesn't already have a related asset
                string.IsNullOrEmpty(candidate.RelatedAsset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "The asset '{0}' was detected as compressed but it didn't specify a related asset.",
                    candidate.Identity);
                var relatedAsset = FindRelatedAsset(candidate, candidatesByIdentity);
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
                UpdateCompressedAsset(candidate, relatedAsset);
                assetsToUpdate.Add(candidate.ToTaskItem());
            }
        }

        DiscoveredCompressedAssets = [.. assetsToUpdate];

        return !Log.HasLoggedErrors;
    }

    private static StaticWebAsset FindRelatedAsset(StaticWebAsset candidate, IDictionary<string, StaticWebAsset> candidates)
    {
        // The only pattern that we support is a related asset that lives in the same directory, with the same name,
        // but without the compression extension. In any other case we are not going to consider the assets related
        // and an error will occur.
        var identityWithoutExtension = candidate.Identity.Substring(0, candidate.Identity.Length - 3); // We take advantage we know the extension is .br or .gz.
        return candidates.TryGetValue(identityWithoutExtension, out var relatedAsset) ? relatedAsset : null;
    }

    private static bool HasCompressionExtension(string relativePath)
    {
        return relativePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".br", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompressedAsset(StaticWebAsset asset)
        => string.Equals("Content-Encoding", asset.AssetTraitName, StringComparison.Ordinal);

    private static void UpdateCompressedAsset(StaticWebAsset asset, StaticWebAsset relatedAsset)
    {
        string fileExtension;
        string assetTraitValue;

        if (!asset.RelativePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".br";
            assetTraitValue = BrotliAssetTraitValue;
        }
        else
        {
            fileExtension = ".gz";
            assetTraitValue = GzipAssetTraitValue;
        }

        var relativePath = relatedAsset.EmbedTokens(relatedAsset.RelativePath);

        asset.RelativePath = $"{relativePath}{fileExtension}";
        asset.OriginalItemSpec = relatedAsset.Identity;
        asset.RelatedAsset = relatedAsset.Identity;
        asset.AssetRole = "Alternative";
        asset.AssetTraitName = "Content-Encoding";
        asset.AssetTraitValue = assetTraitValue;
    }
}
