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

        // Set up framework pattern matcher if provided
        StaticWebAssetGlobMatcher frameworkMatcher = null;
        if (!string.IsNullOrEmpty(FrameworkPattern))
        {
            var patterns = FrameworkPattern
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
            frameworkMatcher = new StaticWebAssetGlobMatcherBuilder()
                .AddIncludePatterns(patterns)
                .Build();
        }

        var hasFrameworkMatcher = frameworkMatcher != null;
        var matchContext = hasFrameworkMatcher ? StaticWebAssetGlobMatcher.CreateMatchContext() : default;

        // Pre-compute identity-to-package-relative-path mapping for RelatedAsset remapping
        var identityToPackagePath = new Dictionary<string, string>(Utils.OSPath.PathComparer);
        foreach (var element in StaticWebAssets)
        {
            var asset = StaticWebAsset.FromTaskItem(element);
            var packagePath = asset.ComputeTargetPath(PackagePathPrefix, '/', tokenResolver);
            identityToPackagePath[element.ItemSpec] = packagePath;
        }

        // Build manifest assets
        var assets = new Dictionary<string, StaticWebAsset>(OSPath.PathComparer);
        var orderedAssets = StaticWebAssets
            .OrderBy(e => e.GetMetadata("BasePath"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.GetMetadata("RelativePath"), StringComparer.OrdinalIgnoreCase);

        foreach (var element in orderedAssets)
        {
            var asset = StaticWebAsset.FromTaskItem(element);
            var relativePath = asset.RelativePath;
            var packagePath = identityToPackagePath[element.ItemSpec];

            var emittedSourceType = "Package";
            if (hasFrameworkMatcher)
            {
                matchContext.SetPathAndReinitialize(relativePath.AsSpan());
                var match = frameworkMatcher.Match(matchContext);
                if (match.IsMatch)
                {
                    emittedSourceType = "Framework";
                }
            }

            // Remap RelatedAsset from build-time absolute path to package-relative path
            var relatedAssetValue = element.GetMetadata("RelatedAsset");
            if (!string.IsNullOrEmpty(relatedAssetValue) &&
                identityToPackagePath.TryGetValue(relatedAssetValue, out var remappedRelatedAsset))
            {
                relatedAssetValue = remappedRelatedAsset;
            }
            else if (!string.IsNullOrEmpty(relatedAssetValue))
            {
                // If we can't remap, clear it
                relatedAssetValue = "";
            }

            var manifestAsset = new StaticWebAsset(asset)
            {
                Identity = packagePath,
                SourceType = emittedSourceType,
                RelatedAsset = relatedAssetValue
            };

            assets[packagePath] = manifestAsset;
        }

        // Build manifest endpoints — reuse identityToPackagePath for AssetFile remapping
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
            manifestEndpoints.Add(endpoint);
        }

        var manifest = new StaticWebAssetPackageManifest
        {
            Version = 1,
            ManifestType = "Package",
            Assets = assets,
            Endpoints = manifestEndpoints.ToArray(),
        };

        this.PersistFileIfChanged(manifest, TargetManifestPath,
            StaticWebAssetsJsonSerializerContext.RelaxedEscaping.StaticWebAssetPackageManifest);

        return !Log.HasLoggedErrors;
    }
}
