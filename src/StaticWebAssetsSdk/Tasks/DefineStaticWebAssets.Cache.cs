// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public partial class DefineStaticWebAssets : Task
    {
        private DefineStaticWebAssetsCache GetOrCreateAssetsCache()
        {
            var memoryStream = new MemoryStream();
            var properties = new[] {
                SourceId, SourceType, BasePath, ContentRoot, RelativePathPattern, RelativePathFilter,
                AssetKind, AssetMode, AssetRole, AssetMergeSource, AssetMergeBehavior, RelatedAsset,
                AssetTraitName, AssetTraitValue, CopyToOutputDirectory, CopyToPublishDirectory,
                FingerprintCandidates.ToString()
            };
            var propertiesHash = HashingUtils.ComputeHash(memoryStream, properties);

            var patternMetadata = new[] { nameof(FingerprintPattern.Pattern), nameof(FingerprintPattern.Expression) };
            var fingerprintPatternsHash = HashingUtils.ComputeHash(memoryStream, FingerprintPatterns ?? [], patternMetadata);

            var propertyOverridesHash = HashingUtils.ComputeHash(memoryStream, PropertyOverrides, nameof(ITaskItem.GetMetadata));

            var candidateAssetMetadata = new[] {
                "FullPath", "RelativePath", "TargetPath", "Link", "ModifiedTime", nameof(StaticWebAsset.SourceId),
                nameof(StaticWebAsset.SourceType), nameof(StaticWebAsset.BasePath), nameof(StaticWebAsset.ContentRoot),
                nameof(StaticWebAsset.AssetKind), nameof(StaticWebAsset.AssetMode), nameof(StaticWebAsset.AssetRole),
                nameof(StaticWebAsset.AssetMergeBehavior), nameof(StaticWebAsset.AssetMergeSource), nameof(StaticWebAsset.RelatedAsset),
                nameof(StaticWebAsset.AssetTraitName), nameof(StaticWebAsset.AssetTraitValue), nameof(StaticWebAsset.Fingerprint),
                nameof(StaticWebAsset.Integrity), nameof(StaticWebAsset.CopyToOutputDirectory), nameof(StaticWebAsset.CopyToPublishDirectory),
                nameof(StaticWebAsset.OriginalItemSpec)
            };
            var inputHashes = HashingUtils.ComputeHashLookup(memoryStream, CandidateAssets ?? [], candidateAssetMetadata);

            var assetsCache = DefineStaticWebAssetsCache.ReadOrCreateCache(Log, CacheManifestPath);
            assetsCache.PrepareForProcessing(propertiesHash, fingerprintPatternsHash, propertyOverridesHash, inputHashes);

            return assetsCache;
        }

        internal class DefineStaticWebAssetsCache
        {
            private readonly List<ITaskItem> _assets = [];
            private readonly List<ITaskItem> _copyCandidates = [];
            private string _manifestPath;
            private IDictionary<string, ITaskItem> _inputHashes;
            private bool _cacheUpToDate = false;
            private TaskLoggingHelper _log;

            public DefineStaticWebAssetsCache() { }

            internal DefineStaticWebAssetsCache(TaskLoggingHelper log, string manifestPath) : this()
                => (_log, _manifestPath) = (log, manifestPath);

            // Inputs for the cache
            public byte[] GlobalPropertiesHash { get; set; } = [];
            public byte[] FingerprintPatternsHash { get; set; } = [];
            public byte[] PropertyOverridesHash { get; set; } = [];
            public HashSet<string> InputHashes { get; set; } = [];

            // Outputs for the cache
            public Dictionary<string, StaticWebAsset> CachedAssets { get; set; } = [];
            public Dictionary<string, CopyCandidate> CachedCopyCandidates { get; set; } = [];

            internal static DefineStaticWebAssetsCache ReadOrCreateCache(TaskLoggingHelper log, string manifestPath)
            {
                if (manifestPath != null && File.Exists(manifestPath))
                {
                    using var existingManifestFile = File.OpenRead(manifestPath);
                    var cache = JsonSerializer.Deserialize(existingManifestFile, DefineStaticWebAssetsSerializerContext.Default.DefineStaticWebAssetsCache);
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
                if (_manifestPath != null)
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
                CachedAssets[hash] = asset;
            }

            internal void AppendCopyCandidate(string hash, string identity, string targetPath)
            {
                var copyCandidate = new CopyCandidate { Identity = identity, TargetPath = targetPath };
                _copyCandidates.Add(copyCandidate.ToTaskItem());
                CachedCopyCandidates[hash] = copyCandidate;
            }

            internal void PrepareForProcessing(
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


            internal void TotalUpdate(byte[] propertiesHash, byte[] fingerprintPatternsHash, byte[] propertyOverridesHash, IDictionary<string, ITaskItem> inputsByHash)
            {
                _log.LogMessage(MessageImportance.Low, "Updating cache completely.");
                GlobalPropertiesHash = propertiesHash;
                FingerprintPatternsHash = fingerprintPatternsHash;
                PropertyOverridesHash = propertyOverridesHash;
                InputHashes = [.. inputsByHash.Keys];
                _inputHashes = inputsByHash;
            }

            internal void PartialUpdate(Dictionary<string, ITaskItem> inputHashes)
            {
                var newHashes = new HashSet<string>(inputHashes.Keys);
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
                    _log.LogMessage(MessageImportance.Low, "Cache is fully up to date.");
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
                        _log.LogMessage(MessageImportance.Low, "Asset {0} is up to date", candidate.ItemSpec);
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

                _inputHashes = remainingCandidates;
            }

            internal void SetPathAndLogger(string manifestPath, TaskLoggingHelper log) => (_manifestPath, _log) = (manifestPath, log);

            public (IList<ITaskItem> CopyCandidates, IList<ITaskItem> Assets) ComputeOutputs() => (_copyCandidates, _assets);

            internal IEnumerable<KeyValuePair<string, ITaskItem>> OutOfDateInputs() => _cacheUpToDate ? [] : _inputHashes;

            internal bool IsUpToDate() => _cacheUpToDate;
        }

        internal class CopyCandidate
        {
            public string Identity { get; set; }
            public string TargetPath { get; set; }

            internal ITaskItem ToTaskItem() => new TaskItem(Identity, new Dictionary<string, string> { ["TargetPath"] = TargetPath });
        }

        [JsonSerializable(typeof(DefineStaticWebAssetsCache))]
        [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization, WriteIndented = false)]
        internal partial class DefineStaticWebAssetsSerializerContext : JsonSerializerContext { }
    }
}
