// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Unified task for filtering assets and endpoints by group declarations.
// Two modes:
//   - DeferredOnly=false (default): Eager filtering scoped to the current project (Source required).
//     Only assets whose SourceId matches Source are evaluated; others pass through.
//   - DeferredOnly=true: Evaluates only deferred group requirements on all assets.
public class FilterStaticWebAssetGroups : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    // When provided, only assets with matching SourceId are filtered; others pass through.
    public string Source { get; set; }

    // When true, only evaluates deferred group requirements.
    public bool DeferredOnly { get; set; }

    [Output]
    public ITaskItem[] FilteredAssets { get; set; }

    [Output]
    public ITaskItem[] FilteredEndpoints { get; set; }

    public override bool Execute()
    {
        var groups = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);

        if (DeferredOnly)
        {
            return ExecuteDeferredMode(groups);
        }

        return ExecuteEagerMode(groups);
    }

    private bool ExecuteEagerMode(StaticWebAssetGroup[] groups)
    {
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

            // Only filter assets from the current project when Source is set
            if (!string.IsNullOrEmpty(Source) && !string.Equals(sourceId, Source, StringComparison.Ordinal))
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

        CascadeAndFilterEndpoints(includedAssets, excludedAssetFiles);
        return !Log.HasLoggedErrors;
    }

    private bool ExecuteDeferredMode(StaticWebAssetGroup[] groups)
    {
        var deferredGroupNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            if (group.Deferred)
            {
                deferredGroupNames.Add(group.Name);
            }
        }

        if (deferredGroupNames.Count == 0)
        {
            FilteredAssets = Assets;
            FilteredEndpoints = Endpoints;
            return true;
        }

        var excludedAssetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includedAssets = new List<ITaskItem>(Assets.Length);

        foreach (var assetItem in Assets)
        {
            if (StaticWebAssetGroupFilter.IsExcludedByDeferredGroups(
                    assetItem.GetMetadata("AssetGroups"),
                    assetItem.GetMetadata("SourceId"),
                    deferredGroupNames,
                    groups))
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

        CascadeAndFilterEndpoints(includedAssets, excludedAssetFiles);
        return !Log.HasLoggedErrors;
    }

    private void CascadeAndFilterEndpoints(List<ITaskItem> includedAssets, HashSet<string> excludedAssetFiles)
    {
        StaticWebAssetGroupFilter.CascadeExclusions(
            includedAssets,
            excludedAssetFiles,
            item => item.ItemSpec,
            item => item.GetMetadata("RelatedAsset"));

        FilteredAssets = includedAssets.ToArray();

        if (excludedAssetFiles.Count > 0)
        {
            var filteredEndpoints = new List<ITaskItem>(Endpoints.Length);
            foreach (var endpoint in Endpoints)
            {
                var assetFile = endpoint.GetMetadata("AssetFile");
                if (!string.IsNullOrEmpty(assetFile) && excludedAssetFiles.Contains(assetFile))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
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
    }
}
