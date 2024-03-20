// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetEndpointResponseHeader : IComparable<StaticWebAssetEndpointResponseHeader>, IEquatable<StaticWebAssetEndpointResponseHeader>
{
    public static readonly IEqualityComparer<StaticWebAssetEndpointResponseHeader> NameComparer = new NameStaticWebAssetEndpointResponseHeaderComparer();

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

    public class NameStaticWebAssetEndpointResponseHeaderComparer : IEqualityComparer<StaticWebAssetEndpointResponseHeader>
    {
        public bool Equals(StaticWebAssetEndpointResponseHeader x, StaticWebAssetEndpointResponseHeader y)
        {
            return string.Equals(x?.Name, y?.Name, StringComparison.Ordinal);
        }

        public int GetHashCode(StaticWebAssetEndpointResponseHeader obj)
        {
            return obj?.Name.GetHashCode() ?? 1;
        }
    }
}
