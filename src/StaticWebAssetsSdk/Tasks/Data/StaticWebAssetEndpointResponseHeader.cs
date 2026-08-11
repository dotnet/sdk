// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpointResponseHeader : IEquatable<StaticWebAssetEndpointResponseHeader>, IComparable<StaticWebAssetEndpointResponseHeader>
{
    public string Name { get; set; }

    public string Value { get; set; }

    internal static StaticWebAssetEndpointResponseHeader[] FromMetadataValue(string value)
    {
        return string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(value);
    }

    internal static string ToMetadataValue(StaticWebAssetEndpointResponseHeader[] responseHeaders)
    {
        return JsonSerializer.Serialize(responseHeaders ?? []);
    }

    private string GetDebuggerDisplay() => $"{Name}: {Value}";

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetEndpointResponseHeader);

    public bool Equals(StaticWebAssetEndpointResponseHeader other) => string.Equals(Name, other?.Name, StringComparison.Ordinal) && string.Equals(Value, other?.Value, StringComparison.Ordinal);

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

    public int CompareTo(StaticWebAssetEndpointResponseHeader other) => string.Compare(Name, other?.Name, StringComparison.Ordinal) switch
    {
        0 => string.Compare(Value, other?.Value, StringComparison.Ordinal),
        int result => result
    };
}
