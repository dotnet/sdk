// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Filters the list of endpoints based on the provided criteria
// The endpoints must meet all the criteria to be included.
// The criteria are:
// - Assets: The asset file that the endpoint is associated with must be in the list of assets.
// - Properties: The endpoint must have all the properties specified in the list of properties with
//   optionally a specific value.
// - Selectors: The endpoint must have all the selectors specified in the list of selectors with
//   optionally a specific value.
// - Headers: The endpoint must have all the headers specified in the list of headers with
//   optionally a specific value.
public class FilterStaticWebAssetEndpoints : Task
{
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] Assets { get; set; }

    public ITaskItem[] Filters { get; set; }

    [Output] public ITaskItem[] FilteredEndpoints { get; set; }

    [Output] public ITaskItem[] AssetsWithoutMatchingEndpoints { get; set; }

    public override bool Execute()
    {
        var filterCriteria = (Filters ?? []).Select(FilterCriteria.FromTaskItem).ToArray();
        var assetFiles = (Assets ?? []).ToDictionary(a => a.ItemSpec, a => StaticWebAsset.FromTaskItem(a));
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints ?? []);
        var endpointFoundMatchingAsset = new Dictionary<string, StaticWebAsset>();

        var filteredEndpoints = new List<StaticWebAssetEndpoint>();
        for (int i = 0; i < endpoints.Length; i++)
        {
            StaticWebAsset asset = null;
            var endpoint = endpoints[i];
            if (assetFiles.Count > 0 && !assetFiles.TryGetValue(endpoint.AssetFile, out asset))
            {
                continue;
            }

            if (MeetsAllCriteria(endpoint, asset, filterCriteria, out var failingCriteria))
            {
                if (asset != null && !endpointFoundMatchingAsset.ContainsKey(asset.Identity))
                {
                    endpointFoundMatchingAsset.Add(asset.Identity, asset);
                }
                filteredEndpoints.Add(endpoint);
            }
            else
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    $"Filtered out endpoint {endpoint.Route} because it did not meet the criteria {failingCriteria.Type} {failingCriteria.Name} {failingCriteria.Value}");
            }
        }

        FilteredEndpoints = filteredEndpoints.Select(e => e.ToTaskItem()).ToArray();

        foreach (var asset in endpointFoundMatchingAsset)
        {
            assetFiles.Remove(asset.Key);
        }

        AssetsWithoutMatchingEndpoints = assetFiles.Values.Select(a => a.ToTaskItem()).ToArray();
        return !Log.HasLoggedErrors;
    }

    private bool MeetsAllCriteria(StaticWebAssetEndpoint endpoint, StaticWebAsset asset, FilterCriteria[] filterCriteria, out FilterCriteria failingCriteria)
    {
        for (int i = 0; i < filterCriteria.Length; i++)
        {
            var criteria = filterCriteria[i];
            switch (criteria.Type)
            {
                case "Property":
                    var meetsPropertyCriteria = criteria.ExcludeOnMatch();
                    for (var j = 0; j < endpoint.EndpointProperties.Length; j++)
                    {
                        var property = endpoint.EndpointProperties[j];
                        if (MeetsCriteria(criteria, property.Name, property.Value))
                        {
                            meetsPropertyCriteria = !criteria.ExcludeOnMatch();
                            break;
                        }
                    }
                    if (!meetsPropertyCriteria)
                    {
                        failingCriteria = criteria;
                        return false;
                    }
                    break;
                case "Selector":
                    var meetsSelectorCriteria = criteria.ExcludeOnMatch();
                    for (var j = 0; j < endpoint.Selectors.Length; j++)
                    {
                        var selector = endpoint.Selectors[j];
                        if (MeetsCriteria(criteria, selector.Name, selector.Value))
                        {
                            meetsSelectorCriteria = !criteria.ExcludeOnMatch();
                            break;
                        }
                    }
                    if (!meetsSelectorCriteria)
                    {
                        failingCriteria = criteria;
                        return false;
                    }
                    break;
                case "Header":
                    var meetsHeaderCriteria = criteria.ExcludeOnMatch();
                    for (var j = 0; j < endpoint.ResponseHeaders.Length; j++)
                    {
                        var header = endpoint.ResponseHeaders[j];
                        if (MeetsCriteria(criteria, header.Name, header.Value))
                        {
                            meetsHeaderCriteria = !criteria.ExcludeOnMatch();
                            break;
                        }
                    }
                    if (!meetsHeaderCriteria)
                    {
                        failingCriteria = criteria;
                        return false;
                    }
                    break;
                case "Standalone":
                    if (asset == null)
                    {
                        failingCriteria = criteria;
                        return false;
                    }
                    var path = asset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);
                    var route = asset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);
                    if (!string.Equals(route, path, StringComparison.OrdinalIgnoreCase) || criteria.ExcludeOnMatch())
                    {
                        failingCriteria = criteria;
                        return false;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown criteria type {criteria.Type}");
            }
        }

        failingCriteria = null;
        return true;
    }

    private static bool MeetsCriteria(
        FilterCriteria criteria,
        string name, string value) =>
            string.Equals(name, criteria.Name, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(criteria.Value) || string.Equals(value, criteria.Value, StringComparison.Ordinal));

    private class FilterCriteria(string type, string name, string value, string mode)
    {
        public string Type { get; } = type;
        public string Name { get; } = name;
        public string Value { get; } = value;
        public string Mode { get; } = mode ?? "Include";

        public bool ExcludeOnMatch() => string.Equals(Mode, "Exclude", StringComparison.OrdinalIgnoreCase);

        public static FilterCriteria FromTaskItem(ITaskItem item)
        {
            return new FilterCriteria(
                item.ItemSpec,
                item.GetMetadata("Name"),
                item.GetMetadata("Value"),
                item.GetMetadata("Mode"));
        }
    }
}
