// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class FilterDeferredStaticWebAssetGroups : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    [Output]
    public ITaskItem[] FilteredAssets { get; set; }

    [Output]
    public ITaskItem[] FilteredEndpoints { get; set; }

    public override bool Execute()
    {
        // Collect which group names are deferred
        var deferredGroupNames = new HashSet<string>(StringComparer.Ordinal);
        if (StaticWebAssetGroups != null)
        {
            foreach (var group in StaticWebAssetGroups)
            {
                if (string.Equals(group.GetMetadata("Deferred"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    deferredGroupNames.Add(group.ItemSpec);
                }
            }
        }

        if (deferredGroupNames.Count == 0)
        {
            // No deferred groups — nothing to filter
            FilteredAssets = Assets;
            FilteredEndpoints = Endpoints;
            return true;
        }

        // Phase 1: Evaluate deferred group requirements on each asset
        var excludedAssetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includedAssets = new List<ITaskItem>(Assets.Length);

        foreach (var assetItem in Assets)
        {
            if (StaticWebAssetGroupFilter.IsExcludedByDeferredGroups(
                    assetItem.GetMetadata("AssetGroups"),
                    assetItem.GetMetadata("SourceId"),
                    deferredGroupNames,
                    StaticWebAssetGroups))
            {
                excludedAssetFiles.Add(assetItem.ItemSpec);
                Log.LogMessage(MessageImportance.Low,
                    "Excluding asset '{0}' by deferred group filtering.", assetItem.ItemSpec);
            }
            else
            {
                includedAssets.Add(assetItem);
            }
        }

        // Phase 2: Cascading exclusion of related/alternative assets
        StaticWebAssetGroupFilter.CascadeExclusions(
            includedAssets,
            excludedAssetFiles,
            item => item.ItemSpec,
            item => item.GetMetadata("RelatedAsset"));

        FilteredAssets = includedAssets.ToArray();

        // Phase 3: Filter endpoints for excluded assets
        if (excludedAssetFiles.Count > 0)
        {
            var filteredEndpoints = new List<ITaskItem>(Endpoints.Length);
            foreach (var endpoint in Endpoints)
            {
                var assetFile = endpoint.GetMetadata("AssetFile");
                if (!string.IsNullOrEmpty(assetFile) && excludedAssetFiles.Contains(assetFile))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by deferred group filtering.",
                        endpoint.ItemSpec, assetFile);
                    continue;
                }
                filteredEndpoints.Add(endpoint);
            }
            FilteredEndpoints = filteredEndpoints.ToArray();
        }
        else
        {
            FilteredEndpoints = Endpoints;
        }

        return !Log.HasLoggedErrors;
    }
}
