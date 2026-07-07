// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetGlobMatcherBuilder
{
    private readonly List<string> _includePatterns = [];
    private readonly List<string> _excludePatterns = [];

#if NET9_0_OR_GREATER
    public StaticWebAssetGlobMatcherBuilder AddIncludePatterns(params Span<string> patterns)
#else
    public StaticWebAssetGlobMatcherBuilder AddIncludePatterns(params string[] patterns)
#endif
    {
        _includePatterns.AddRange(patterns);
        return this;
    }

    public StaticWebAssetGlobMatcherBuilder AddIncludePatternsList(ICollection<string> patterns)
    {
        _includePatterns.AddRange(patterns);
        return this;
    }

#if NET9_0_OR_GREATER
    public StaticWebAssetGlobMatcherBuilder AddExcludePatterns(params Span<string> patterns)
#else
    public StaticWebAssetGlobMatcherBuilder AddExcludePatterns(params string[] patterns)
#endif
    {
        _excludePatterns.AddRange(patterns);
        return this;
    }

    public StaticWebAssetGlobMatcherBuilder AddExcludePatternsList(ICollection<string> patterns)
    {
        _excludePatterns.AddRange(patterns);
        return this;
    }

    public StaticWebAssetGlobMatcher Build()
    {
        var includeRoot = new GlobNode();
        GlobNode excludeRoot = null;
        var segments = new List<ReadOnlyMemory<char>>();
        BuildTree(includeRoot, _includePatterns, segments);
        if (_excludePatterns.Count > 0)
        {
            excludeRoot = new GlobNode();
            BuildTree(excludeRoot, _excludePatterns, segments);
        }

        return new StaticWebAssetGlobMatcher(includeRoot, excludeRoot);
    }

    private static void BuildTree(GlobNode root, List<string> patterns, List<ReadOnlyMemory<char>> segments)
    {
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var patternMemory = pattern.AsMemory();
            var tokenizer = new PathTokenizer(patternMemory.Span);
            segments.Clear();
            var tokenRanges = new List<PathTokenizer.Segment>();
            var collection = tokenizer.Fill(tokenRanges);
            for (var j = 0; j < collection.Count; j++)
            {
                var segment = collection[patternMemory, j];
                segments.Add(segment);
            }
            if (patternMemory.Span.EndsWith("/".AsSpan()) || patternMemory.Span.EndsWith("\\".AsSpan()))
            {
                segments.Add("**".AsMemory());
            }
            var current = root;
            for (var j = 0; j < segments.Count; j++)
            {
                var segment = segments[j];
                if (segment.Length == 0)
                {
                    continue;
                }

                var segmentSpan = segment.Span;
                if (TryAddRecursiveWildCard(segmentSpan, ref current) ||
                    TryAddWildcard(segmentSpan, ref current) ||
                    TryAddExtension(segment, segmentSpan, ref current) ||
                    TryAddComplexSegment(segment, segmentSpan, ref current) ||
                    TryAddLiteral(segment, ref current))
                {
                    continue;
                }
            }

            current.Match = pattern;
        }
    }
    private static bool TryAddLiteral(ReadOnlyMemory<char> segment, ref GlobNode current)
    {
#if NET9_0_OR_GREATER
        current.LiteralsDictionary ??= new(StringComparer.OrdinalIgnoreCase);
        current.Literals = current.Literals.Dictionary != null ? current.Literals : current.LiteralsDictionary.GetAlternateLookup<ReadOnlySpan<char>>();
#else
        current.Literals ??= new Dictionary<string, GlobNode>(StringComparer.OrdinalIgnoreCase);
#endif
        var literal = segment.ToString();
        if (!current.Literals.TryGetValue(literal, out var literalNode))
        {
            literalNode = new GlobNode();
#if NET9_0_OR_GREATER
            current.LiteralsDictionary[literal] = literalNode;
#else
            current.Literals[literal] = literalNode;
#endif
        }

        current = literalNode;
        return true;
    }

    private static bool TryAddComplexSegment(ReadOnlyMemory<char> segment, ReadOnlySpan<char> segmentSpan, ref GlobNode current)
    {
        var searchValues = "*?".AsSpan();
        var variableIndex = segmentSpan.IndexOfAny(searchValues);
        if (variableIndex != -1)
        {
            var lastSegmentIndex = -1;
            var complexSegment = new ComplexGlobSegment()
            {
                Node = new GlobNode(),
                Parts = []
            };
            current.ComplexGlobSegments ??= [complexSegment];
            var parts = complexSegment.Parts;
            while (variableIndex != -1)
            {
                if (variableIndex > lastSegmentIndex + 1)
                {
                    parts.Add(new GlobSegmentPart
                    {
                        Kind = GlobSegmentPartKind.Literal,
                        Value = segment.Slice(lastSegmentIndex + 1, variableIndex - lastSegmentIndex - 1)
                    });
                }

                parts.Add(new GlobSegmentPart
                {
                    Kind = segmentSpan[variableIndex] == '*' ? GlobSegmentPartKind.WildCard : GlobSegmentPartKind.QuestionMark,
                    Value = "*".AsMemory()
                });

                lastSegmentIndex = variableIndex;
                var nextVariableIndex = segmentSpan.Slice(variableIndex + 1).IndexOfAny(searchValues);
                variableIndex = nextVariableIndex == -1 ? -1 : variableIndex + 1 + nextVariableIndex;
            }

            if (lastSegmentIndex + 1 < segmentSpan.Length)
            {
                parts.Add(new GlobSegmentPart
                {
                    Kind = GlobSegmentPartKind.Literal,
                    Value = segment.Slice(lastSegmentIndex + 1)
                });
            }

            current = complexSegment.Node;
            return true;
        }

        return false;
    }

    private static bool TryAddExtension(ReadOnlyMemory<char> segment, ReadOnlySpan<char> segmentSpan, ref GlobNode current)
    {
        if (segmentSpan.StartsWith("*.".AsSpan(), StringComparison.Ordinal) && segmentSpan.LastIndexOf('*') == 0)
        {
#if NET9_0_OR_GREATER
            current.ExtensionsDictionary ??= new(StringComparer.OrdinalIgnoreCase);
            current.Extensions = current.Extensions.Dictionary != null ? current.Extensions : current.ExtensionsDictionary.GetAlternateLookup<ReadOnlySpan<char>>();
#else
            current.Extensions ??= new Dictionary<string, GlobNode>(StringComparer.OrdinalIgnoreCase);
#endif

            var extension = segment.Slice(1).ToString();
            if (!current.Extensions.TryGetValue(extension, out var extensionNode))
            {
                extensionNode = new GlobNode();
#if NET9_0_OR_GREATER
                current.ExtensionsDictionary[extension] = extensionNode;
#else
                current.Extensions[extension] = extensionNode;
#endif
            }
            current = extensionNode;
            return true;
        }

        return false;
    }

    private static bool TryAddRecursiveWildCard(ReadOnlySpan<char> segmentSpan, ref GlobNode current)
    {
        if (segmentSpan.Equals("**".AsMemory().Span, StringComparison.Ordinal))
        {
            current.RecursiveWildCard ??= new GlobNode();
            current = current.RecursiveWildCard;
            return true;
        }

        return false;
    }

    private static bool TryAddWildcard(ReadOnlySpan<char> segmentSpan, ref GlobNode current)
    {
        if (segmentSpan.Equals("*".AsMemory().Span, StringComparison.Ordinal))
        {
            current.WildCard ??= new GlobNode();

            current = current.WildCard;
            return true;
        }

        return false;
    }
}
