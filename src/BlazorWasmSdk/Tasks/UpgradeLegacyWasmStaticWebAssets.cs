// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.BlazorWebAssembly;

public class UpgradeLegacyWasmStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] LegacyAssets { get; set; }

    [Output]
    public ITaskItem[] UpgradedAssets { get; set; }

    public override bool Execute()
    {
        var upgradedAssets = new List<ITaskItem>();

        var legacyAssets = LegacyAssets.Select(StaticWebAsset.FromV1TaskItem).ToArray();
        var assetsByFinalPath = legacyAssets
            .GroupBy(asset => asset.ComputeTargetPath("", '/'))
            .ToDictionary(g => g.Key, g => g.ToArray());

        foreach (var legacyAsset in legacyAssets)
        {
            var upgradedAsset = new StaticWebAsset(legacyAsset);

            legacyAsset.AssetKind = StaticWebAsset.AssetKinds.Build;

            if (string.Equals(Path.GetExtension(legacyAsset.RelativePath), ".gz", StringComparison.Ordinal))
            {
                var relatedPath = legacyAsset.RelativePath.Substring(0, legacyAsset.RelativePath.Length - 3);
                var relatedAsset = assetsByFinalPath.TryGetValue(relatedPath, out var relatedAssets)
                    ? relatedAssets.Single()
                    : null;

                if (relatedAsset == null)
                {
                    Log.LogError($"Could not find a corresponding asset for '{legacyAsset.RelativePath}'");
                    continue;
                }

                Log.LogMessage($"Upgrading '{legacyAsset.RelativePath}' to use '{relatedAsset.Identity}' as the related asset");

                upgradedAsset.RelatedAsset = relatedAsset.Identity;
                upgradedAsset.AssetTraitName = "Content-Encoding";
                upgradedAsset.AssetTraitValue = "gzip";
            }

            upgradedAssets.Add(upgradedAsset.ToTaskItem());
        }

        UpgradedAssets = [.. upgradedAssets];

        return !Log.HasLoggedErrors;
    }
}
