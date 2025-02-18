// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            {
                _log = log;
                _manifestPath = manifestPath;
            }

            // Inputs for the cache
            public byte[] GlobalPropertiesHash { get; set; } = [];
            public byte[] FingerprintPatternsHash { get; set; } = [];
            public byte[] PropertyOverridesHash { get; set; } = [];
            public HashSet<string> InputHashes { get; set; } = [];

            // Outputs for the cache
            [JsonConverter(typeof(TaskItemDictionaryConverter))]
            public Dictionary<string, ITaskItem> CachedAssets { get; set; } = [];

            [JsonConverter(typeof(TaskItemDictionaryConverter))]
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
                    return new DefineStaticWebAssetsCache(log, manifestPath);
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

            public (IList<ITaskItem> CopyCandidates, IList<ITaskItem> Assets) ComputeOutputs() => (_copyCandidates, _assets);

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
                    else if (CachedAssets.TryGetValue(hash, out var asset))
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

        [JsonSerializable(typeof(DefineStaticWebAssetsCache), GenerationMode = JsonSourceGenerationMode.Serialization)]
        internal partial class DefineStaticWebAssetsSerializerContext : JsonSerializerContext
        {
        }

        internal class TaskItemDictionaryConverter : JsonConverter<Dictionary<string, ITaskItem>>
        {
            public override Dictionary<string, ITaskItem> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // We need to read an ojbect where all the properties are entries to a dictionary. Each entry is a TaskItem where
                // the ItemSpec is the "Identity" property and the rest of the properties are metadata.
                Dictionary<string, ITaskItem> result = [];

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException();
                    }
                    var key = reader.GetString();
                    ReadTaskItem(ref reader, result, key);
                }

                return result;
            }

            private void ReadTaskItem(ref Utf8JsonReader reader, Dictionary<string, ITaskItem> result, string key)
            {
                // Read the TaskItem as a dictionary with the "Identity" property as the ItemSpec
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }
                var itemSpec = "";
                var metadata = new Dictionary<string, string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException();
                    }
                    var propertyName = reader.GetString();
                    reader.Read();
                    if (propertyName == "Identity")
                    {
                        itemSpec = reader.GetString();
                    }
                    else
                    {
                        metadata[propertyName] = reader.GetString();
                    }
                }
                var taskItem = new TaskItem(itemSpec, metadata);
                result[key] = taskItem;
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<string, ITaskItem> value, JsonSerializerOptions options)
            {

                writer.WriteStartObject();
                foreach (var kvp in value)
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteTaskItem(writer, kvp.Value);
                }
                writer.WriteEndObject();
            }

            private void WriteTaskItem(Utf8JsonWriter writer, ITaskItem taskItem)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Identity");
                writer.WriteStringValue(taskItem.ItemSpec);
                foreach (var metadata in taskItem.CloneCustomMetadata())
                {
                    if (metadata is DictionaryEntry kvp)
                    {
                        var key = (string)kvp.Key;
                        var value = (string)kvp.Value;
                        writer.WritePropertyName(key);
                        writer.WriteStringValue(value);
                    }
                }
                writer.WriteEndObject();
            }
        }
    }
}
