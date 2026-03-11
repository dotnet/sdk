// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpoint : IEquatable<StaticWebAssetEndpoint>, IComparable<StaticWebAssetEndpoint>, ITaskItem2
{
    private ITaskItem _originalItem;
    private StaticWebAssetEndpointProperty[] _endpointProperties;
    private StaticWebAssetEndpointResponseHeader[] _responseHeaders;
    private StaticWebAssetEndpointSelector[] _selectors;
    private string _assetFile;
    private string _route;
    private bool _modified;
    private string _selectorsString;
    private bool _selectorsModified;
    private string _responseHeadersString;
    private bool _responseHeadersModified;
    private string _endpointPropertiesString;
    private bool _endpointPropertiesModified;
    private Dictionary<string, string> _additionalCustomMetadata;

    // Route as it should be registered in the routing table.
    public string Route
    {
        get
        {
            _route ??= _originalItem?.ItemSpec;
            return _route;
        }

        set
        {
            _route = value;
            _modified = true;
        }
    }

    // Path to the file system as provided by static web assets (BasePath + RelativePath).
    public string AssetFile
    {
        get
        {
            _assetFile ??= _originalItem?.GetMetadata(nameof(AssetFile));
            return _assetFile;
        }

        set
        {
            _assetFile = value;
            _modified = true;
        }
    }

    private string SelectorsString
    {
        get
        {
            _selectorsString ??= _originalItem?.GetMetadata(nameof(Selectors));
            return _selectorsString;
        }
    }

    // Request values that must be compatible for the file to be selected.
    public StaticWebAssetEndpointSelector[] Selectors
    {
        get
        {
            _selectors ??= StaticWebAssetEndpointSelector.FromMetadataValue(SelectorsString);
            return _selectors;
        }

        set
        {
            Array.Sort(value);
            _selectors = value;
            _selectorsModified = true;
            _modified = true;
        }
    }

    private string ResponseHeadersString
    {
        get
        {
            _responseHeadersString ??= _originalItem?.GetMetadata(nameof(ResponseHeaders));
            return _responseHeadersString;
        }
    }

    // Response headers that must be added to the response.
    public StaticWebAssetEndpointResponseHeader[] ResponseHeaders
    {
        get
        {
            _responseHeaders ??= StaticWebAssetEndpointResponseHeader.FromMetadataValue(ResponseHeadersString);
            return _responseHeaders;
        }
        set
        {
            Array.Sort(value);
            _responseHeaders = value;
            _responseHeadersModified = true;
            _modified = true;
        }
    }

    private string EndpointPropertiesString
    {
        get
        {
            _endpointPropertiesString ??= _originalItem?.GetMetadata(nameof(EndpointProperties));
            return _endpointPropertiesString;
        }
    }

    // Properties associated with the endpoint.
    public StaticWebAssetEndpointProperty[] EndpointProperties
    {
        get
        {
            _endpointProperties ??= StaticWebAssetEndpointProperty.FromMetadataValue(EndpointPropertiesString);
            return _endpointProperties;
        }
        set
        {
            Array.Sort(value);
            _endpointProperties = value;
            _endpointPropertiesModified = true;
            _modified = true;
        }
    }

    internal void MarkProperiesAsModified()
    {
        _modified = true;
        _endpointPropertiesModified = true;
    }

    public static IEqualityComparer<StaticWebAssetEndpoint> RouteAndAssetComparer { get; } = new RouteAndAssetEqualityComparer();

    internal static IDictionary<string, List<StaticWebAssetEndpoint>> ToAssetFileDictionary(ITaskItem[] candidateEndpoints)
    {
        var result = new Dictionary<string, List<StaticWebAssetEndpoint>>(candidateEndpoints.Length / 2);

        foreach (var candidate in candidateEndpoints)
        {
            var endpoint = FromTaskItem(candidate);
            var assetFile = endpoint.AssetFile;
            if (!result.TryGetValue(assetFile, out var endpoints))
            {
                endpoints = new List<StaticWebAssetEndpoint>(5);
                result[assetFile] = endpoints;
            }
            endpoints.Add(endpoint);
        }

        return result;
    }

    public static StaticWebAssetEndpoint[] FromItemGroup(ITaskItem[] endpoints)
    {
        var result = new StaticWebAssetEndpoint[endpoints.Length];
        for (var i = 0; i < endpoints.Length; i++)
        {
            result[i] = FromTaskItem(endpoints[i]);
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
            _originalItem = item,
        };

        return result;
    }

    public static ITaskItem[] ToTaskItems(ICollection<StaticWebAssetEndpoint> endpoints)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return [];
        }

        var endpointItems = new ITaskItem[endpoints.Count];
        var i = 0;
        foreach (var endpoint in endpoints)
        {
            endpointItems[i++] = endpoint.ToTaskItem();
        }

        return endpointItems;
    }

    public ITaskItem ToTaskItem()
    {
        if (!_modified && _originalItem != null)
        {
            return _originalItem;
        }

        // If we're implementing ITaskItem2, we can just return this instance
        return this;
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

        if (Selectors.Length > other.Selectors.Length)
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

        if (EndpointProperties.Length > other.EndpointProperties.Length)
        {
            return 1;
        }
        else if (EndpointProperties.Length < other.EndpointProperties.Length)
        {
            return -1;
        }

        for (var i = 0; i < EndpointProperties.Length; i++)
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

    internal static ITaskItem[] ToTaskItems(ConcurrentBag<StaticWebAssetEndpoint> endpoints)
    {
        if (endpoints == null || endpoints.IsEmpty)
        {
            return [];
        }

        var endpointItems = new ITaskItem[endpoints.Count];
        var i = 0;
        foreach (var endpoint in endpoints)
        {
            endpointItems[i++] = endpoint.ToTaskItem();
        }

        return endpointItems;
    }

    private sealed class RouteAndAssetEqualityComparer : IEqualityComparer<StaticWebAssetEndpoint>
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

    #region ITaskItem2 implementation

    string ITaskItem2.EvaluatedIncludeEscaped { get => Route; set => Route = value; }
    string ITaskItem.ItemSpec { get => Route; set => Route = value; }

    private static readonly string[] _defaultPropertyNames = [
        nameof(AssetFile),
        nameof(Selectors),
        nameof(ResponseHeaders),
        nameof(EndpointProperties)
    ];

    ICollection ITaskItem.MetadataNames
    {
        get
        {
            if (_additionalCustomMetadata == null)
            {
                return _defaultPropertyNames;
            }

            var result = new List<string>(_defaultPropertyNames.Length + _additionalCustomMetadata.Count);
            result.AddRange(_defaultPropertyNames);

            foreach (var kvp in _additionalCustomMetadata)
            {
                result.Add(kvp.Key);
            }

            return result;
        }
    }

    int ITaskItem.MetadataCount => _defaultPropertyNames.Length + (_additionalCustomMetadata?.Count ?? 0);

    string ITaskItem2.GetMetadataValueEscaped(string metadataName)
    {
        return metadataName switch
        {
            nameof(AssetFile) => AssetFile ?? "",
            nameof(Selectors) => !_selectorsModified ? SelectorsString ?? "" : StaticWebAssetEndpointSelector.ToMetadataValue(Selectors),
            nameof(ResponseHeaders) => !_responseHeadersModified ? ResponseHeadersString ?? "" : StaticWebAssetEndpointResponseHeader.ToMetadataValue(ResponseHeaders),
            nameof(EndpointProperties) => !_endpointPropertiesModified ? EndpointPropertiesString ?? "" : StaticWebAssetEndpointProperty.ToMetadataValue(EndpointProperties),
            _ => _additionalCustomMetadata?.TryGetValue(metadataName, out var value) == true ? (value ?? "") : "",
        };
    }

    void ITaskItem2.SetMetadataValueLiteral(string metadataName, string metadataValue)
    {
        metadataValue ??= "";
        switch (metadataName)
        {
            case nameof(AssetFile):
                AssetFile = metadataValue;
                break;
            case nameof(Selectors):
                _selectorsString = metadataValue;
                _selectors = null;
                _selectorsModified = false;
                break;
            case nameof(ResponseHeaders):
                _responseHeadersString = metadataValue;
                _responseHeaders = null;
                _responseHeadersModified = false;
                break;
            case nameof(EndpointProperties):
                _endpointPropertiesString = metadataValue;
                _endpointProperties = null;
                _endpointPropertiesModified = false;
                break;
            default:
                _additionalCustomMetadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _additionalCustomMetadata[metadataName] = metadataValue;
                break;
        }
        _modified = true;
    }

    IDictionary ITaskItem2.CloneCustomMetadataEscaped()
    {
        var result = new Dictionary<string, string>(((ITaskItem)this).MetadataCount)
        {
            { nameof(AssetFile), AssetFile ?? "" },
            { nameof(Selectors), !_selectorsModified ? SelectorsString ?? "" : StaticWebAssetEndpointSelector.ToMetadataValue(Selectors) },
            { nameof(ResponseHeaders), !_responseHeadersModified ? ResponseHeadersString ?? "" : StaticWebAssetEndpointResponseHeader.ToMetadataValue(ResponseHeaders) },
            { nameof(EndpointProperties), !_endpointPropertiesModified ? EndpointPropertiesString ?? "" : StaticWebAssetEndpointProperty.ToMetadataValue(EndpointProperties) }
        };

        if (_additionalCustomMetadata != null)
        {
            foreach (var kvp in _additionalCustomMetadata)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    string ITaskItem.GetMetadata(string metadataName) => ((ITaskItem2)this).GetMetadataValueEscaped(metadataName);

    void ITaskItem.SetMetadata(string metadataName, string metadataValue) => ((ITaskItem2)this).SetMetadataValueLiteral(metadataName, metadataValue);

    void ITaskItem.RemoveMetadata(string metadataName) => _additionalCustomMetadata?.Remove(metadataName);

    void ITaskItem.CopyMetadataTo(ITaskItem destinationItem)
    {
        destinationItem.SetMetadata(nameof(AssetFile), AssetFile ?? "");
        destinationItem.SetMetadata(nameof(Selectors), !_selectorsModified ? SelectorsString ?? "" : StaticWebAssetEndpointSelector.ToMetadataValue(Selectors));
        destinationItem.SetMetadata(nameof(ResponseHeaders), !_responseHeadersModified ? ResponseHeadersString ?? "" : StaticWebAssetEndpointResponseHeader.ToMetadataValue(ResponseHeaders));
        destinationItem.SetMetadata(nameof(EndpointProperties), !_endpointPropertiesModified ? EndpointPropertiesString ?? "" : StaticWebAssetEndpointProperty.ToMetadataValue(EndpointProperties));

        if (_additionalCustomMetadata != null)
        {
            foreach (var kvp in _additionalCustomMetadata)
            {
                destinationItem.SetMetadata(kvp.Key, kvp.Value ?? "");
            }
        }
    }

    IDictionary ITaskItem.CloneCustomMetadata() => ((ITaskItem2)this).CloneCustomMetadataEscaped();

    #endregion
}
