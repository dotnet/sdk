// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class GlobNode
{
    public string Match { get; set; }

    public Dictionary<string, GlobNode> Literals { get; set; }

    public Dictionary<string, GlobNode> Extensions { get; set; }

    public List<ComplexGlobSegment> ComplexGlobSegments { get; set; }

    public GlobNode WildCard { get; set; }

    public GlobNode RecursiveWildCard { get; set; }

    internal bool HasChildren()
    {
        return Literals?.Count > 0 || Extensions?.Count > 0 || ComplexGlobSegments?.Count > 0 || WildCard != null || RecursiveWildCard != null;
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    public override string ToString()
    {
        var literals = $$"""{{{string.Join(", ", Literals?.Keys ?? Enumerable.Empty<string>())}}}""";
        var extensions = $$"""{{{string.Join(", ", Extensions?.Keys ?? Enumerable.Empty<string>())}}}""";
        var wildCard = WildCard != null ? "*" : string.Empty;
        var recursiveWildCard = RecursiveWildCard != null ? "**" : string.Empty;
        return $"{literals}|{extensions}|{wildCard}|{recursiveWildCard}";
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class ComplexGlobSegment
{
    public GlobNode Node { get; set; }
    public List<GlobSegmentPart> Parts { get; set; }

    private string GetDebuggerDisplay() => ToString();

    public override string ToString() => string.Join("", Parts.Select(p => p.ToString()));
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class GlobSegmentPart
{
    public GlobSegmentPartKind Kind { get; set; }
    public ReadOnlyMemory<char> Value { get; set; }

    private string GetDebuggerDisplay() => ToString();

    public override string ToString() => Kind switch
    {
        GlobSegmentPartKind.Literal => Value.ToString(),
        GlobSegmentPartKind.WildCard => "*",
        GlobSegmentPartKind.QuestionMark => "?",
        _ => throw new InvalidOperationException(),
    };
}

public enum GlobSegmentPartKind
{
    Literal,
    WildCard,
    QuestionMark,
}
