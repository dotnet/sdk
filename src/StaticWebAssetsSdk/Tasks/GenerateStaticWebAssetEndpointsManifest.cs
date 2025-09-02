// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class GenerateStaticWebAssetEndpointsManifest : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; } = [];

    [Required]
    public ITaskItem[] Endpoints { get; set; } = [];

    [Required]
    public string ManifestType { get; set; }

    [Required] public string Source { get; set; }

    [Required]
    public string ManifestPath { get; set; }

    public string CacheFilePath { get; set; }

    public string ExclusionPatterns { get; set; }

    public string ExclusionPatternsCacheFilePath { get; set; }

    public override bool Execute()
    {
        var (patternString, parsedPatterns) = ParseAndSortPatterns(ExclusionPatterns);
        var existingPatternString = !string.IsNullOrEmpty(ExclusionPatternsCacheFilePath) && File.Exists(ExclusionPatternsCacheFilePath)
            ? File.ReadAllText(ExclusionPatternsCacheFilePath)
            : null;
        existingPatternString = string.IsNullOrEmpty(existingPatternString) ? null : existingPatternString;
        if (!string.IsNullOrEmpty(CacheFilePath) && File.Exists(ManifestPath) && File.GetLastWriteTimeUtc(ManifestPath) > File.GetLastWriteTimeUtc(CacheFilePath))
        {
            // Check if exclusion patterns cache is also up to date
            if (string.Equals(patternString, existingPatternString, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, "Skipping manifest generation because manifest file '{0}' is up to date.", ManifestPath);
                return true;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Generating manifest file '{0}' because exclusion patterns changed from '{1}' to '{2}'.", ManifestPath,
                    existingPatternString ?? "no patterns",
                    patternString ?? "no patterns");
            }
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "Generating manifest file '{0}' because manifest file is missing or out of date.", ManifestPath);
        }

        try
        {
            // Update exclusion patterns cache if needed
            UpdateExclusionPatternsCache(existingPatternString, patternString);

            // Get the list of the asset that need to be part of the manifest (this is similar to GenerateStaticWebAssetsDevelopmentManifest)
            var assets = StaticWebAsset.FromTaskItemGroup(Assets);
            var manifestAssets = ComputeManifestAssets(assets, ManifestType)
                .ToDictionary(a => a.ResolvedAsset.Identity, a => a, OSPath.PathComparer);

            // Build exclusion matcher if patterns are provided
            StaticWebAssetGlobMatcher exclusionMatcher = null;
            if (parsedPatterns.Length > 0)
            {
                var builder = new StaticWebAssetGlobMatcherBuilder();
                builder.AddIncludePatternsList(parsedPatterns);
                exclusionMatcher = builder.Build();
            }

            // Filter out the endpoints to those that point to the assets that are part of the manifest
            var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);
            var filteredEndpoints = new List<StaticWebAssetEndpoint>();
            var updatedManifest = false;
            foreach (var endpoint in endpoints)
            {
                if (!manifestAssets.TryGetValue(endpoint.AssetFile, out var asset))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping endpoint '{0}' because the asset '{1}' is not part of the manifest", endpoint.Route, endpoint.AssetFile);
                    continue;
                }

                // Check if endpoint should be excluded based on patterns
                var route = asset.ResolvedAsset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);
                if (exclusionMatcher != null)
                {
                    var match = exclusionMatcher.Match(route);
                    if (match.IsMatch)
                    {
                        if (!updatedManifest && File.Exists(ManifestPath))
                        {
                            updatedManifest = true;
                            // Touch the manifest if we are excluding endpoints to ensure we don't keep reporting out of date
                            // for the excluded endpoints.
                            // (The SWA manifest we use as cache might get updated, but if we filter out the new endpoints, we won't
                            // update the endpoints manifest file and on the next build we will re-enter this loop).
                            Log.LogMessage(MessageImportance.Low, "Updating manifest timestamp '{0}'.", ManifestPath);
                            File.SetLastWriteTime(ManifestPath, DateTime.UtcNow);
                        }
                        Log.LogMessage(MessageImportance.Low, "Excluding endpoint '{0}' based on exclusion patterns", route);
                        continue;
                    }
                }

                filteredEndpoints.Add(endpoint);
                // Update the endpoint to use the target path of the asset, this will be relative to the wwwroot

                endpoint.AssetFile = asset.ResolvedAsset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);
                endpoint.Route = route;

                Log.LogMessage(MessageImportance.Low, "Including endpoint '{0}' for asset '{1}' with final location '{2}'", endpoint.Route, endpoint.AssetFile, asset.TargetPath);
            }

            var manifest = new StaticWebAssetEndpointsManifest()
            {
                Version = 1,
                ManifestType = ManifestType,
                Endpoints = [.. filteredEndpoints]
            };

            this.PersistFileIfChanged(manifest, ManifestPath, StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetEndpointsManifest);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, null);
            return false;
        }

        return !Log.HasLoggedErrors;
    }

    private static (string, string[]) ParseAndSortPatterns(string patterns)
    {
        if (string.IsNullOrEmpty(patterns))
        {
            return (null, []);
        }

        var parsed = patterns.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(parsed, StringComparer.OrdinalIgnoreCase);

        return (string.Join(Environment.NewLine, parsed), parsed);
    }

    private void UpdateExclusionPatternsCache(string existingPatternString, string patternString)
    {
        if (string.IsNullOrEmpty(ExclusionPatternsCacheFilePath))
        {
            return;
        }

        if (!File.Exists(ExclusionPatternsCacheFilePath) ||
            !string.Equals(existingPatternString, patternString, StringComparison.Ordinal))
        {
            var directoryName = Path.GetDirectoryName(ExclusionPatternsCacheFilePath);
            if (directoryName != null)
            {
                Directory.CreateDirectory(directoryName);
            }
            File.WriteAllText(ExclusionPatternsCacheFilePath, patternString);
            // We need to touch the file because otherwise we will keep thinking that is out of date in the future.
            // This file might not be rewritten if the results are unchanged.
            if (File.Exists(ManifestPath))
            {
                File.SetLastWriteTime(ManifestPath, DateTime.UtcNow);
            }
        }
    }

    private IEnumerable<TargetPathAssetPair> ComputeManifestAssets(IEnumerable<StaticWebAsset> assets, string kind)
    {
        var assetsByTargetPath = assets
            .GroupBy(a => a.ComputeTargetPath("", '/'));

        foreach (var group in assetsByTargetPath)
        {
            var asset = StaticWebAsset.ChooseNearestAssetKind(group, kind).SingleOrDefault();

            if (asset == null)
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because it is not a '{1}' or 'All' asset.", group.Key, kind);
                continue;
            }

            if (asset.HasSourceId(Source) && !StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetInCurrentProject(asset, StaticWebAssetsManifest.ManifestModes.Root))
            {
                Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because asset mode is '{1}'",
                    asset.Identity,
                    asset.AssetMode);

                continue;
            }

            yield return new TargetPathAssetPair(group.Key, asset);
        }
    }

    private sealed class TargetPathAssetPair(string targetPath, StaticWebAsset asset)
    {
        public string TargetPath { get; } = targetPath;
        public StaticWebAsset ResolvedAsset { get; } = asset;
    }
}
