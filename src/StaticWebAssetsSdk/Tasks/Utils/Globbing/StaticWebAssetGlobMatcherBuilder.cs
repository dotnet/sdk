// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetGlobMatcherBuilder
{
    private readonly List<string> _includePatterns = new();
    private readonly List<string> _excludePatterns = new();

    public StaticWebAssetGlobMatcherBuilder AddIncludePatterns(params string[] patterns)
    {
        _includePatterns.AddRange(patterns);
        return this;
    }

    public StaticWebAssetGlobMatcherBuilder AddExcludePatterns(params string[] patterns)
    {
        _excludePatterns.AddRange(patterns);
        return this;
    }

    public StaticWebAssetGlobMatcher Build()
    {
        var includeRoot = new GlobNode();
        var excludeRoot = new GlobNode();
        var segments = new List<ReadOnlyMemory<char>>();
        BuildTree(includeRoot, _includePatterns, segments);
        if (_excludePatterns.Count > 0)
        {
            BuildTree(excludeRoot, _excludePatterns, segments);
        }

        return new StaticWebAssetGlobMatcher(includeRoot, _excludePatterns.Count > 0 ? excludeRoot : null);
    }

    private void BuildTree(GlobNode includes, List<string> includePatterns, List<ReadOnlyMemory<char>> segments)
    {
        for (var i = 0; i < includePatterns.Count; i++)
        {
            var pattern = includePatterns[i];
            var patternMemory = pattern.AsMemory();
            var tokenizer = new PathTokenizer(patternMemory);
            segments.Clear();
            tokenizer.Fill(segments);
            if (patternMemory.Span.EndsWith("/".AsSpan()) || patternMemory.Span.EndsWith("\\".AsSpan()))
            {
                segments.Add("**".AsMemory());
            }
            var current = includes;
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
                    TryAddLiteral(segment, segmentSpan, pattern, ref current))
                {
                    continue;
                }
            }

            current.Match = pattern;
        }
    }
    private bool TryAddLiteral(ReadOnlyMemory<char> segment, ReadOnlySpan<char> segmentSpan, string pattern, ref GlobNode current)
    {
        current.Literals ??= new Dictionary<string, GlobNode>(StringComparer.OrdinalIgnoreCase);
        var literal = segment.ToString();
        if (!current.Literals.TryGetValue(literal, out var literalNode))
        {
            literalNode = new GlobNode();
            current.Literals[literal] = literalNode;
        }

        current = literalNode;
        return true;
    }

    private bool TryAddComplexSegment(ReadOnlyMemory<char> segment, ReadOnlySpan<char> segmentSpan, ref GlobNode current)
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
            current.Extensions ??= new Dictionary<string, GlobNode>(StringComparer.OrdinalIgnoreCase);

            var extension = segment.Slice(1).ToString();
            if (!current.Extensions.TryGetValue(extension, out var extensionNode))
            {
                extensionNode = new GlobNode();
                current.Extensions[extension] = extensionNode;
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
