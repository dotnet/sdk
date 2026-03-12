// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Unified task for filtering assets and endpoints by group declarations.
// When SkipDeferred=true (first pass / pre-filter), groups marked Deferred are skipped.
// When SkipDeferred=false (final pass), all groups must be concrete — an error is raised
// if any group is still marked Deferred.
public class FilterStaticWebAssetGroups : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    // When provided, only assets with matching SourceId are filtered; others pass through.
    public string Source { get; set; }

    // When true, groups marked Deferred are skipped (first-pass pre-filter).
    public bool SkipDeferred { get; set; }

    [Output]
    public ITaskItem[] FilteredAssets { get; set; }

    [Output]
    public ITaskItem[] FilteredEndpoints { get; set; }

    // Endpoints whose asset was excluded by group filtering.
    [Output]
    public ITaskItem[] RemovedEndpoints { get; set; }

    // Endpoints that share a route with a removed endpoint but belong to a
    // non-excluded asset.  MSBuild Remove operations match by ItemSpec (Route),
    // so callers that remove by ItemSpec must re-add surviving endpoints.
    [Output]
    public ITaskItem[] SurvivingEndpoints { get; set; }

    public override bool Execute()
    {
        var groups = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);

        if (groups.Count == 0)
        {
            FilteredAssets = Assets;
            FilteredEndpoints = Endpoints;
            RemovedEndpoints = [];
            SurvivingEndpoints = [];
            return true;
        }

        if (!SkipDeferred)
        {
            foreach (var group in groups.Values)
            {
                if (group.Deferred)
                {
                    Log.LogError(
                        "Group '{0}' for source '{1}' is still marked as Deferred during final evaluation. " +
                        "Deferred groups must be resolved before the final filtering pass.",
                        group.Name, group.SourceId);
                    return false;
                }
            }
        }

        var parsedAssets = StaticWebAsset.SortByRelatedAsset(StaticWebAsset.FromTaskItemGroup(Assets));
        var excludedAssetFiles = new HashSet<string>(OSPath.PathComparer);
        var includedAssets = new List<StaticWebAsset>(parsedAssets.Length);

        foreach (var asset in parsedAssets)
        {
            if (!string.IsNullOrEmpty(Source) && !string.Equals(asset.SourceId, Source, StringComparison.Ordinal))
            {
                includedAssets.Add(asset);
                continue;
            }

            if (!string.IsNullOrEmpty(asset.RelatedAsset) && excludedAssetFiles.Contains(asset.RelatedAsset))
            {
                excludedAssetFiles.Add(asset.Identity);
                continue;
            }

            if (asset.MatchesGroups(groups, SkipDeferred))
            {
                includedAssets.Add(asset);
            }
            else
            {
                excludedAssetFiles.Add(asset.Identity);
            }
        }

        FilteredAssets = includedAssets.Select(asset => asset.ToTaskItem()).ToArray();

        var parsedEndpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);

        if (excludedAssetFiles.Count > 0)
        {
            var endpointGroups = StaticWebAssetEndpointGroup.CreateEndpointGroups(parsedEndpoints);
            var (removedEndpoints, survivingEndpoints) =
                StaticWebAssetEndpointGroup.ComputeFilteredEndpoints(endpointGroups, excludedAssetFiles);

            var removedSet = new HashSet<StaticWebAssetEndpoint>(
                removedEndpoints, StaticWebAssetEndpoint.RouteAndAssetComparer);

            var filteredEndpoints = new List<StaticWebAssetEndpoint>(parsedEndpoints.Length);
            foreach (var endpoint in parsedEndpoints)
            {
                if (removedSet.Contains(endpoint))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
                        endpoint.Route, endpoint.AssetFile);
                    continue;
                }
                filteredEndpoints.Add(endpoint);
            }

            FilteredEndpoints = StaticWebAssetEndpoint.ToTaskItems(filteredEndpoints);
            RemovedEndpoints = StaticWebAssetEndpoint.ToTaskItems(removedEndpoints);
            SurvivingEndpoints = StaticWebAssetEndpoint.ToTaskItems(survivingEndpoints);
        }
        else
        {
            FilteredEndpoints = StaticWebAssetEndpoint.ToTaskItems(parsedEndpoints);
            RemovedEndpoints = [];
            SurvivingEndpoints = [];
        }

        return !Log.HasLoggedErrors;
    }
}
