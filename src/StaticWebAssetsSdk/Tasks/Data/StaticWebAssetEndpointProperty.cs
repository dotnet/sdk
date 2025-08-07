// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpointProperty : IComparable<StaticWebAssetEndpointProperty>, IEquatable<StaticWebAssetEndpointProperty>
{
    public string Name { get; set; }

    public string Value { get; set; }

    internal static StaticWebAssetEndpointProperty[] FromMetadataValue(string value)
    {
        return string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<StaticWebAssetEndpointProperty[]>(value);
    }

    internal static string ToMetadataValue(StaticWebAssetEndpointProperty[] responseHeaders)
    {
        return JsonSerializer.Serialize(responseHeaders ?? []);
    }

    public int CompareTo(StaticWebAssetEndpointProperty other)
    {
        return string.Compare(Name, other.Name, StringComparison.Ordinal) switch
        {
            0 => string.Compare(Value, other.Value, StringComparison.Ordinal),
            int result => result
        };
    }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetEndpointProperty);

    public bool Equals(StaticWebAssetEndpointProperty other) => other is not null && Name == other.Name && Value == other.Value;

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

    private string GetDebuggerDisplay() => $"Name: {Name}, Value: {Value}";
}
