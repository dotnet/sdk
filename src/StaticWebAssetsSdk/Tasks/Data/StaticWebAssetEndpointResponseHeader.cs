// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

public class StaticWebAssetEndpointResponseHeader : IComparable<StaticWebAssetEndpointResponseHeader>, IEquatable<StaticWebAssetEndpointResponseHeader>
{
    public string Name { get; set; }

    public string Value { get; set; }

    internal static StaticWebAssetEndpointResponseHeader[] FromMetadataValue(string value)
    {
        return JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(value);
    }

    internal static string ToMetadataValue(StaticWebAssetEndpointResponseHeader[] responseHeaders)
    {
        return JsonSerializer.Serialize(responseHeaders);
    }

    public int CompareTo(StaticWebAssetEndpointResponseHeader other)
    {
        return string.Compare(Name, other.Name, StringComparison.Ordinal) switch
        {
            0 => string.Compare(Value, other.Value, StringComparison.Ordinal),
            int result => result
        };
    }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetEndpointResponseHeader);

    public bool Equals(StaticWebAssetEndpointResponseHeader other) => other is not null && Name == other.Name && Value == other.Value;

    public override int GetHashCode()
    {
#if NET472_OR_GREATER
        var hashCode = -244751520;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Value);
        return hashCode;
#else
        return HashCode.Combine(Name, Value);
#endif
    }
}
