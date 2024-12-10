// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetSegmentPart : IEquatable<StaticWebAssetSegmentPart>
{
    public ReadOnlyMemory<char> Name { get; set; }

    public ReadOnlyMemory<char> Value { get; set; }

    public bool IsLiteral { get; set; }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetSegmentPart);

    public bool Equals(StaticWebAssetSegmentPart other) => other is not null &&
        IsLiteral == other.IsLiteral &&
        Name.Span.SequenceEqual(other.Name.Span) &&
        Value.Span.SequenceEqual(other.Value.Span);

#if NET47_OR_GREATER
    public override int GetHashCode()
    {
        var hashCode = -62096114;
        hashCode = (hashCode * -1521134295) + GetSpanHashCode(Name);
        hashCode = (hashCode * -1521134295) + GetSpanHashCode(Value);
        hashCode = (hashCode * -1521134295) + IsLiteral.GetHashCode();
        return hashCode;
    }

    private int GetSpanHashCode(ReadOnlyMemory<char> memory)
    {
        var hashCode = -62096114;
        var span = memory.Span;
        for ( var i = 0; i < span.Length; i++)
        {
            hashCode = (hashCode * -1521134295) + span[i].GetHashCode();
        }

        return hashCode;
    }
#else
    public override int GetHashCode() => HashCode.Combine(Name, Value, IsLiteral);
#endif

    public static bool operator ==(StaticWebAssetSegmentPart left, StaticWebAssetSegmentPart right) => EqualityComparer<StaticWebAssetSegmentPart>.Default.Equals(left, right);
    public static bool operator !=(StaticWebAssetSegmentPart left, StaticWebAssetSegmentPart right) => !(left == right);

    private string GetDebuggerDisplay() => IsLiteral ? Value.ToString() : $"{{{Name}}}";
}
