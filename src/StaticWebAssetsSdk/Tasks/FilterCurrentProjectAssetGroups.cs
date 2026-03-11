// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Filters the current project's own assets and endpoints by group declarations.
// Runs after the build/publish manifest has been written (which retains all variants)
// so that the dev manifest and endpoints manifest only include the selected variant.
public class FilterCurrentProjectAssetGroups : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    [Required]
    public string Source { get; set; }

    [Output]
    public ITaskItem[] FilteredAssets { get; set; }

    [Output]
    public ITaskItem[] FilteredEndpoints { get; set; }

    public override bool Execute()
    {
        var groups = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);

        if (groups.Length == 0)
        {
            FilteredAssets = Assets;
            FilteredEndpoints = Endpoints;
            return true;
        }

        var excludedAssetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includedAssets = new List<ITaskItem>(Assets.Length);

        foreach (var assetItem in Assets)
        {
            var sourceId = assetItem.GetMetadata("SourceId");

            // Only filter assets from the current project
            if (!string.Equals(sourceId, Source, StringComparison.Ordinal))
            {
                includedAssets.Add(assetItem);
                continue;
            }

            var assetGroups = assetItem.GetMetadata("AssetGroups");
            if (StaticWebAssetGroupFilter.IsAssetIncludedByGroups(assetGroups, sourceId, groups))
            {
                includedAssets.Add(assetItem);
            }
            else
            {
                excludedAssetFiles.Add(assetItem.ItemSpec);
                Log.LogMessage(MessageImportance.Low,
                    "Excluding current-project asset '{0}' by group filtering.", assetItem.ItemSpec);
            }
        }

        // Cascading exclusion of related/alternative assets
        StaticWebAssetGroupFilter.CascadeExclusions(
            includedAssets,
            excludedAssetFiles,
            item => item.ItemSpec,
            item => item.GetMetadata("RelatedAsset"));

        FilteredAssets = includedAssets.ToArray();

        // Filter endpoints for excluded assets
        if (excludedAssetFiles.Count > 0)
        {
            var filteredEndpoints = new List<ITaskItem>(Endpoints.Length);
            foreach (var endpoint in Endpoints)
            {
                var assetFile = endpoint.GetMetadata("AssetFile");
                if (!string.IsNullOrEmpty(assetFile) && excludedAssetFiles.Contains(assetFile))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by current-project group filtering.",
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
