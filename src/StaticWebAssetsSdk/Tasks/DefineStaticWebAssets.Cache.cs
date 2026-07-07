// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using static Microsoft.AspNetCore.StaticWebAssets.Tasks.FingerprintPatternMatcher;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public partial class DefineStaticWebAssets : Task
{
    private DefineStaticWebAssetsCache GetOrCreateAssetsCache()
    {
        // Absolutize the cache manifest path relative to the project directory so file I/O in the cache
        // does not depend on the process working directory (required for multithreaded task execution).
        // Treat null/empty/whitespace uniformly as "no cache" so we never call GetAbsolutePath on a blank
        // value (which throws) nor fall through to File.OpenWrite("") when persisting the manifest.
        var cacheManifestPath = string.IsNullOrWhiteSpace(CacheManifestPath)
            ? null
            : TaskEnvironment.GetAbsolutePath(CacheManifestPath).Value;
        var assetsCache = DefineStaticWebAssetsCache.ReadOrCreateCache(Log, cacheManifestPath);
        if (cacheManifestPath == null)
        {
            assetsCache.NoCache(CandidateAssets);
            return assetsCache;
        }

        var memoryStream = new MemoryStream();
#if NET9_0_OR_GREATER
        Span<string> properties = [
#else
        var properties = new string[] {
#endif
        SourceId, SourceType, BasePath, ContentRoot, RelativePathPattern, RelativePathFilter,
            AssetKind, AssetMode, AssetRole, AssetMergeSource, AssetMergeBehavior, RelatedAsset,
            AssetTraitName, AssetTraitValue, CopyToOutputDirectory, CopyToPublishDirectory,
            FingerprintCandidates.ToString()
#if NET9_0_OR_GREATER
        ];
#else
        };
#endif
        var propertiesHash = HashingUtils.ComputeHash(memoryStream, properties);

        var patternMetadata = new[] { nameof(FingerprintPattern.Pattern), nameof(FingerprintPattern.Expression) };
        var fingerprintPatternsHash = HashingUtils.ComputeHash(memoryStream, FingerprintPatterns ?? [], patternMetadata);

        var propertyOverridesHash = HashingUtils.ComputeHash(memoryStream, PropertyOverrides ?? []);

#if NET9_0_OR_GREATER
        Span<string> candidateAssetMetadata = [
#else
        var candidateAssetMetadata = new[] {
#endif
            "RelativePath", "TargetPath", "Link", nameof(StaticWebAsset.SourceId),
            nameof(StaticWebAsset.SourceType), nameof(StaticWebAsset.BasePath), nameof(StaticWebAsset.ContentRoot),
            nameof(StaticWebAsset.AssetKind), nameof(StaticWebAsset.AssetMode), nameof(StaticWebAsset.AssetRole),
            nameof(StaticWebAsset.AssetMergeBehavior), nameof(StaticWebAsset.AssetMergeSource), nameof(StaticWebAsset.RelatedAsset),
            nameof(StaticWebAsset.AssetTraitName), nameof(StaticWebAsset.AssetTraitValue), nameof(StaticWebAsset.Fingerprint),
            nameof(StaticWebAsset.Integrity), nameof(StaticWebAsset.CopyToOutputDirectory), nameof(StaticWebAsset.CopyToPublishDirectory),
            nameof(StaticWebAsset.OriginalItemSpec)
#if NET9_0_OR_GREATER
        ];
#else
        };
#endif
        var inputHashes = ComputeInputHashes(memoryStream, candidateAssetMetadata);

        assetsCache.Update(propertiesHash, fingerprintPatternsHash, propertyOverridesHash, inputHashes);

        return assetsCache;
    }

    private Dictionary<string, ITaskItem> ComputeInputHashes(
        MemoryStream memoryStream,
#if NET9_0_OR_GREATER
        Span<string> candidateAssetMetadata)
#else
        string[] candidateAssetMetadata)
#endif
    {
        var candidateAssets = CandidateAssets ?? [];
        var inputHashes = new Dictionary<string, ITaskItem>(candidateAssets.Length);
        for (var i = 0; i < candidateAssets.Length; i++)
        {
            var candidate = candidateAssets[i];
            var candidateFullPath = Path.GetFullPath(TaskEnvironment.GetAbsolutePath(candidate.ItemSpec));
            var file = new FileInfo(candidateFullPath);
            var originalItemSpecFullPath = "";
            if (!file.Exists)
            {
                var originalItemSpec = candidate.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec));
                if (!string.IsNullOrEmpty(originalItemSpec))
                {
                    originalItemSpecFullPath = Path.GetFullPath(TaskEnvironment.GetAbsolutePath(originalItemSpec));
                    file = new FileInfo(originalItemSpecFullPath);
                }
            }

            var values = new string[candidateAssetMetadata.Length + 5];
            values[0] = candidate.ItemSpec;
            values[1] = candidateFullPath;
            values[2] = originalItemSpecFullPath;
            values[3] = file.Exists ? file.Length.ToString(CultureInfo.InvariantCulture) : "";
            values[4] = file.Exists ? file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) : "";
            for (var j = 0; j < candidateAssetMetadata.Length; j++)
            {
                values[j + 5] = candidate.GetMetadata(candidateAssetMetadata[j]);
            }

            inputHashes.Add(Convert.ToBase64String(HashingUtils.ComputeHash(memoryStream, values)), candidate);
        }

        return inputHashes;
    }

    internal class DefineStaticWebAssetsCache
    {
        private readonly List<ITaskItem> _assets = [];
        private readonly List<ITaskItem> _copyCandidates = [];
        private string? _manifestPath;
        private IDictionary<string, ITaskItem>? _inputByHash;
        private ITaskItem[]? _noCacheCandidates;
        private bool _cacheUpToDate;
        private TaskLoggingHelper? _log;

        public DefineStaticWebAssetsCache() { }

        internal DefineStaticWebAssetsCache(TaskLoggingHelper log, string? manifestPath) : this()
            => SetPathAndLogger(manifestPath, log);

        // Inputs for the cache
        public byte[] GlobalPropertiesHash { get; set; } = [];
        public byte[] FingerprintPatternsHash { get; set; } = [];
        public byte[] PropertyOverridesHash { get; set; } = [];
        public HashSet<string> InputHashes { get; set; } = [];

        // Outputs for the cache
        public Dictionary<string, StaticWebAsset> CachedAssets { get; set; } = [];
        public Dictionary<string, CopyCandidate> CachedCopyCandidates { get; set; } = [];

        internal static DefineStaticWebAssetsCache ReadOrCreateCache(TaskLoggingHelper log, string? manifestPath)
        {
            if (manifestPath != null && File.Exists(manifestPath))
            {
                using var existingManifestFile = File.OpenRead(manifestPath);
                var cache = JsonSerializer.Deserialize(existingManifestFile, DefineStaticWebAssetsSerializerContext.Default.DefineStaticWebAssetsCache);
                if (cache == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize cache from {manifestPath}");
                }
                cache.SetPathAndLogger(manifestPath, log);
                return cache;
            }
            else
            {
                return new DefineStaticWebAssetsCache(log, manifestPath);
            }
        }

        internal void WriteCacheManifest()
        {
            if (_manifestPath != null && !_cacheUpToDate && InputHashes.Count > 0)
            {
                using var manifestFile = File.OpenWrite(_manifestPath);
                manifestFile.SetLength(0);
                JsonSerializer.Serialize(manifestFile, this, DefineStaticWebAssetsSerializerContext.Default.DefineStaticWebAssetsCache);
            }
        }

        internal void AppendAsset(string hash, StaticWebAsset asset, ITaskItem item)
        {
            asset.AssetKind = item.GetMetadata(nameof(StaticWebAsset.AssetKind));
            _assets.Add(item);
            if (!string.IsNullOrEmpty(hash))
            {
                CachedAssets[hash] = asset;
            }
        }

        internal void AppendCopyCandidate(string hash, string identity, string targetPath)
        {
            var copyCandidate = new CopyCandidate(identity, targetPath);
            _copyCandidates.Add(copyCandidate.ToTaskItem());
            if (!string.IsNullOrEmpty(hash))
            {
                CachedCopyCandidates[hash] = copyCandidate;
            }
        }

        internal void Update(
            byte[] propertiesHash,
            byte[] fingerprintPatternsHash,
            byte[] propertyOverridesHash,
            Dictionary<string, ITaskItem> inputHashes)
        {
            if (!propertiesHash.SequenceEqual(GlobalPropertiesHash) ||
                !fingerprintPatternsHash.SequenceEqual(FingerprintPatternsHash) ||
                !propertyOverridesHash.SequenceEqual(PropertyOverridesHash))
            {
                TotalUpdate(propertiesHash, fingerprintPatternsHash, propertyOverridesHash, inputHashes);
            }
            else
            {
                PartialUpdate(inputHashes);
            }
        }

        private void TotalUpdate(byte[] propertiesHash, byte[] fingerprintPatternsHash, byte[] propertyOverridesHash, IDictionary<string, ITaskItem> inputsByHash)
        {
            _log?.LogMessage(MessageImportance.Low, "Updating cache completely.");
            GlobalPropertiesHash = propertiesHash;
            FingerprintPatternsHash = fingerprintPatternsHash;
            PropertyOverridesHash = propertyOverridesHash;
            CachedAssets.Clear();
            CachedCopyCandidates.Clear();
            InputHashes = [.. inputsByHash.Keys];
            _inputByHash = inputsByHash;
        }

        private void PartialUpdate(Dictionary<string, ITaskItem> inputHashes)
        {
            var newHashes = new HashSet<string>(inputHashes.Count);
            foreach (var kvp in inputHashes)
            {
                var hash = kvp.Key;
                newHashes.Add(hash);
            }

            var oldHashes = InputHashes;

            if (newHashes.SetEquals(oldHashes))
            {
                // If all the input hashes match, then we can reuse all the results.
                foreach (var cachedAsset in CachedAssets)
                {
                    _assets.Add(cachedAsset.Value.ToTaskItem());
                }
                foreach (var cachedCopyCandidate in CachedCopyCandidates)
                {
                    _copyCandidates.Add(cachedCopyCandidate.Value.ToTaskItem());
                }

                _cacheUpToDate = true;
                _log?.LogMessage(MessageImportance.Low, "Cache is fully up to date.");
                return;
            }

            var remainingCandidates = new Dictionary<string, ITaskItem>();
            foreach (var kvp in inputHashes)
            {
                var candidate = kvp.Value;
                var hash = kvp.Key;
                if (!oldHashes.Contains(hash))
                {
                    remainingCandidates.Add(hash, candidate);
                }
                else if (CachedAssets.TryGetValue(hash, out var asset))
                {
                    _log?.LogMessage(MessageImportance.Low, "Asset {0} is up to date", candidate.ItemSpec);
                    _assets.Add(asset.ToTaskItem());
                    if (CachedCopyCandidates.TryGetValue(hash, out var copyCandidate))
                    {
                        _copyCandidates.Add(copyCandidate.ToTaskItem());
                    }
                }
            }

            // Remove any assets that are no longer in the input set
            InputHashes = newHashes;
            var assetsToRemove = oldHashes.Except(InputHashes);
            foreach (var hash in assetsToRemove)
            {
                CachedAssets.Remove(hash);
                CachedCopyCandidates.Remove(hash);
            }

            _inputByHash = remainingCandidates;
        }

        internal void SetPathAndLogger(string? manifestPath, TaskLoggingHelper log) => (_manifestPath, _log) = (manifestPath, log);

        public (IList<ITaskItem> CopyCandidates, IList<ITaskItem> Assets) GetComputedOutputs() => (_copyCandidates, _assets);

        internal void NoCache(ITaskItem[] candidateAssets)
        {
            _log?.LogMessage(MessageImportance.Low, "No cache manifest path specified. Cache will not be used.");
            _cacheUpToDate = false;
            _noCacheCandidates = candidateAssets;
        }

        internal IEnumerable<KeyValuePair<string, ITaskItem>> OutOfDateInputs()
        {
            if (_noCacheCandidates != null)
            {
                return EnumerateNoCache();
            }

            return _cacheUpToDate || _inputByHash == null ? [] : _inputByHash;

            IEnumerable<KeyValuePair<string, ITaskItem>> EnumerateNoCache()
            {
                foreach (var candidate in _noCacheCandidates)
                {
                    var hash = "";
                    yield return new KeyValuePair<string, ITaskItem>(hash, candidate);
                }
            }
        }

        internal bool IsUpToDate() => _cacheUpToDate;
    }

    internal class CopyCandidate(string identity, string targetPath)
    {
        public string Identity { get; set; } = identity;
        public string TargetPath { get; set; } = targetPath;

        internal ITaskItem ToTaskItem() => new TaskItem(Identity, new Dictionary<string, string> { ["TargetPath"] = TargetPath });
    }

    [JsonSerializable(typeof(DefineStaticWebAssetsCache))]
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
        WriteIndented = false)]
    internal partial class DefineStaticWebAssetsSerializerContext : JsonSerializerContext { }
}
