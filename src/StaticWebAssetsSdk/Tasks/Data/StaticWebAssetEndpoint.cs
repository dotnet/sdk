// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpoint : IEquatable<StaticWebAssetEndpoint>, IComparable<StaticWebAssetEndpoint>
{
    // Route as it should be registered in the routing table.
    public string Route { get; set; }

    // Path to the file system as provided by static web assets (BasePath + RelativePath).
    public string AssetFile { get; set; }

    // Request values that must be compatible for the file to be selected.
    public StaticWebAssetEndpointSelector[] Selectors { get; set; } = [];

    // Response headers that must be added to the response.
    public StaticWebAssetEndpointResponseHeader[] ResponseHeaders { get; set; } = [];

    // Properties associated with the endpoint.
    public StaticWebAssetEndpointProperty[] EndpointProperties { get; set; } = [];

    public static IEqualityComparer<StaticWebAssetEndpoint> RouteAndAssetComparer { get; } = new RouteAndAssetEqualityComparer();

    public static StaticWebAssetEndpoint[] FromItemGroup(ITaskItem[] endpoints)
    {
        var result = new StaticWebAssetEndpoint[endpoints.Length];
        for (var i = 0; i < endpoints.Length; i++)
        {
            result[i] = FromTaskItem(endpoints[i]);
            Array.Sort(result[i].ResponseHeaders);
            Array.Sort(result[i].Selectors);
            Array.Sort(result[i].EndpointProperties);
        }

        Array.Sort(result, (a, b) => (a.Route, b.Route) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            var (x, y) => string.Compare(x, y, StringComparison.Ordinal) switch
            {
                0 => string.Compare(a.AssetFile, b.AssetFile, StringComparison.Ordinal),
                int result => result
            }
        });

        return result;
    }

    public static StaticWebAssetEndpoint FromTaskItem(ITaskItem item)
    {
        var result = new StaticWebAssetEndpoint()
        {
            Route = item.ItemSpec,
            AssetFile = item.GetMetadata(nameof(AssetFile)),
            Selectors = StaticWebAssetEndpointSelector.FromMetadataValue(item.GetMetadata(nameof(Selectors))),
            ResponseHeaders = StaticWebAssetEndpointResponseHeader.FromMetadataValue(item.GetMetadata(nameof(ResponseHeaders))),
            EndpointProperties = StaticWebAssetEndpointProperty.FromMetadataValue(item.GetMetadata(nameof(EndpointProperties)))
        };

        return result;
    }

    public static ITaskItem[] ToTaskItems(IList<StaticWebAssetEndpoint> endpoints)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return [];
        }

        var endpointItems = new ITaskItem[endpoints.Count];
        for (var i = 0; i < endpoints.Count; i++)
        {
            endpointItems[i] = endpoints[i].ToTaskItem();
        }

        return endpointItems;
    }

    public TaskItem ToTaskItem()
    {
        var item = new TaskItem(Route);
        item.SetMetadata(nameof(AssetFile), AssetFile);
        item.SetMetadata(nameof(Selectors), StaticWebAssetEndpointSelector.ToMetadataValue(Selectors));
        item.SetMetadata(nameof(ResponseHeaders), StaticWebAssetEndpointResponseHeader.ToMetadataValue(ResponseHeaders));
        item.SetMetadata(nameof(EndpointProperties), StaticWebAssetEndpointProperty.ToMetadataValue(EndpointProperties));
        return item;
    }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetEndpoint);

    public bool Equals(StaticWebAssetEndpoint other) => other is not null && Route == other.Route &&
        AssetFile == other.AssetFile &&
        Selectors.SequenceEqual(other.Selectors) &&
        ResponseHeaders.SequenceEqual(other.ResponseHeaders) &&
        EndpointProperties.SequenceEqual(other.EndpointProperties);

    public override int GetHashCode()
    {
#if NET472_OR_GREATER
        var hashCode = -604019124;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Route);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetFile);
        for (var i = 0; i < Selectors.Length; i++)
        {
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAssetEndpointSelector>.Default.GetHashCode(Selectors[i]);
        }
        for (var i = 0; i < ResponseHeaders.Length; i++)
        {
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAssetEndpointResponseHeader>.Default.GetHashCode(ResponseHeaders[i]);
        }
        for (var i = 0; i < EndpointProperties.Length; i++)
        {
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAssetEndpointProperty>.Default.GetHashCode(EndpointProperties[i]);
        }
        return hashCode;
#else
        var hashCode = new HashCode();
        hashCode.Add(Route);
        hashCode.Add(AssetFile);
        for (var i = 0; i < Selectors.Length; i++)
        {
            hashCode.Add(Selectors[i]);
        }
        for (var i = 0; i < ResponseHeaders.Length; i++)
        {
            hashCode.Add(ResponseHeaders[i]);
        }
        for (var i = 0; i < EndpointProperties.Length; i++)
        {
            hashCode.Add(EndpointProperties[i]);
        }
        return hashCode.ToHashCode();
#endif
    }

    private string GetDebuggerDisplay() =>
        $"{nameof(StaticWebAssetEndpoint)}: Route = {Route}, AssetFile = {AssetFile}, Selectors = {StaticWebAssetEndpointSelector.ToMetadataValue(Selectors ?? [])}, ResponseHeaders = {ResponseHeaders?.Length}, EndpointProperties = {StaticWebAssetEndpointProperty.ToMetadataValue(EndpointProperties ?? [])}";

    public int CompareTo(StaticWebAssetEndpoint other)
    {
        var routeComparison = StringComparer.Ordinal.Compare(Route, Route);
        if (routeComparison != 0)
        {
            return routeComparison;
        }

        var assetFileComparison = StringComparer.Ordinal.Compare(AssetFile, other.AssetFile);
        if (assetFileComparison != 0)
        {
            return assetFileComparison;
        }

        if(Selectors.Length > other.Selectors.Length)
        {
            return 1;
        }
        else if (Selectors.Length < other.Selectors.Length)
        {
            return -1;
        }

        for (var i = 0; i < Selectors.Length; i++)
        {
            var selectorComparison = Selectors[i].Name.CompareTo(other.Selectors[i].Name);
            if (selectorComparison != 0)
            {
                return selectorComparison;
            }

            selectorComparison = Selectors[i].Value.CompareTo(other.Selectors[i].Value);
            if (selectorComparison != 0)
            {
                return selectorComparison;
            }
        }

        if(EndpointProperties.Length > other.EndpointProperties.Length)
        {
            return 1;
        }
        else if (EndpointProperties.Length < other.EndpointProperties.Length)
        {
            return -1;
        }

        for (var i = 0; i < EndpointProperties.Length;i++)
        {
            var propertyComparison = EndpointProperties[i].Name.CompareTo(other.EndpointProperties[i].Name);
            if (propertyComparison != 0)
            {
                return propertyComparison;
            }

            propertyComparison = EndpointProperties[i].Value.CompareTo(other.EndpointProperties[i].Value);
            if (propertyComparison != 0)
            {
                return propertyComparison;
            }
        }

        if (ResponseHeaders.Length > other.ResponseHeaders.Length)
        {
            return 1;
        }
        else if (ResponseHeaders.Length < other.ResponseHeaders.Length)
        {
            return -1;
        }

        for (var i = 0; i < ResponseHeaders.Length; i++)
        {
            var responseHeaderComparison = ResponseHeaders[i].Name.CompareTo(other.ResponseHeaders[i].Name);
            if (responseHeaderComparison != 0)
            {
                return responseHeaderComparison;
            }

            responseHeaderComparison = ResponseHeaders[i].Value.CompareTo(other.ResponseHeaders[i].Value);
            if (responseHeaderComparison != 0)
            {
                return responseHeaderComparison;
            }
        }

        return 0;
    }

    private class RouteAndAssetEqualityComparer : IEqualityComparer<StaticWebAssetEndpoint>
    {
        public bool Equals(StaticWebAssetEndpoint x, StaticWebAssetEndpoint y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Route, y.Route, StringComparison.Ordinal) &&
                string.Equals(x.AssetFile, y.AssetFile, StringComparison.Ordinal);
        }

        public int GetHashCode(StaticWebAssetEndpoint obj)
        {
#if NET472_OR_GREATER
            var hashCode = -604019124;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.Route);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.AssetFile);
            return hashCode;
#else
            return HashCode.Combine(obj.Route, obj.AssetFile);
#endif
        }
    }
}
