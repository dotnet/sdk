// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveCompressedAssets : Task
{
    private static readonly char[] PatternSeparator = [';'];

    public ITaskItem[] CandidateAssets { get; set; }

    public ITaskItem[] CompressionFormats { get; set; }

    // Semicolon-separated list of format names to compress assets into (e.g., "gzip" for build, "gzip;brotli" for publish).
    // Must be a subset of the names in CompressionFormats.
    public string Formats { get; set; }

    public string IncludePatterns { get; set; }

    public string ExcludePatterns { get; set; }

    public ITaskItem[] ExplicitAssets { get; set; }

    // Dictionary candidates produced by ResolveDictionaryCandidates.
    // Each item's Identity is the extracted dictionary bytes path.
    // Metadata: Hash (structured field), TargetAsset (new asset identity), MatchPattern.
    public ITaskItem[] DictionaryCandidates { get; set; }

    [Required]
    public string OutputPath { get; set; }

    [Output]
    public ITaskItem[] AssetsToCompress { get; set; }

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

        if (CompressionFormats is null || CompressionFormats.Length == 0)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no compression formats were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        var activeFormatNames = SplitPattern(Formats);

        if (activeFormatNames.Length == 0)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no active format names were specified in Formats.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        // Build lookup from content-encoding trait value → format name for reverse mapping of existing compressed assets.
        var formatsByContentEncoding = new Dictionary<string, string>(CompressionFormats.Length, StringComparer.OrdinalIgnoreCase);
        // Build lookup from format name → (extension, contentEncoding) for creating new compressed assets.
        var formatsByName = new Dictionary<string, CompressionFormatInfo>(CompressionFormats.Length, StringComparer.OrdinalIgnoreCase);
        var knownExtensions = new HashSet<string>(CompressionFormats.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var format in CompressionFormats)
        {
            var formatName = format.ItemSpec;
            var extension = format.GetMetadata("FileExtension");
            var contentEncoding = format.GetMetadata("ContentEncoding");
            var usesDictionary = string.Equals(format.GetMetadata("UsesDictionary"), "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentEncoding))
            {
                Log.LogError(
                    "Compression format '{0}' is missing required metadata. FileExtension='{1}', ContentEncoding='{2}'.",
                    formatName, extension, contentEncoding);
                return false;
            }

            if (formatsByName.ContainsKey(formatName))
            {
                Log.LogError("Duplicate compression format name '{0}'.", formatName);
                return false;
            }
            formatsByName[formatName] = new CompressionFormatInfo(extension, contentEncoding, usesDictionary);

            if (formatsByContentEncoding.ContainsKey(contentEncoding))
            {
                Log.LogError("Duplicate content-encoding '{0}' in compression format '{1}'.", contentEncoding, formatName);
                return false;
            }
            formatsByContentEncoding[contentEncoding] = formatName;

            if (!knownExtensions.Add(extension))
            {
                Log.LogError("Duplicate extension '{0}' in compression format '{1}'.", extension, formatName);
                return false;
            }
        }

        // Validate that all active format names have definitions in CompressionFormats.
        foreach (var name in activeFormatNames)
        {
            if (!formatsByName.ContainsKey(name))
            {
                Log.LogError("Active compression format '{0}' is not defined in CompressionFormats.", name);
                return false;
            }
        }

        var candidates = StaticWebAsset.FromTaskItemGroup(CandidateAssets).ToArray();
        var explicitAssets = ExplicitAssets == null ? [] : StaticWebAsset.FromTaskItemGroup(ExplicitAssets);
        var existingCompressionFormatsByAssetItemSpec = CollectCompressedAssets(candidates, formatsByContentEncoding);

        // Build dictionary candidate lookup: TargetAsset → candidate TaskItem
        var dictionaryCandidatesByIdentity = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
        if (DictionaryCandidates != null)
        {
            foreach (var candidate in DictionaryCandidates)
            {
                var targetAsset = candidate.GetMetadata("TargetAsset");
                if (!string.IsNullOrEmpty(targetAsset))
                {
                    dictionaryCandidatesByIdentity[targetAsset] = candidate;
                }
            }
        }

        var includePatterns = SplitPattern(IncludePatterns);
        var excludePatterns = SplitPattern(ExcludePatterns);

        var matcher = new StaticWebAssetGlobMatcherBuilder()
            .AddIncludePatterns(includePatterns)
            .AddExcludePatterns(excludePatterns)
            .Build();

        var matchingCandidateAssets = new List<StaticWebAsset>(CandidateAssets.Length);

        var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();

        // Add each candidate asset to each compression configuration with a matching pattern.
        foreach (var asset in candidates)
        {
            if (IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Ignoring asset '{0}' for compression because it is already compressed asset for '{1}'.",
                    asset.Identity,
                    asset.RelatedAsset);
                continue;
            }

            var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
            matchContext.SetPathAndReinitialize(relativePath.AsSpan());
            var match = matcher.Match(matchContext);

            if (!match.IsMatch)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset '{0}' with relative path '{1}' did not match include pattern '{2}' or matched exclude pattern '{3}'.",
                    asset.Identity,
                    relativePath,
                    IncludePatterns,
                    ExcludePatterns);
                continue;
            }

            Log.LogMessage(
                MessageImportance.Low,
                "Asset '{0}' with relative path '{1}' matched include pattern '{2}' and did not match exclude pattern '{3}'.",
                asset.Identity,
                relativePath,
                IncludePatterns,
                ExcludePatterns);
            matchingCandidateAssets.Add(asset);
        }

        // Consider each explicitly-provided asset to be a matching asset.
        matchingCandidateAssets.AddRange(explicitAssets);

        // Process the final set of candidate assets, deduplicating assets to be compressed in the same format multiple times and
        // generating new a static web asset definition for each compressed item.
        var assetsToCompress = new ITaskItem[matchingCandidateAssets.Count * activeFormatNames.Length];
        var outputPath = Path.GetFullPath(OutputPath);
        var assetCounter = 0;
        foreach (var asset in matchingCandidateAssets)
        {
            // Reset common properties
            StaticWebAsset previousAsset = null;
            string pathTemplate = null;
            string relativePath = null;

            foreach (var formatName in activeFormatNames)
            {
                var itemSpec = asset.Identity;
                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(itemSpec, out var existingFormats))
                {
                    existingFormats = new HashSet<string>(2, StringComparer.OrdinalIgnoreCase);
                    existingCompressionFormatsByAssetItemSpec.Add(itemSpec, existingFormats);
                }

                if (existingFormats.Contains(formatName))
                {
                    Log.LogMessage(
                        "Ignoring asset '{0}' because it was already resolved with format '{1}'.",
                        itemSpec,
                        formatName);
                    continue;
                }

                pathTemplate ??= CreatePathTemplate(asset, outputPath);
                relativePath ??= asset.EmbedTokens(asset.RelativePath);

                var format = formatsByName[formatName];

                // Dictionary-requiring formats (e.g. dcz) can only produce candidates when a
                // dictionary candidate exists for the asset.
                ITaskItem dictCandidate = null;
                if (format.UsesDictionary)
                {
                    if (!dictionaryCandidatesByIdentity.TryGetValue(asset.Identity, out dictCandidate))
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            "Skipping dictionary format '{0}' for asset '{1}' because no dictionary candidate is available.",
                            formatName,
                            asset.Identity);
                        continue;
                    }
                }

                var oldFileFingerprint = dictCandidate?.GetMetadata("OldFileFingerprint");

                if (TryCreateCompressedAsset(
                    asset,
                    outputPath,
                    format.Extension,
                    format.ContentEncoding,
                    pathTemplate,
                    relativePath,
                    dictCandidate?.ItemSpec,
                    oldFileFingerprint,
                    ref previousAsset,
                    out var compressedAsset))
                {
                    var result = compressedAsset.ToTaskItem();
                    result.SetMetadata("RelatedAssetOriginalItemSpec", asset.OriginalItemSpec);

                    // Propagate dictionary metadata for dictionary-requiring formats
                    if (dictCandidate != null)
                    {
                        result.SetMetadata("DictionaryPath", dictCandidate.ItemSpec);
                        result.SetMetadata("DictionaryHash", dictCandidate.GetMetadata("Hash"));
                    }

                    assetsToCompress[assetCounter++] = result;
                    existingFormats.Add(formatName);

                    Log.LogMessage(
                        "Accepted compressed asset '{0}' for '{1}'.",
                        result.ItemSpec,
                        itemSpec);
                }
                else
                {
                    Log.LogError(
                        "Could not create compressed asset for original asset '{0}'.",
                        itemSpec);
                }
            }
        }

        Log.LogMessage(
            "Resolved {0} compressed assets for {1} candidate assets.",
            assetCounter,
            matchingCandidateAssets.Count);

        AssetsToCompress = assetsToCompress;

        return !Log.HasLoggedErrors;
    }

    private static string CreatePathTemplate(StaticWebAsset asset, string outputPath)
    {
        var relativePath = asset.ComputePathWithoutTokens(asset.RelativePath);
        var pathHash = FileHasher.HashString(asset.SourceId + asset.BasePath + asset.AssetKind + asset.AssetGroups + relativePath);
        return Path.Combine(outputPath, $"{pathHash}-{{0}}-{asset.Fingerprint}");
    }

    private Dictionary<string, HashSet<string>> CollectCompressedAssets(
        StaticWebAsset[] candidates,
        Dictionary<string, string> formatsByContentEncoding)
    {
        // Scan the provided candidate assets and determine which ones have already been detected for compression and in which formats.
        var existingCompressionFormatsByAssetItemSpec = new Dictionary<string, HashSet<string>>();

        foreach (var asset in candidates)
        {
            if (!IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset '{0}' is not compressed.",
                    asset.Identity);
                continue;
            }
            var relatedAssetItemSpec = asset.RelatedAsset;

            if (string.IsNullOrEmpty(relatedAssetItemSpec))
            {
                Log.LogError(
                    "The asset '{0}' was detected as compressed but didn't specify a related asset.",
                    asset.Identity);
                continue;
            }

            if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(relatedAssetItemSpec, out var existingFormats))
            {
                existingFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                existingCompressionFormatsByAssetItemSpec.Add(relatedAssetItemSpec, existingFormats);
            }

            if (!formatsByContentEncoding.TryGetValue(asset.AssetTraitValue, out var assetFormat))
            {
                Log.LogError(
                    "The asset '{0}' has content-encoding '{1}' which does not match any configured compression format.",
                    asset.Identity,
                    asset.AssetTraitValue);
                continue;
            }

            Log.LogMessage(
                "The asset '{0}' with related asset '{1}' was detected as already compressed with format '{2}'.",
                asset.Identity,
                relatedAssetItemSpec,
                assetFormat);
            existingFormats.Add(assetFormat);
        }

        return existingCompressionFormatsByAssetItemSpec;
    }

    private static bool IsCompressedAsset(StaticWebAsset asset)
        => string.Equals("Content-Encoding", asset.AssetTraitName, StringComparison.Ordinal);

    private static string[] SplitPattern(string pattern)
        => string.IsNullOrEmpty(pattern) ? [] : pattern
            .Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

    private bool TryCreateCompressedAsset(
        StaticWebAsset asset,
        string outputPath,
        string fileExtension,
        string assetTraitValue,
        string pathTemplate,
        string relativePath,
        string dictionaryId,
        string oldFileFingerprint,
        ref StaticWebAsset previousAsset,
        out StaticWebAsset result)
    {
        result = null;

        // Make the hash name more unique by including source id, base path, asset kind and relative path.
        // This combination must be unique across all assets, so this will avoid collisions when two files on
        // the same project have the same contents, when it happens across different projects or between Build/Publish
        // assets.
        // When a dictionary is involved, include its identity in the path so that
        // different dictionaries for the same asset produce distinct output files.
        var dictSuffix = dictionaryId != null ? $"-{FileHasher.HashString(dictionaryId)}" : "";
        var fileName = $"{pathTemplate}-{asset.Fingerprint}{dictSuffix}{fileExtension}";
        var itemSpec = Path.GetFullPath(Path.Combine(OutputPath, fileName));

        // For dictionary-compressed formats, embed the old file's fingerprint in the RelativePath
        // so that multiple dictionaries for the same asset produce distinct served URLs.
        // Format: name.{oldFingerprint}.dcz (e.g., site.prevfp.dcz)
        var compressedRelativePath = !string.IsNullOrEmpty(oldFileFingerprint)
            ? $"{relativePath}.{oldFileFingerprint}{fileExtension}"
            : $"{relativePath}{fileExtension}";

        if (previousAsset != null)
        {
            result = new StaticWebAsset(previousAsset)
            {
                Identity = itemSpec,
                RelativePath = compressedRelativePath,
                AssetTraitValue = assetTraitValue,
            };
        }
        else
        {
            result = new StaticWebAsset(asset)
            {
                Identity = itemSpec,
                RelativePath = compressedRelativePath,
                OriginalItemSpec = asset.Identity,
                RelatedAsset = asset.Identity,
                AssetRole = "Alternative",
                AssetTraitName = "Content-Encoding",
                AssetTraitValue = assetTraitValue,
                ContentRoot = outputPath,
                // Set integrity and fingerprint to null so that they get recalculated for the compressed asset.
                Fingerprint = null,
                Integrity = null,
            };

            previousAsset = result;
        }

        return true;
    }

    internal readonly struct CompressionFormatInfo
    {
        public CompressionFormatInfo(string extension, string contentEncoding, bool usesDictionary)
        {
            Extension = extension;
            ContentEncoding = contentEncoding;
            UsesDictionary = usesDictionary;
        }

        public string Extension { get; }
        public string ContentEncoding { get; }
        public bool UsesDictionary { get; }
    }
}
