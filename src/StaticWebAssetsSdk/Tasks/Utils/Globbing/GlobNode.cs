// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class GlobNode
{
    public string Match { get; set; }

#if NET9_0_OR_GREATER
    public Dictionary<string, GlobNode> LiteralsDictionary { get; set; }
    public Dictionary<string, GlobNode>.AlternateLookup<ReadOnlySpan<char>> Literals { get; set; }
#else
    public Dictionary<string, GlobNode> Literals { get; set; }
#endif

#if NET9_0_OR_GREATER
    public Dictionary<string, GlobNode> ExtensionsDictionary { get; set; }
    public Dictionary<string, GlobNode>.AlternateLookup<ReadOnlySpan<char>> Extensions { get; set; }
#else
    public Dictionary<string, GlobNode> Extensions { get; set; }
#endif

    public List<ComplexGlobSegment> ComplexGlobSegments { get; set; }

    public GlobNode WildCard { get; set; }

    public GlobNode RecursiveWildCard { get; set; }

    internal bool HasChildren()
    {
#if NET9_0_OR_GREATER
        return LiteralsDictionary?.Count > 0 || ExtensionsDictionary?.Count > 0 || ComplexGlobSegments?.Count > 0 || WildCard != null || RecursiveWildCard != null;
#else
        return Literals?.Count > 0 || Extensions?.Count > 0 || ComplexGlobSegments?.Count > 0 || WildCard != null || RecursiveWildCard != null;
#endif
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    public override string ToString()
    {
#if NET9_0_OR_GREATER
        var literals = $$"""{{{string.Join(", ", LiteralsDictionary?.Keys ?? Enumerable.Empty<string>())}}}""";
        var extensions = $$"""{{{string.Join(", ", ExtensionsDictionary?.Keys ?? Enumerable.Empty<string>())}}}""";
#else
        var literals = $$"""{{{string.Join(", ", Literals?.Keys ?? Enumerable.Empty<string>())}}}""";
        var extensions = $$"""{{{string.Join(", ", Extensions?.Keys ?? Enumerable.Empty<string>())}}}""";
#endif
        var wildCard = WildCard != null ? "*" : string.Empty;
        var recursiveWildCard = RecursiveWildCard != null ? "**" : string.Empty;
        return $"{literals}|{extensions}|{wildCard}|{recursiveWildCard}";
    }

    internal bool HasLiterals()
    {
#if NET9_0_OR_GREATER
        return LiteralsDictionary?.Count > 0;
#else
        return Literals?.Count > 0;
#endif
    }

    internal bool HasExtensions()
    {
#if NET9_0_OR_GREATER
        return ExtensionsDictionary?.Count > 0;
#else
        return Extensions?.Count > 0;
#endif
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
