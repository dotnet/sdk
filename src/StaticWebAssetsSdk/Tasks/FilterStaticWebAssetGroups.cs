// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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

    // Endpoints whose asset was excluded by group filtering.
    [Output]
    public ITaskItem[] RemovedEndpoints { get; set; }

    // All endpoints that survived group filtering (unaffected groups +
    // endpoints from affected groups whose asset was not excluded).
    [Output]
    public ITaskItem[] SurvivingEndpoints { get; set; }

    public override bool Execute()
    {
        var groups = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);

        if (groups.Count == 0)
        {
            FilteredAssets = Assets;
            RemovedEndpoints = [];
            SurvivingEndpoints = Endpoints;
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

        var parsedAssets = StaticWebAsset.FromTaskItemGroup(Assets);
        var (_, excludedAssetFiles) = StaticWebAsset.FilterByGroup(parsedAssets, groups, SkipDeferred, Source);

        // Null out excluded entries in-place — MSBuild ignores null ITaskItem[] entries,
        // so we avoid allocating a new list and re-serializing included assets.
        for (var i = 0; i < Assets.Length; i++)
        {
            if (excludedAssetFiles.Contains(Assets[i].ItemSpec))
            {
                Assets[i] = null;
            }
        }

        FilteredAssets = Assets;

        var parsedEndpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);

        if (excludedAssetFiles.Count > 0)
        {
            var endpointGroups = StaticWebAssetEndpointGroup.CreateEndpointGroups(parsedEndpoints);
            var (removedEndpoints, survivingEndpoints) =
                StaticWebAssetEndpointGroup.ComputeFilteredEndpoints(endpointGroups, excludedAssetFiles);

            foreach (var endpoint in removedEndpoints)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
                    endpoint.Route, endpoint.AssetFile);
            }

            RemovedEndpoints = StaticWebAssetEndpoint.ToTaskItems(removedEndpoints);
            SurvivingEndpoints = StaticWebAssetEndpoint.ToTaskItems(survivingEndpoints);
        }
        else
        {
            RemovedEndpoints = [];
            SurvivingEndpoints = StaticWebAssetEndpoint.ToTaskItems(parsedEndpoints);
        }

        return !Log.HasLoggedErrors;
    }
}
