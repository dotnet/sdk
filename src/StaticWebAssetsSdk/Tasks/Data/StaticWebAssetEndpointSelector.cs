// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

public class StaticWebAssetEndpointSelector : IEquatable<StaticWebAssetEndpointSelector>
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
}
