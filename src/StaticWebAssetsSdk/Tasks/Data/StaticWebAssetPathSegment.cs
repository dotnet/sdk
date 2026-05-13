// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetPathSegment : IEquatable<StaticWebAssetPathSegment>
{
    public IList<StaticWebAssetSegmentPart> Parts { get; set; } = [];

    public bool IsOptional { get; set; }
    public bool IsPreferred { get; set; }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetPathSegment);

    public bool Equals(StaticWebAssetPathSegment other) => other is not null && Parts.SequenceEqual(other.Parts);

#if NET47_OR_GREATER
    public override int GetHashCode()
    {
        var hashCode = -1187269697;
        for (var i = 0; i < Parts.Count; i++)
        {
            hashCode = (hashCode * -1521134295) + EqualityComparer<StaticWebAssetSegmentPart>.Default.GetHashCode(Parts[i]);
        }

        return hashCode;
    }
#else
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        for (var i = 0; i < Parts.Count; i++)
        {
            hashCode.Add(Parts[i]);
        }
        return hashCode.ToHashCode();
    }
#endif

    public static bool operator ==(StaticWebAssetPathSegment left, StaticWebAssetPathSegment right) => EqualityComparer<StaticWebAssetPathSegment>.Default.Equals(left, right);
    public static bool operator !=(StaticWebAssetPathSegment left, StaticWebAssetPathSegment right) => !(left == right);

    internal string GetDebuggerDisplay()
    {
        return Parts != null && Parts.Count == 1 && Parts[0].IsLiteral ? Parts[0].Name : ComputeParameterExpression();

        string ComputeParameterExpression() =>
                string.Concat(Parts.Select(p => p.IsLiteral ? p.Name : $"{{{p.Name}}}").Prepend("#[").Append($"]{(IsOptional ? (IsPreferred ? "!" : "?") : "")}"));
    }

    internal ICollection<string> GetTokenNames()
    {
        var result = new HashSet<string>();
        foreach (var part in Parts)
        {
            if (!part.IsLiteral && string.IsNullOrEmpty(part.Value))
            {
                result.Add(part.Name);
            }
        }

        return result;
    }
}
