﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpointSelector : IEquatable<StaticWebAssetEndpointSelector>, IComparable<StaticWebAssetEndpointSelector>
{
    public string Name { get; set; }

    public string Value { get; set; }

    public string Quality { get; set; }

    public static StaticWebAssetEndpointSelector[] FromMetadataValue(string value)
    {
        return string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<StaticWebAssetEndpointSelector[]>(value);
    }

    public static string ToMetadataValue(StaticWebAssetEndpointSelector[] selectors)
    {
        return JsonSerializer.Serialize(selectors ?? []);
    }

    public int CompareTo(StaticWebAssetEndpointSelector other)
    {
        if (other is null)
        {
            return 1;
        }

        var nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        var valueComparison = string.Compare(Value, other.Value, StringComparison.Ordinal);
        if (valueComparison != 0)
        {
            return valueComparison;
        }

        return 0;
    }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetEndpointSelector);

    public bool Equals(StaticWebAssetEndpointSelector other) => other is not null && Name == other.Name && Value == other.Value && Quality == other.Quality;

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
