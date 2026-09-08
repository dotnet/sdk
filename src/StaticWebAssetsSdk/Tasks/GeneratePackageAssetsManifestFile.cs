// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Generates a JSON manifest file containing both static web asset and endpoint data
// for inclusion in a NuGet package. Replaces the pair of XML .props generators
// (GenerateStaticWebAssetsPropsFile + GenerateStaticWebAssetEndpointsPropsFile) for
// the new .targets-based packaging path (net11.0+).
public class GeneratePackageAssetsManifestFile : Task
{
    [Required]
    public ITaskItem[] StaticWebAssets { get; set; }

    [Required]
    public ITaskItem[] StaticWebAssetEndpoints { get; set; }

    [Required]
    public string TargetManifestPath { get; set; }

    public string PackagePathPrefix { get; set; } = "staticwebassets";

    public string FrameworkPattern { get; set; }

    public override bool Execute()
    {
        if (StaticWebAssets.Length == 0)
        {
            return !Log.HasLoggedErrors;
        }

        var tokenResolver = StaticWebAssetTokenResolver.Instance;

        var frameworkMatcher = CreateFrameworkMatcher();
        var hasFrameworkMatcher = frameworkMatcher != null;
        var matchContext = hasFrameworkMatcher ? StaticWebAssetGlobMatcher.CreateMatchContext() : default;

        // Parse all assets once and pre-compute package-relative paths.
        var parsedAssets = StaticWebAsset.FromTaskItemGroup(StaticWebAssets);
        var (identityToPackagePath, packagePaths) = ComputePackagePaths(parsedAssets, tokenResolver);

        var assets = BuildManifestAssets(parsedAssets, packagePaths, identityToPackagePath, frameworkMatcher, matchContext);
        if (assets == null)
        {
            return false;
        }

        var manifestEndpoints = BuildManifestEndpoints(identityToPackagePath);
        if (manifestEndpoints == null)
        {
            return false;
        }

        var manifest = new StaticWebAssetPackageManifest
        {
            Version = StaticWebAssetPackageManifest.CurrentVersion,
            ManifestType = StaticWebAssetPackageManifest.PackageManifestType,
            Assets = assets,
            Endpoints = manifestEndpoints,
        };

        this.PersistFileIfChanged(manifest, TargetManifestPath,
            StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetPackageManifest);

        return !Log.HasLoggedErrors;
    }

    private StaticWebAssetGlobMatcher CreateFrameworkMatcher()
    {
        if (string.IsNullOrEmpty(FrameworkPattern))
        {
            return null;
        }

        var patterns = FrameworkPattern
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();
        return new StaticWebAssetGlobMatcherBuilder()
            .AddIncludePatterns(patterns)
            .Build();
    }

    private (Dictionary<string, string> IdentityToPackagePath, string[] PackagePaths) ComputePackagePaths(
        StaticWebAsset[] parsedAssets, StaticWebAssetTokenResolver tokenResolver)
    {
        var identityToPackagePath = new Dictionary<string, string>(parsedAssets.Length, Utils.OSPath.PathComparer);
        var packagePaths = new string[parsedAssets.Length];
        for (var i = 0; i < parsedAssets.Length; i++)
        {
            var packagePath = parsedAssets[i].ComputeTargetPath(PackagePathPrefix, '/', tokenResolver, TokenResolveMode.Pack);
            identityToPackagePath[parsedAssets[i].Identity] = packagePath;
            packagePaths[i] = packagePath;
        }
        return (identityToPackagePath, packagePaths);
    }

    private Dictionary<string, StaticWebAsset> BuildManifestAssets(
        StaticWebAsset[] parsedAssets,
        string[] packagePaths,
        Dictionary<string, string> identityToPackagePath,
        StaticWebAssetGlobMatcher frameworkMatcher,
        StaticWebAssetGlobMatcher.MatchContext matchContext)
    {
        var hasFrameworkMatcher = frameworkMatcher != null;
        var assets = new Dictionary<string, StaticWebAsset>(OSPath.PathComparer);

        // Sort indices by BasePath then RelativePath for deterministic output.
        var indices = Enumerable.Range(0, parsedAssets.Length)
            .OrderBy(i => parsedAssets[i].BasePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => parsedAssets[i].RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var i in indices)
        {
            var asset = parsedAssets[i];
            var relativePath = asset.RelativePath;
            var packagePath = packagePaths[i];

            var emittedSourceType = StaticWebAsset.SourceTypes.Package;
            if (hasFrameworkMatcher)
            {
                matchContext.SetPathAndReinitialize(relativePath.AsSpan());
                var match = frameworkMatcher.Match(matchContext);
                if (match.IsMatch)
                {
                    emittedSourceType = StaticWebAsset.SourceTypes.Framework;
                }
            }

            // Remap RelatedAsset from build-time absolute path to package-relative path
            var relatedAssetValue = asset.RelatedAsset;
            if (!string.IsNullOrEmpty(relatedAssetValue) &&
                identityToPackagePath.TryGetValue(relatedAssetValue, out var remappedRelatedAsset))
            {
                relatedAssetValue = remappedRelatedAsset;
            }
            else if (!string.IsNullOrEmpty(relatedAssetValue))
            {
                Log.LogError(
                    "Asset '{0}' has RelatedAsset '{1}' which could not be mapped to a package-relative path. " +
                    "This indicates a graph inconsistency — the related asset is not part of the package.",
                    asset.Identity, relatedAssetValue);
                return null;
            }

            var manifestAsset = new StaticWebAsset(asset)
            {
                Identity = packagePath,
                SourceType = emittedSourceType,
                RelatedAsset = relatedAssetValue
            };

            assets[packagePath] = manifestAsset;
        }

        return assets;
    }

    private StaticWebAssetEndpoint[] BuildManifestEndpoints(Dictionary<string, string> identityToPackagePath)
    {
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(StaticWebAssetEndpoints);
        var manifestEndpoints = new List<StaticWebAssetEndpoint>();
        foreach (var endpoint in endpoints.OrderBy(e => e.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.AssetFile, StringComparer.OrdinalIgnoreCase))
        {
            // Remap AssetFile to package-relative path
            if (identityToPackagePath.TryGetValue(endpoint.AssetFile, out var packageRelativePath))
            {
                endpoint.AssetFile = packageRelativePath;
            }
            else
            {
                Log.LogError(
                    "Endpoint '{0}' references AssetFile '{1}' which could not be mapped to a package-relative path. " +
                    "This indicates a graph inconsistency — the referenced asset is not part of the package.",
                    endpoint.Route, endpoint.AssetFile);
                return null;
            }
            manifestEndpoints.Add(endpoint);
        }
        return manifestEndpoints.ToArray();
    }
}
