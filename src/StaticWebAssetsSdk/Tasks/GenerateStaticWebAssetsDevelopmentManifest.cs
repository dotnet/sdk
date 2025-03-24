// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// The manifest needs to always be case sensitive, since we don't know what the final runtime environment
// will be. The runtime is responsible for merging the tree nodes in the manifest when the underlying OS
// is case insensitive.
public class GenerateStaticWebAssetsDevelopmentManifest : Task
{
    private static readonly char[] _separator = ['/'];

    [Required]
    public string Source { get; set; }

    [Required]
    public ITaskItem[] DiscoveryPatterns { get; set; }

    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public string ManifestPath { get; set; }

    [Required]
    public string CacheFilePath { get; set; }

    public override bool Execute()
    {
        if (File.Exists(ManifestPath) && File.GetLastWriteTimeUtc(ManifestPath) > File.GetLastWriteTimeUtc(CacheFilePath))
        {
            Log.LogMessage(MessageImportance.Low, "Skipping manifest generation because manifest file '{0}' is up to date.", ManifestPath);
            return true;
        }

        try
        {
            if (Assets.Length == 0 && DiscoveryPatterns.Length == 0)
            {
                Log.LogMessage(MessageImportance.Low, "Skipping manifest generation because no assets nor discovery patterns were found.");
                return true;
            }

            var manifest = ComputeDevelopmentManifest(
                Assets.Select(StaticWebAsset.FromTaskItem),
                DiscoveryPatterns.Select(StaticWebAssetsDiscoveryPattern.FromTaskItem));

            PersistManifest(manifest);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
        }
        return !Log.HasLoggedErrors;
    }

    public StaticWebAssetsDevelopmentManifest ComputeDevelopmentManifest(
        IEnumerable<StaticWebAsset> assets,
        IEnumerable<StaticWebAssetsDiscoveryPattern> discoveryPatterns)
    {
        var assetsWithPathSegments = ComputeManifestAssets(assets).ToArray();
        Array.Sort(assetsWithPathSegments);

        var discoveryPatternsByBasePath = discoveryPatterns
            .GroupBy(p => p.HasSourceId(Source) ? "" : p.BasePath,
             (key, values) =>
                (key.Split(_separator, options: StringSplitOptions.RemoveEmptyEntries),
                values.OrderBy(id => id.ContentRoot).ThenBy(id => id.Pattern).ToArray())).ToArray();

        Array.Sort(discoveryPatternsByBasePath, (x, y) =>
        {
            var lengthResult = x.Item1.Length.CompareTo(y.Item1.Length);
            if (lengthResult != 0)
            {
                return lengthResult;
            }
            for (var i = 0; i < x.Item1.Length; i++)
            {
                var comparison = x.Item1[i].CompareTo(y.Item1[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        });

        var manifest = CreateManifest(assetsWithPathSegments, discoveryPatternsByBasePath);
        return manifest;
    }

    private IEnumerable<SegmentsAssetPair> ComputeManifestAssets(IEnumerable<StaticWebAsset> assets)
    {
        var assetsByTargetPath = assets
            .GroupBy(a => a.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance));

        foreach (var group in assetsByTargetPath)
        {
            var asset = StaticWebAsset.ChooseNearestAssetKind(group, StaticWebAsset.AssetKinds.Build).SingleOrDefault();

            if (asset == null)
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because it is a 'Publish' asset.", group.Key);
                continue;
            }

            if (asset.HasSourceId(Source) && !StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetInCurrentProject(asset, StaticWebAssetsManifest.ManifestModes.Root))
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because asset mode is '{1}'",
                    asset.Identity,
                    asset.AssetMode);

                continue;
            }

            yield return new SegmentsAssetPair(group.Key, asset);
        }
    }

    private void PersistManifest(StaticWebAssetsDevelopmentManifest manifest)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest, StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetsDevelopmentManifest);
#if !NET9_0_OR_GREATER
        using var sha256 = SHA256.Create();
        var currentHash = sha256.ComputeHash(data);
#else
        var currentHash = SHA256.HashData(data);
#endif
        var fileExists = File.Exists(ManifestPath);
        var existingManifestHash = fileExists ?
#if !NET9_0_OR_GREATER
            sha256.ComputeHash(File.ReadAllBytes(ManifestPath)) :
#else
            SHA256.HashData(File.ReadAllBytes(ManifestPath)) :
#endif
            [];

        if (!fileExists)
        {
            Log.LogMessage(MessageImportance.Low, "Creating manifest because manifest file '{0}' does not exist.", ManifestPath);
            File.WriteAllBytes(ManifestPath, data);
        }
        else if (!currentHash.SequenceEqual(existingManifestHash))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Updating manifest because manifest version '{0}' is different from existing manifest hash '{1}'.",
                Convert.ToBase64String(currentHash),
                Convert.ToBase64String(existingManifestHash));
            File.WriteAllBytes(ManifestPath, data);
        }
        else
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping manifest update because manifest version '{0}' has not changed.",
                Convert.ToBase64String(currentHash));
        }
    }

    private static StaticWebAssetsDevelopmentManifest CreateManifest(
        SegmentsAssetPair[] assetsWithPathSegments,
        (string[], StaticWebAssetsDiscoveryPattern[] values)[] discoveryPatternsByBasePath)
    {
        var contentRootIndex = new Dictionary<string, int>();
        var root = new StaticWebAssetNode() { };
        foreach (var (segments, asset) in assetsWithPathSegments)
        {
            var currentNode = root;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segments.Length - 1 == i)
                {
                    if (!contentRootIndex.TryGetValue(asset.ContentRoot, out var index))
                    {
                        index = contentRootIndex.Count;
                        contentRootIndex.Add(asset.ContentRoot, contentRootIndex.Count);
                    }
                    var matchingAsset = new StaticWebAssetMatch
                    {
                        SubPath = ResolveSubPath(asset),
                        ContentRootIndex = index
                    };
                    currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                    currentNode.Children.Add(segment, new StaticWebAssetNode
                    {
                        Asset = matchingAsset
                    });
                    break;
                }
                else
                {
                    currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                    if (currentNode.Children.TryGetValue(segment, out var existing))
                    {
                        currentNode = existing;
                    }
                    else
                    {
                        var newNode = new StaticWebAssetNode
                        {
                            Children = new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal)
                        };
                        currentNode.Children.Add(segment, newNode);
                        currentNode = newNode;
                    }
                }
            }
        }

        foreach (var (segments, patternGroup) in discoveryPatternsByBasePath)
        {
            var currentNode = root;
            if (segments.Length == 0)
            {
                var patterns = new List<StaticWebAssetPattern>();
                foreach (var pattern in patternGroup)
                {
                    if (!contentRootIndex.TryGetValue(pattern.ContentRoot, out var index))
                    {
                        index = contentRootIndex.Count;
                        contentRootIndex.Add(pattern.ContentRoot, contentRootIndex.Count);
                    }
                    var assetPattern = new StaticWebAssetPattern
                    {
                        Pattern = pattern.Pattern,
                        ContentRootIndex = index
                    };
                    patterns.Add(assetPattern);
                }
                currentNode.Patterns = [.. patterns];
            }
            else
            {
                for (var i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    if (segments.Length - 1 == i)
                    {
                        var patterns = new List<StaticWebAssetPattern>();
                        foreach (var pattern in patternGroup)
                        {
                            if (!contentRootIndex.TryGetValue(pattern.ContentRoot, out var index))
                            {
                                index = contentRootIndex.Count;
                                contentRootIndex.Add(pattern.ContentRoot, contentRootIndex.Count);
                            }
                            var matchingPattern = new StaticWebAssetPattern
                            {
                                ContentRootIndex = index,
                                Pattern = pattern.Pattern,
                                Depth = segments.Length
                            };

                            patterns.Add(matchingPattern);
                        }
                        currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                        if (!currentNode.Children.TryGetValue(segment, out var childNode))
                        {
                            childNode = new StaticWebAssetNode
                            {
                                Patterns = [.. patterns],
                            };
                            currentNode.Children.Add(segment, childNode);
                        }
                        else
                        {
                            childNode.Patterns = [.. patterns];
                        }

                        break;
                    }
                    else
                    {
                        currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                        if (currentNode.Children.TryGetValue(segment, out var existing))
                        {
                            currentNode = existing;
                        }
                        else
                        {
                            var newNode = new StaticWebAssetNode
                            {
                                Children = new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal)
                            };
                            currentNode.Children.Add(segment, newNode);
                            currentNode = newNode;
                        }
                    }
                }
            }
        }

        return new StaticWebAssetsDevelopmentManifest
        {
            ContentRoots = contentRootIndex.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray(),
            Root = root
        };

        static string ResolveSubPath(StaticWebAsset asset)
        {
            if (File.Exists(asset.Identity))
            {
                if (asset.Identity.StartsWith(asset.ContentRoot, OSPath.PathComparison))
                {
                    // We need an extra check that the file exist to avoid pointing out to a non-existing file. This can happen
                    // when the asset is defined with an identity that doesn't exist yet, but that will be materialized later
                    // when the asset is copied to the wwwroot folder.
#if NET9_0_OR_GREATER
                    return StaticWebAsset.Normalize(asset.Identity[asset.ContentRoot.Length..]);
#else
                    return StaticWebAsset.Normalize(asset.Identity.Substring(asset.ContentRoot.Length));
#endif
                }
                else
                {
                    // This is a content root that we don't know about, so we can't resolve the subpath based on the identity, and
                    // we need to rely on the assumption that the file will be available at contentRoot + relativePath.
                    return asset.ReplaceTokens(asset.RelativePath, StaticWebAssetTokenResolver.Instance);
                }
            }
            else
            {
                // In any other case where the file doesn't exist, we expect the file to end up at the correct final location
                // which is defined by contentRoot + relativePath, and since the file will be copied there, the tokens will be
                // replaced as needed so that the file can be found.
                return asset.ReplaceTokens(asset.RelativePath, StaticWebAssetTokenResolver.Instance);
            }
        }
    }

    public class StaticWebAssetsDevelopmentManifest
    {
        public string[] ContentRoots { get; set; }

        public StaticWebAssetNode Root { get; set; }
    }

    public class StaticWebAssetPattern
    {
        public int ContentRootIndex { get; set; }
        public string Pattern { get; set; }
        public int Depth { get; set; }
    }

    public class StaticWebAssetMatch
    {
        public int ContentRootIndex { get; set; }
        public string SubPath { get; set; }
    }

    public class StaticWebAssetNode
    {
        public Dictionary<string, StaticWebAssetNode> Children { get; set; }
        public StaticWebAssetMatch Asset { get; set; }
        public StaticWebAssetPattern[] Patterns { get; set; }
    }

    private readonly struct SegmentsAssetPair(string path, StaticWebAsset asset) : IComparable<SegmentsAssetPair>
    {
        private static readonly char[] separator = ['/'];

        public string[] PathSegments { get; } = path.Split(separator, options: StringSplitOptions.RemoveEmptyEntries);

        public StaticWebAsset Asset { get; } = asset;

        public readonly int CompareTo(SegmentsAssetPair other)
        {
            if (PathSegments.Length != other.PathSegments.Length)
            {
                return PathSegments.Length.CompareTo(other.PathSegments.Length);
            }

            for (var i = 0; i < PathSegments.Length; i++)
            {
                var comparison = PathSegments[i].CompareTo(other.PathSegments[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }

        public readonly void Deconstruct(out string[] segments, out StaticWebAsset asset)
        {
            asset = Asset;
            segments = PathSegments;
        }
    }
}
