// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetSegmentPart : IEquatable<StaticWebAssetSegmentPart>
{
    public string Name { get; set; }

    public string Value { get; set; }

    public bool IsLiteral { get; set; }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetSegmentPart);

    public bool Equals(StaticWebAssetSegmentPart other) => other is not null && Name == other.Name && Value == other.Value && IsLiteral == other.IsLiteral;

#if NET47_OR_GREATER
    public override int GetHashCode()
    {
        var hashCode = -62096114;
        hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(Value);
        hashCode = (hashCode * -1521134295) + IsLiteral.GetHashCode();
        return hashCode;
    }
#else
    public override int GetHashCode() => HashCode.Combine(Name, Value, IsLiteral);
#endif

    public static bool operator ==(StaticWebAssetSegmentPart left, StaticWebAssetSegmentPart right) => EqualityComparer<StaticWebAssetSegmentPart>.Default.Equals(left, right);
    public static bool operator !=(StaticWebAssetSegmentPart left, StaticWebAssetSegmentPart right) => !(left == right);
}
