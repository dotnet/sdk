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

    public override bool Execute()
    {
        var filterCriteria = (Filters ?? []).Select(FilterCriteria.FromTaskItem).ToArray();
        var assetFiles = (Assets ?? []).Select(a => a.ItemSpec).ToHashSet();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);

        var filteredEndpoints = new List<StaticWebAssetEndpoint>();
        for (int i = 0; i < endpoints.Length; i++)
        {
            var endpoint = endpoints[i];
            if (Assets != null && !assetFiles.Contains(endpoint.AssetFile))
            {
                continue;
            }

            if (MeetsAllCriteria(endpoint, filterCriteria, out var failingCriteria))
            {
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
        return !Log.HasLoggedErrors;
    }

    private bool MeetsAllCriteria(StaticWebAssetEndpoint endpoint, FilterCriteria[] filterCriteria, out FilterCriteria failingCriteria)
    {
        for (int i = 0; i < filterCriteria.Length; i++)
        {
            var criteria = filterCriteria[i];
            switch (criteria.Type)
            {
                case "Property":
                    var meetsPropertyCriteria = false;
                    for (var j = 0; j < endpoint.EndpointProperties.Length; j++)
                    {
                        var property = endpoint.EndpointProperties[j];
                        if (MeetsCriteria(criteria, property.Name, property.Value))
                        {
                            meetsPropertyCriteria = true;
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
                    var meetsSelectorCriteria = false;
                    for (var j = 0; j < endpoint.Selectors.Length; j++)
                    {
                        var selector = endpoint.Selectors[j];
                        if (MeetsCriteria(criteria, selector.Name, selector.Value))
                        {
                            meetsSelectorCriteria = true;
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
                    var meetsHeaderCriteria = false;
                    for (var j = 0; j < endpoint.ResponseHeaders.Length; j++)
                    {
                        var header = endpoint.ResponseHeaders[j];
                        if (MeetsCriteria(criteria, header.Name, header.Value))
                        {
                            meetsHeaderCriteria = true;
                            break;
                        }
                    }
                    if (!meetsHeaderCriteria)
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
        string.Equals(name, criteria.Name, StringComparison.OrdinalIgnoreCase)
        && (string.IsNullOrEmpty(criteria.Value) || string.Equals(value, criteria.Value, StringComparison.Ordinal));

    private class FilterCriteria(string type, string name, string value)
    {
        public string Type { get; } = type;
        public string Name { get; } = name;
        public string Value { get; } = value;

        public static FilterCriteria FromTaskItem(ITaskItem item)
        {
            return new FilterCriteria(
                item.ItemSpec,
                item.GetMetadata("Name"),
                item.GetMetadata("Value"));
        }
    }
}
