// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct StaticWebAssetEndpointSelector : IEquatable<StaticWebAssetEndpointSelector>, IComparable<StaticWebAssetEndpointSelector>
{
    private static readonly JsonTypeInfo<StaticWebAssetEndpointSelector[]> _jsonTypeInfo =
        StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointSelectorArray;

    public string Name { get; set; }

    public string Value { get; set; }

    public string Quality { get; set; }

    public static StaticWebAssetEndpointSelector[] FromMetadataValue(string value) => string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize(value, _jsonTypeInfo);

    public static string ToMetadataValue(StaticWebAssetEndpointSelector[] selectors) =>
        JsonSerializer.Serialize(
            selectors ?? [],
            _jsonTypeInfo);

    public int CompareTo(StaticWebAssetEndpointSelector other)
    {
        var nameComparison = string.CompareOrdinal(Name, other.Name);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        var valueComparison = string.CompareOrdinal(Value, other.Value);
        if (valueComparison != 0)
        {
            return valueComparison;
        }

        return 0;
    }

    public override bool Equals(object obj) => obj is StaticWebAssetEndpointSelector endpointSelector &&
        Equals(endpointSelector);

    public bool Equals(StaticWebAssetEndpointSelector other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal) &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode()
    {
#if NET471_OR_GREATER
        var hashCode = 1379895590;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Value);
        hashCode = hashCode * -1521134295 + Quality.GetHashCode();
        return hashCode;
#else
        return HashCode.Combine(Name, Value, Quality);
#endif
    }

    private string GetDebuggerDisplay() => $"Name: {Name}, Value: {Value}, Quality: {Quality}";
}
