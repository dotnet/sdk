// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets
{
    public static class JoinVerticalsManifestExportHelper
    {
        public static XDocument ExportMergedManifest(BuildAssetsManifest mainBuildManifest, IEnumerable<AssetVerticalMatchResult> assets)
        {
            BuildAssetsManifest mergedManifest = new BuildAssetsManifest();
            foreach (var attribute in mainBuildManifest.Attributes.Where(o => o.Key != nameof(BuildAssetsManifest.VerticalName)))
            {
                mergedManifest.SetAttribute(attribute.Key, attribute.Value);
            }

            foreach (var asset in assets.OrderBy(o => o.Asset.AssetType).ThenBy(o => o.Asset.Id))
            {
                asset.Asset.BuildVertical = asset.VerticalName;
                mergedManifest.Assets.Add(asset.Asset);
            }

            return mergedManifest.SaveToDocument();
        }
    }
}
