// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
            var propertiesHash = HashingUtils.ComputeHash(
                memoryStream,
                SourceId,
                SourceType,
                BasePath,
                ContentRoot,
                RelativePathPattern,
                RelativePathFilter,
                AssetKind,
                AssetMode,
                AssetRole,
                AssetMergeSource,
                AssetMergeBehavior,
                RelatedAsset,
                AssetTraitName,
                AssetTraitValue,
                CopyToOutputDirectory,
                CopyToPublishDirectory,
                FingerprintCandidates.ToString());

            var fingerprintPatternsHash = HashingUtils.ComputeHash(
                memoryStream,
                FingerprintPatterns ?? [],
                nameof(FingerprintPattern.Pattern),
                nameof(FingerprintPattern.Expression));

            var propertyOverridesHash = HashingUtils.ComputeHash(
                memoryStream,
                PropertyOverrides,
                nameof(ITaskItem.GetMetadata));

            var inputHashes = HashingUtils.ComputeHashLookup(
                memoryStream,
                CandidateAssets ?? [],
                "FullPath",
                "RelativePath",
                "TargetPath",
                "Link",
                "ModifiedTime",
                nameof(StaticWebAsset.SourceId),
                nameof(StaticWebAsset.SourceType),
                nameof(StaticWebAsset.BasePath),
                nameof(StaticWebAsset.ContentRoot),
                nameof(StaticWebAsset.AssetKind),
                nameof(StaticWebAsset.AssetMode),
                nameof(StaticWebAsset.AssetRole),
                nameof(StaticWebAsset.AssetMergeBehavior),
                nameof(StaticWebAsset.AssetMergeSource),
                nameof(StaticWebAsset.RelatedAsset),
                nameof(StaticWebAsset.AssetTraitName),
                nameof(StaticWebAsset.AssetTraitValue),
                nameof(StaticWebAsset.Fingerprint),
                nameof(StaticWebAsset.Integrity),
                nameof(StaticWebAsset.CopyToOutputDirectory),
                nameof(StaticWebAsset.CopyToPublishDirectory),
                nameof(StaticWebAsset.OriginalItemSpec));

            var assetsCache = DefineStaticWebAssetsCache.ReadOrCreateCache(Log, ManifestPath);
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

            internal DefineStaticWebAssetsCache(TaskLoggingHelper log) : this() => _log = log;

            // Inputs for the cache
            public byte[] GlobalPropertiesHash { get; set; } = [];
            public byte[] FingerprintPatternsHash { get; set; } = [];
            public byte[] PropertyOverridesHash { get; set; } = [];
            public HashSet<string> InputHashes { get; set; } = [];

            // Outputs for the cache
            public Dictionary<string, ITaskItem> CachedAssets { get; set; } = [];

            public Dictionary<string, ITaskItem> CachedCopyCandidates { get; set; } = [];

            internal static DefineStaticWebAssetsCache ReadOrCreateCache(TaskLoggingHelper log, string manifestPath)
            {
                if (manifestPath != null && File.Exists(manifestPath))
                {
                    using var existingManifestFile = File.OpenRead(manifestPath);
                    var cache = JsonSerializer.Deserialize<DefineStaticWebAssetsCache>(existingManifestFile);
                    cache.SetPathAndLogger(manifestPath, log);
                    return cache;
                }
                else
                {
                    return new DefineStaticWebAssetsCache(log);
                }
            }

            internal void SetPathAndLogger(string manifestPath, TaskLoggingHelper log)
            {
                _manifestPath = manifestPath;
                _log = log;
            }

            internal void WriteCacheManifest()
            {
                if (_manifestPath != null)
                {
                    using var manifestFile = File.OpenWrite(_manifestPath);
                    manifestFile.SetLength(0);
                    JsonSerializer.Serialize(manifestFile, this);
                }
            }

            public DefineStaticWebAssetsOutputs ComputeOutputs() => new()
            {
                CopyCandidates = _copyCandidates,
                Assets = _assets
            };

            internal void AppendAsset(string hash, ITaskItem taskItem)
            {
                _assets.Add(taskItem);
                CachedAssets[hash] = taskItem;
            }

            internal void AppendCopyCandidate(string hash, ITaskItem taskItem)
            {
                _copyCandidates.Add(taskItem);
                CachedCopyCandidates[hash] = taskItem;
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

            internal IEnumerable<KeyValuePair<string, ITaskItem>> RemainingInputs() => _cacheUpToDate ? [] : _inputHashes;

            internal void PartialUpdate(Dictionary<string, ITaskItem> inputHashes)
            {
                var newHashes = new HashSet<string>(inputHashes.Keys);
                var oldHashes = InputHashes;

                if (newHashes.SetEquals(oldHashes))
                {
                    // If all the input hashes match, then we can reuse all the results.
                    foreach (var cachedAsset in CachedAssets)
                    {
                        _assets.Add(cachedAsset.Value);
                    }
                    foreach (var cachedCopyCandidate in CachedCopyCandidates)
                    {
                        _copyCandidates.Add(cachedCopyCandidate.Value);
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
                    else if(CachedAssets.TryGetValue(hash, out var asset))
                    {
                        _log.LogMessage(MessageImportance.Low, "Asset {0} is up to date", candidate.ItemSpec);
                        _assets.Add(asset);
                        if (CachedCopyCandidates.TryGetValue(hash, out var copyCandidate))
                        {
                            _copyCandidates.Add(copyCandidate);
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

            internal bool IsUpToDate() => _cacheUpToDate;

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
        }
    }

    internal class DefineStaticWebAssetsOutputs
    {
        public List<ITaskItem> Assets { get; set; }
        public List<ITaskItem> CopyCandidates { get; set; }
    }
}
