// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
            if (IsExcludedByDeferredGroups(assetItem, deferredGroupNames))
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
        if (excludedAssetFiles.Count > 0)
        {
            bool changed;
            do
            {
                changed = false;
                for (var i = includedAssets.Count - 1; i >= 0; i--)
                {
                    var relatedAsset = includedAssets[i].GetMetadata("RelatedAsset");
                    if (!string.IsNullOrEmpty(relatedAsset) && excludedAssetFiles.Contains(relatedAsset))
                    {
                        excludedAssetFiles.Add(includedAssets[i].ItemSpec);
                        Log.LogMessage(MessageImportance.Low,
                            "Excluding related asset '{0}' because its primary '{1}' was excluded by deferred group filtering.",
                            includedAssets[i].ItemSpec, relatedAsset);
                        includedAssets.RemoveAt(i);
                        changed = true;
                    }
                }
            } while (changed);
        }

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

    private bool IsExcludedByDeferredGroups(ITaskItem assetItem, HashSet<string> deferredGroupNames)
    {
        var assetGroups = assetItem.GetMetadata("AssetGroups");
        if (string.IsNullOrEmpty(assetGroups))
        {
            return false; // Ungrouped assets are never excluded
        }

        var sourceId = assetItem.GetMetadata("SourceId");
        var requirements = assetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var requirement in requirements)
        {
            var eqIdx = requirement.IndexOf('=');
            if (eqIdx < 0)
            {
                continue;
            }

            var reqName = requirement.Substring(0, eqIdx);
            var reqValue = requirement.Substring(eqIdx + 1);

            // Only evaluate requirements whose group name is deferred
            if (!deferredGroupNames.Contains(reqName))
            {
                continue;
            }

            var satisfied = false;
            if (StaticWebAssetGroups != null)
            {
                foreach (var group in StaticWebAssetGroups)
                {
                    if (!string.Equals(group.ItemSpec, reqName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var groupSourceId = group.GetMetadata("SourceId");
                    if (!string.IsNullOrEmpty(groupSourceId) &&
                        !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(group.GetMetadata("Value"), reqValue, StringComparison.Ordinal))
                    {
                        satisfied = true;
                        break;
                    }
                }
            }

            if (!satisfied)
            {
                return true; // Deferred requirement not satisfied → exclude
            }
        }

        return false;
    }
}
