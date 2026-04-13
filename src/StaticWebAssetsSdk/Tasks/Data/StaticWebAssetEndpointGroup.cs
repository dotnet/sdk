// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Groups endpoints that share the same Route (ItemSpec in MSBuild terms).
// Multiple endpoints can share the same Route but point to different AssetFiles.
// MSBuild Remove operations match by ItemSpec, so removing an endpoint for one
// asset can accidentally remove endpoints for other assets with the same Route.
// This grouping enables correct filtering by tracking which endpoints share a route.
// TState allows tasks to attach per-group state (e.g., compression metadata, linked groups).
internal class StaticWebAssetEndpointGroup<TState> where TState : class
{
    public string Route { get; set; }

    public TState State { get; set; }

    public List<StaticWebAssetEndpointGroupItem> Items { get; } = new();

    // Creates a dictionary grouping endpoints by Route.
    internal static Dictionary<string, StaticWebAssetEndpointGroup<TState>> CreateEndpointGroups(
        StaticWebAssetEndpoint[] endpoints)
    {
        var groups = new Dictionary<string, StaticWebAssetEndpointGroup<TState>>(StringComparer.Ordinal);

        foreach (var endpoint in endpoints)
        {
            if (!groups.TryGetValue(endpoint.Route, out var group))
            {
                group = new StaticWebAssetEndpointGroup<TState> { Route = endpoint.Route };
                groups[endpoint.Route] = group;
            }

            group.Items.Add(new StaticWebAssetEndpointGroupItem
            {
                AssetFile = endpoint.AssetFile,
                Endpoint = endpoint
            });
        }

        return groups;
    }

    // Iterates over all endpoint groups and classifies each endpoint:
    //  - removed:   endpoints whose asset was excluded
    //  - surviving: all endpoints that survived filtering (unaffected groups +
    //               endpoints from affected groups whose asset was not excluded)
    internal static (List<StaticWebAssetEndpoint> removed, List<StaticWebAssetEndpoint> surviving)
        ComputeFilteredEndpoints(
            Dictionary<string, StaticWebAssetEndpointGroup<TState>> groups,
            HashSet<string> excludedAssetFiles)
    {
        var removed = new List<StaticWebAssetEndpoint>();
        var surviving = new List<StaticWebAssetEndpoint>();

        foreach (var group in groups.Values)
        {
            foreach (var item in group.Items)
            {
                if (!string.IsNullOrEmpty(item.AssetFile) && excludedAssetFiles.Contains(item.AssetFile))
                {
                    removed.Add(item.Endpoint);
                }
                else
                {
                    surviving.Add(item.Endpoint);
                }
            }
        }

        return (removed, surviving);
    }
}

// Non-generic version for callers that don't need per-group state.
internal class StaticWebAssetEndpointGroup : StaticWebAssetEndpointGroup<object>
{
}

internal class StaticWebAssetEndpointGroupItem
{
    public string AssetFile { get; set; }

    public StaticWebAssetEndpoint Endpoint { get; set; }
}
