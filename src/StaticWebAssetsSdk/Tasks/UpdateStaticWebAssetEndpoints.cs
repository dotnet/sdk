// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Updates the given endpoints to update based on the provided operations
// The operations are:
// - Append: Appends the provided value to one of the lists of properties, headers, selectors of the endpoint
// - Remove: Removes the first matching element from the list of properties, headers, selectors. If multiple values
//   match, only the first one is removed. If the value is not found, the operation is a no-op. If a value is specified,
//   only the matching name and value is removed.
// - Replace: Replaces the first matching element from the list of properties, headers, selectors. If multiple values
//   match, only the first one is replaced. If the value is not found, the operation is a no-op. If a value is specified,
//   only the matching name and value is replaced.
// - RemoveAll: Removes all the properties, headers, selectors that match the provided name.
// Operations are applied in order, so you can RemoveAll and Add multiple times to replace lists of elements
// AllEndpoints needs to be provided to ensure that we add other endpoints with the same route to one of the updated endpoints
// list to the final list of updated endpoints, since we'll remove those from an itemgroup when we remove the ones that we're updating
// to add them later.
// After invoking this task, the following needs to happen:
// <ItemGroup>
//     <StaticWebAssetEndpoint Remove="@(_UpdatedEndpoints)" />
//     <StaticWebAssetEndpoint Include="@(_UpdatedEndpoints)" />
// </ItemGroup>
// This will remove the updated endpoints from the original list and add the updated ones back in. Including any endpoint that might
// have been removed because it had the same route as one of the updated endpoints.

public class UpdateStaticWebAssetEndpoints : Task
{
    [Required] public ITaskItem[] EndpointsToUpdate { get; set; }

    [Required] public ITaskItem[] AllEndpoints { get; set; }

    [Required] public ITaskItem[] Operations { get; set; }

    [Output] public ITaskItem[] UpdatedEndpoints { get; set; }

    public override bool Execute()
    {
        var endpointsToUpdate = StaticWebAssetEndpoint.FromItemGroup(EndpointsToUpdate)
            .GroupBy(e => e.Route)
            .ToDictionary(e => e.Key, e => e.ToHashSet());
        var allEndpoints = StaticWebAssetEndpoint.FromItemGroup(AllEndpoints)
            .GroupBy(e => e.Route)
            .ToDictionary(e => e.Key, e => e.ToHashSet());

        var operations = Operations.Select(StaticWebAssetEndpointOperation.FromTaskItem).ToArray();
        var result = new List<StaticWebAssetEndpoint>();

        // Iterate over all the groups of endpoints that need to be updated
        // If we find a matching endpoint in the allEndpoints list, (which we should), we'll remove it from the list
        // and add the updated endpoint to the result list.
        // After we are done processing a group of endpoints, we'll add the remaining endpoints in the allEndpoints list
        // to the result list. Those are the endpoints that even though aren't updated, would get removed if we remove the
        // updated endpoints from an itemgroup to add the updated ones back in.
        foreach (var kvp in endpointsToUpdate)
        {
            var route = kvp.Key;
            var endpointGroup = kvp.Value;
            var mustAddGroup = false;
            foreach (var endpoint in endpointGroup)
            {
                allEndpoints[route].TryGetValue(endpoint, out var oldEndpoint);
                if (TryUpdateEndpoint(endpoint, operations, result))
                {
                    mustAddGroup = true;
                    allEndpoints[route].Remove(oldEndpoint);
                }
            }
            if (mustAddGroup)
            {
                result.AddRange(allEndpoints[route]);
            }
        }

        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(result);

        return !Log.HasLoggedErrors;
    }

    private bool TryUpdateEndpoint(StaticWebAssetEndpoint endpoint, StaticWebAssetEndpointOperation[] operations, List<StaticWebAssetEndpoint> result)
    {
        var updated = false;
        for (var i = 0; i < operations.Length; i++)
        {
            var operation = operations[i];
            switch (operation.Type)
            {
                case "Append":
                    AppendToEndpoint(endpoint, operation);
                    updated = true;
                    break;
                case "Remove":
                    updated |= RemoveFromEndpoint(endpoint, operation);
                    break;
                case "Replace":
                    updated |= ReplaceInEndpoint(endpoint, operation);
                    break;
                case "RemoveAll":
                    updated |= RemoveAllFromEndpoint(endpoint, operation);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operation {operation.Type}");
            }
        }

        if (updated)
        {
            result.Add(endpoint);
        }

        return updated;
    }

    private bool RemoveAllFromEndpoint(StaticWebAssetEndpoint endpoint, StaticWebAssetEndpointOperation operation)
    {
        switch (operation.Target)
        {
            case "Selector":
                var (selectors, selectorRemoved) = RemoveAllIfFound(endpoint.Selectors, s => s.Name, s => s.Value, operation.Name, operation.Value);
                if (selectorRemoved)
                {
                    endpoint.Selectors = selectors;
                    return true;
                }
                break;
            case "Header":
                var (headers, headerRemoved) = RemoveAllIfFound(endpoint.ResponseHeaders, h => h.Name, h => h.Value, operation.Name, operation.Value);
                if (headerRemoved)
                {
                    endpoint.ResponseHeaders = headers;
                    return true;
                }
                break;
            case "Property":
                var (properties, propertyRemoved) = RemoveAllIfFound(endpoint.EndpointProperties, p => p.Name, p => p.Value, operation.Name, operation.Value);
                if (propertyRemoved)
                {
                    endpoint.EndpointProperties = properties;
                    return true;
                }
                break;
            default:
                throw new InvalidOperationException($"Unknown target {operation.Target}");
        }

        return false;
    }

    private (T[], bool replaced) RemoveAllIfFound<T>(T[] elements, Func<T, string> getName, Func<T, string> getValue, string name, string value)
    {
        List<T> selectors = null;
        for (var i = 0; i < elements.Length; i++)
        {
            if (string.Equals(getName(elements[i]), name, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(value) || string.Equals(getValue(elements[i]), value, StringComparison.Ordinal)))
            {
                if (selectors == null)
                {
                    selectors = [];
                    for (var j = 0; j < i; j++)
                    {
                        selectors.Add(elements[j]);
                    }
                }
            }
            else
            {
                selectors?.Add(elements[i]);
            }
        }
        if (selectors != null)
        {
            return ([.. selectors], true);
        }

        return (elements, false);
    }

    private (T[], bool replaced) RemoveFirstIfFound<T>(T[] elements, Func<T, string> getName, Func<T, string> getValue, string name, string value)
    {
        for (var i = 0; i < elements.Length; i++)
        {
            if (string.Equals(getName(elements[i]), name, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(value) || string.Equals(getValue(elements[i]), value, StringComparison.Ordinal)))
            {
                var prefix = elements.Take(i);
                var suffix = prefix.Skip(1);
                return ([.. prefix, .. suffix], true);
            }
        }

        return (elements, false);
    }

    private bool ReplaceInEndpoint(StaticWebAssetEndpoint endpoint, StaticWebAssetEndpointOperation operation)
    {
        switch (operation.Target)
        {
            case "Selector":
                var (selectors, selectorReplaced) = ReplaceFirstIfFound(
                    endpoint.Selectors,
                    s => s.Name,
                    s => s.Value,
                    (name, value) => new StaticWebAssetEndpointSelector { Name = name, Value = value, Quality = operation.Quality },
                    operation.Name,
                    operation.Value,
                    operation.NewValue);
                if (selectorReplaced)
                {
                    endpoint.Selectors = selectors;
                    return true;
                }
                break;
            case "Header":
                var (headers, headerReplaced) = ReplaceFirstIfFound(
                    endpoint.ResponseHeaders,
                    h => h.Name,
                    h => h.Value,
                    (name, value) => new StaticWebAssetEndpointResponseHeader { Name = name, Value = value },
                    operation.Name,
                    operation.Value,
                    operation.NewValue);
                if (headerReplaced)
                {
                    endpoint.ResponseHeaders = headers;
                    return true;
                }
                break;
            case "Property":
                var (properties, propertyReplaced) = ReplaceFirstIfFound(
                    endpoint.EndpointProperties,
                    p => p.Name,
                    p => p.Value,
                    (name, value) => new StaticWebAssetEndpointProperty { Name = name, Value = value },
                    operation.Name,
                    operation.Value,
                    operation.NewValue);
                if (propertyReplaced)
                {
                    endpoint.EndpointProperties = properties;
                    return true;
                }
                break;
            default:
                throw new InvalidOperationException($"Unknown target {operation.Target}");
        }

        return false;
    }

    private (T[], bool replaced) ReplaceFirstIfFound<T>(
        T[] elements,
        Func<T, string> getName,
        Func<T, string> getValue,
        Func<string, string, T> createNew,
        string name, string value, string newValue)
    {
        for (var i = 0; i < elements.Length; i++)
        {
            if (string.Equals(getName(elements[i]), name, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(value) || string.Equals(getValue(elements[i]), value, StringComparison.Ordinal)))
            {
                var prefix = elements.Take(i);
                var suffix = elements.Skip(i + 1);
                return ([.. prefix, createNew(name, newValue), .. suffix], true);
            }
        }

        return (elements, false);
    }

    private bool RemoveFromEndpoint(StaticWebAssetEndpoint endpoint, StaticWebAssetEndpointOperation operation)
    {
        switch (operation.Target)
        {
            case "Selector":
                var (selectors, selectorRemoved) = RemoveFirstIfFound(endpoint.Selectors, s => s.Name, s => s.Value, operation.Name, operation.Value);
                if (selectorRemoved)
                {
                    endpoint.Selectors = selectors;
                    return true;
                }
                break;
            case "Header":
                var (headers, headerRemoved) = RemoveFirstIfFound(endpoint.ResponseHeaders, h => h.Name, h => h.Value, operation.Name, operation.Value);
                if (headerRemoved)
                {
                    endpoint.ResponseHeaders = headers;
                    return true;
                }
                break;
            case "Property":
                var (properties, propertyRemoved) = RemoveFirstIfFound(endpoint.EndpointProperties, p => p.Name, p => p.Value, operation.Name, operation.Value);
                if (propertyRemoved)
                {
                    endpoint.EndpointProperties = properties;
                    return true;
                }
                break;
            default:
                throw new InvalidOperationException($"Unknown target {operation.Target}");
        }

        return false;
    }

    private static void AppendToEndpoint(StaticWebAssetEndpoint endpoint, StaticWebAssetEndpointOperation operation)
    {
        switch (operation.Target)
        {
            case "Selector":
                endpoint.Selectors = [
                    ..endpoint.Selectors,
                                    new StaticWebAssetEndpointSelector
                                    {
                                        Name = operation.Name,
                                        Value = operation.Value,
                                        Quality = operation.Quality
                                    }];
                break;
            case "Header":
                endpoint.ResponseHeaders = [
                    ..endpoint.ResponseHeaders,
                                    new StaticWebAssetEndpointResponseHeader
                                    {
                                        Name = operation.Name,
                                        Value = operation.Value
                                    }];
                break;
            case "Property":
                endpoint.EndpointProperties = [
                    ..endpoint.EndpointProperties,
                                    new StaticWebAssetEndpointProperty
                                    {
                                        Name = operation.Name,
                                        Value = operation.Value
                                    }];
                break;
            default:
                throw new InvalidOperationException($"Unknown target {operation.Target}");
        }
    }

    private class StaticWebAssetEndpointOperation(string type, string target, string name, string value, string newValue, string quality)
    {
        public string Type { get; } = type;

        public string Target { get; } = target;

        public string Name { get; } = name;

        public string Value { get; } = value;

        public string NewValue { get; } = newValue;

        public string Quality { get; } = quality;

        public static StaticWebAssetEndpointOperation FromTaskItem(ITaskItem item)
        {
            return new StaticWebAssetEndpointOperation(
                item.ItemSpec,
                item.GetMetadata("UpdateTarget"),
                item.GetMetadata("Name"),
                item.GetMetadata("Value"),
                item.GetMetadata("NewValue"),
                item.GetMetadata("Quality"));
        }
    }
}
