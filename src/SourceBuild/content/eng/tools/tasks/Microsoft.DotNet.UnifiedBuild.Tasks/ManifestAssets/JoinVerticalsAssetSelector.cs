// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets
{
    public enum AssetVerticalMatchType
    {
        ExactMatch,
        NotSpecified
    }

    public class AssetVerticalMatchResult
    {
        public required string AssetId { get; set; }
        public required AssetVerticalMatchType MatchType { get; set; }
        public required string VerticalName { get; set; }
        public required ManifestAsset Asset { get; set; }
        public required IReadOnlyList<string> OtherVerticals { get; set; }
    }

    public class JoinVerticalsAssetSelector
    {
        private const string cAssetVisibilityExternal = "External";

        // Temporary solution to exclude some assets from Unified Build
        private bool ExcludeAsset(AssetVerticalMatchResult assetVerticalMatch)
        {
            return
                // Skip packages with stable version
                // - this can be removed after this issue is resolved: https://github.com/dotnet/source-build/issues/4892
                StringComparer.OrdinalIgnoreCase.Equals(assetVerticalMatch.AssetId, "Microsoft.Diagnostics.NETCore.Client") ||
                StringComparer.OrdinalIgnoreCase.Equals(assetVerticalMatch.AssetId, "Microsoft.NET.Sdk.Aspire.Manifest-8.0.100") ||
                // Skip productVersion.txt files from all repos except sdk
                // - this can be removed after this issue is resolved: https://github.com/dotnet/source-build/issues/4596
                (assetVerticalMatch.AssetId.Contains("/productVersion.txt", StringComparison.OrdinalIgnoreCase) && (assetVerticalMatch.Asset.RepoOrigin != "sdk"));
        }

        public IEnumerable<AssetVerticalMatchResult> SelectAssetMatchingVertical(IEnumerable<BuildAssetsManifest> verticalManifests)
        {
            bool IsExternalAsset(ManifestAsset asset)
            {
                string? visibility = asset.Visibility;
                return StringComparer.OrdinalIgnoreCase.Equals(visibility, cAssetVisibilityExternal) || string.IsNullOrEmpty(visibility);
            }

            var _assetsById = verticalManifests
                .SelectMany(manifest =>
                    manifest.Assets
                        .Where(IsExternalAsset)
                        .Select(asset => (manifest, asset))
                )
                .Where(o => o.manifest.VerticalName != null)
                .GroupBy(o => o.asset.Id);

            foreach (var assetGroup in _assetsById)
            {
                string assetId = assetGroup.Key;

                var verticalNames = assetGroup.Select(o => o.manifest.VerticalName!).ToList();

                (AssetVerticalMatchType matchType, string verticalName) = SelectVerticalForAsset(verticalNames);

                AssetVerticalMatchResult assetVerticalMatch = new AssetVerticalMatchResult
                {
                    AssetId = assetGroup.Key,
                    MatchType = matchType,
                    VerticalName = verticalName,
                    Asset = assetGroup.FirstOrDefault().asset,
                    OtherVerticals = assetGroup.Select(o => o.manifest.VerticalName!).Skip(1).ToList()
                };

                if (!ExcludeAsset(assetVerticalMatch))
                {
                    yield return assetVerticalMatch;
                }
            }
        }

        private (AssetVerticalMatchType matchType, string verticalName) SelectVerticalForAsset(IList<string> verticalNames)
        {
            // If there is only one vertical which built the asset
            if (verticalNames.Count == 1)
            {
                return (AssetVerticalMatchType.ExactMatch, verticalNames.Single());
            }

            // Select first vertical from the list and report it as ambiguous match
            return (AssetVerticalMatchType.NotSpecified, verticalNames.First());
        }

        public static bool VerticalNameMatches(string? verticalName1, string? verticalName2)
        {
            if (verticalName1 == null || verticalName2 == null)
            {
                return false;
            }
            return StringComparer.OrdinalIgnoreCase.Equals(verticalName1, verticalName2);
        }
    }
}
