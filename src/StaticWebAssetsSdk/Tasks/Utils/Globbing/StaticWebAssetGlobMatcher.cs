// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetGlobMatcher(GlobNode includes, GlobNode excludes)
{
    // For testing only
    internal GlobMatch Match(string path)
    {
        var context = CreateMatchContext();
        context.SetPathAndReinitialize(path);
        return Match(context);
    }

    public GlobMatch Match(MatchContext context)
    {
        var stateStack = context.MatchStates;
        var tokenizer = new PathTokenizer(context.Path);
        var segments = tokenizer.Fill(context.Segments);
        if (segments.Count == 0)
        {
            return new(false, string.Empty);
        }

        if (excludes != null)
        {
            var excluded = MatchCore(excludes, segments, stateStack);
            if (excluded.IsMatch)
            {
                return new(false, null);
            }
        }

        return MatchCore(includes, segments, stateStack);
    }

    private static GlobMatch MatchCore(GlobNode includes, PathTokenizer.SegmentCollection segments, Stack<MatchState> stateStack)
    {
        stateStack.Push(new(includes));
        while (stateStack.Count > 0)
        {
            var state = stateStack.Pop();
            var stage = state.Stage;
            var currentIndex = state.SegmentIndex;
            var node = state.Node;

            switch (stage)
            {
                case MatchStage.Done:
                    if (currentIndex == segments.Count)
                    {
                        if (node.Match != null)
                        {
                            var stem = ComputeStem(segments, state.StemStartIndex);
                            return new(true, node.Match, stem);
                        }

                        // We got to the end with no matches, pop the next element on the stack.
                        continue;
                    }
                    break;
                case MatchStage.Literal:
                    if (currentIndex == segments.Count)
                    {
                        // We ran out of segments to match
                        continue;
                    }
                    PushNextStageIfAvailable(stateStack, state);
                    MatchLiteral(segments, stateStack, state);
                    break;
                case MatchStage.Extension:
                    if (currentIndex == segments.Count)
                    {
                        // We ran out of segments to match
                        continue;
                    }
                    PushNextStageIfAvailable(stateStack, state);
                    MatchExtension(segments, stateStack, state);
                    break;
                case MatchStage.Complex:
                    if (currentIndex == segments.Count)
                    {
                        // We ran out of segments to match
                        continue;
                    }
                    PushNextStageIfAvailable(stateStack, state);
                    MatchComplex(segments, stateStack, state);
                    break;
                case MatchStage.WildCard:
                    if (currentIndex == segments.Count)
                    {
                        // We ran out of segments to match
                        continue;
                    }
                    PushNextStageIfAvailable(stateStack, state);
                    MatchWildCard(stateStack, state);
                    break;
                case MatchStage.RecursiveWildCard:
                    MatchRecursiveWildCard(segments, stateStack, state);
                    break;
            }
        }

        return new(false, null);
    }

    private static string ComputeStem(PathTokenizer.SegmentCollection segments, int stemStartIndex)
    {
        if (stemStartIndex == -1)
        {
            return segments[segments.Count - 1].ToString();
        }
#if NET9_0_OR_GREATER
        var stemLength = 0;
        for (var i = stemStartIndex; i < segments.Count; i++)
        {
            stemLength += segments[i].Length;
        }
        // Separators
        stemLength += segments.Count - stemStartIndex - 1;

        return string.Create(stemLength, segments.Slice(stemStartIndex), (span, segments) =>
        {
            var index = 0;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                segment.CopyTo(span.Slice(index));
                index += segment.Length;
                if (i < segments.Count - 1)
                {
                    span[index++] = '/';
                }
            }
        });
#else
        var stem = new StringBuilder();
        for (var i = stemStartIndex; i < segments.Count; i++)
        {
            stem.Append(segments[i].ToString());
            if (i < segments.Count - 1)
            {
                stem.Append('/');
            }
        }
        return stem.ToString();
#endif
    }

    private static void MatchComplex(PathTokenizer.SegmentCollection segments, Stack<MatchState> stateStack, MatchState state)
    {
        // We need to try all the complex segments until we find one that matches or we run out of segments to try.
        // If we find a match for the current segment, we need to make sure that the rest of the segments match the remainder of the pattern.
        // For that reason, if we find a match, we need to push a state that will try the next complex segment in the list (if any) and one
        // state that will try the next segment in the current match, so that if for some reason the rest of the pattern doesn't match, we can
        // continue trying the rest of the complex segments.
        var complexSegmentIndex = state.ComplexSegmentIndex;
        var currentIndex = state.SegmentIndex;
        var node = state.Node;
        var segment = segments[currentIndex];
        var complexSegment = node.ComplexGlobSegments[complexSegmentIndex];
        var parts = complexSegment.Parts;

        if (TryMatchParts(segment, parts))
        {
            // We have a match for the current segment
            if (complexSegmentIndex + 1 < node.ComplexGlobSegments.Count)
            {
                // Push a state that will try the next complex segment
                stateStack.Push(state.NextComplex());
            }

            // Push a state to try the remainder of the segments
            stateStack.Push(state.NextSegment(complexSegment.Node));
        }
    }

    private static bool TryMatchParts(ReadOnlySpan<char> span, List<GlobSegmentPart> parts, int index = 0, int partIndex = 0)
    {
        for (var i = partIndex; i < parts.Count; i++)
        {
            if (index > span.Length)
            {
                // No more characters to consume but we still have parts to process
                return false;
            }

            var part = parts[i];
            switch (part.Kind)
            {
                case GlobSegmentPartKind.Literal:
                    if (!span.Slice(index).StartsWith(part.Value.Span, StringComparison.OrdinalIgnoreCase))
                    {
                        // Literal didn't match
                        return false;
                    }
                    index += part.Value.Length;
                    break;
                case GlobSegmentPartKind.QuestionMark:
                    index++;
                    break;
                case GlobSegmentPartKind.WildCard:
                    // Wildcards require trying to match 0 or more characters, so we need to try matching the rest of the parts after
                    // having consumed 0, 1, 2, ... characters and so on.
                    // Instead of jumping 0, 1, 2, etc, we are going to calculate the next step by finding the next literal on the list.
                    // If we find another * we can discard the current one.
                    // If we find one or moe '?' we can require that at least as many characters as '?' are consumed.
                    // When we find a literal, we can try to find the index of the literal in the remaining string, and if we find it, we can
                    // try to match the rest of the parts, jumping ahead after the literal.
                    // If we happen to not find a literal, we have a match (trailing *) or at most we can require that there are N characters
                    // left in the string, where N is the number of '?' in the remaining parts.
                    var minimumCharactersToConsume = 0;
                    for (var j = i + 1; j < parts.Count; j++)
                    {
                        var nextPart = parts[j];
                        switch (nextPart.Kind)
                        {
                            case GlobSegmentPartKind.Literal:
                                // Start searching after the current index + the minimum characters to consume
                                var remainingSpan = span.Slice(index + minimumCharactersToConsume);
                                var nextLiteralIndex = remainingSpan.IndexOf(nextPart.Value.Span, StringComparison.OrdinalIgnoreCase);
                                while (nextLiteralIndex != -1)
                                {
                                    // Consume the characters before the literal and the literal itself before we try
                                    // to match the rest of the parts.
                                    remainingSpan = remainingSpan.Slice(nextLiteralIndex + nextPart.Value.Length);

                                    if (remainingSpan.Length == 0 && j == parts.Count - 1)
                                    {
                                        // We were looking at the last literal, so we have a match
                                        return true;
                                    }

                                    if (!TryMatchParts(remainingSpan, parts, 0, j + 1))
                                    {
                                        // If we couldn't match the rest of the parts, try the next literal
                                        nextLiteralIndex = remainingSpan.IndexOf(nextPart.Value.Span, StringComparison.OrdinalIgnoreCase);
                                    }
                                    else
                                    {
                                        return true;
                                    }
                                }
                                // At this point we couldn't match the next literal, in the list, so this pattern is not a match
                                return false;
                            case GlobSegmentPartKind.QuestionMark:
                                minimumCharactersToConsume++;
                                break;
                            case GlobSegmentPartKind.WildCard:
                                // Ignore any wildcard that comes right after the original one
                                break;
                        }
                    }

                    // There were no trailing literals, so we have a match if there are at least as many characters as '?' in the remaining parts
                    return index + minimumCharactersToConsume <= span.Length;
            }
        }

        return index == span.Length;
    }

    private static void MatchRecursiveWildCard(PathTokenizer.SegmentCollection segments, Stack<MatchState> stateStack, MatchState state)
    {
        var node = state.Node;
        for (var i = segments.Count - state.SegmentIndex; i >= 0; i--)
        {
            var nextSegment = state.NextSegment(node.RecursiveWildCard, i);
            // The stem is calculated as the first time the /**/ pattern is matched til the remainder of the path, otherwise, the stem is
            // the file name.
            if (nextSegment.StemStartIndex == -1)
            {
                nextSegment.StemStartIndex = state.SegmentIndex;
            }

            stateStack.Push(nextSegment);
        }
    }

    private static void MatchWildCard(Stack<MatchState> stateStack, MatchState state)
    {
        // A wildcard matches any segment, so we can continue with the next
        stateStack.Push(state.NextSegment(state.Node.WildCard));
    }

    private static void MatchExtension(PathTokenizer.SegmentCollection segments, Stack<MatchState> stateStack, MatchState state)
    {
        var node = state.Node;
        var currentIndex = state.SegmentIndex;
        var extensionIndex = state.ExtensionSegmentIndex;
        var segment = segments[currentIndex];
        if (extensionIndex >= segment.Length)
        {
            // We couldn't find any path that matched the extensions we have
            return;
        }

        // We start from something.else.txt matching.else.txt and then .txt
        var remaining = segment.Slice(extensionIndex);
        var indexOfDot = remaining.IndexOf('.');
        if (indexOfDot != -1)
        {
            if (TryMatchExtension(node, remaining.Slice(indexOfDot), out var extensionCandidate))
            {
                stateStack.Push(state.NextSegment(extensionCandidate));
            }
            else
            {
                // If we fail to match, try and match the next extension.
                stateStack.Push(state.NextExtension(extensionIndex + indexOfDot + 1));
            }
        }
    }

    private static void MatchLiteral(PathTokenizer.SegmentCollection segments, Stack<MatchState> stateStack, MatchState state)
    {
        var currentIndex = state.SegmentIndex;
        var node = state.Node;
        // Push the next stage to the stack so we can continue searching in case we don't match the entire path
        PushNextStageIfAvailable(stateStack, state);
        if (TryMatchLiteral(node, segments[currentIndex], out var literalCandidate))
        {
            // Push the found node to the stack to match the remaining path segments
            stateStack.Push(state.NextSegment(literalCandidate));
        }
    }

    private static void PushNextStageIfAvailable(Stack<MatchState> stateStack, MatchState state)
    {
        if (state.ExtensionSegmentIndex == 0 && state.ComplexSegmentIndex == 0)
        {
            var nextStage = state.NextStage();
            if (nextStage.HasValue)
            {
                stateStack.Push(nextStage);
            }
        }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal struct MatchState(GlobNode node, MatchStage stage, int segmentIndex, int extensionSegmentIndex, int complexSegmentIndex)
    {
        public MatchState(GlobNode node) : this(node, GetInitialStage(node), 0, 0, 0) { }

        public GlobNode Node { get; set; } = node;

        public MatchStage Stage { get; set; } = stage;

        // Index on the list of segments for the path
        public int SegmentIndex { get; set; } = segmentIndex;

        public int ExtensionSegmentIndex { get; set; } = extensionSegmentIndex;

        public int ComplexSegmentIndex { get; set; } = complexSegmentIndex;

        public int StemStartIndex { get; set; } = -1;

        internal readonly bool HasValue => Node != null;

        public readonly void Deconstruct(out GlobNode node, out MatchStage stage, out int segmentIndex, out int extensionIndex, out int complexIndex)
        {
            node = Node;
            stage = Stage;
            segmentIndex = SegmentIndex;
            extensionIndex = ExtensionSegmentIndex;
            complexIndex = ComplexSegmentIndex;
        }

        internal MatchState NextSegment(GlobNode candidate, int elements = 1, int complexIndex = 0) =>
            new(candidate, GetInitialStage(candidate), SegmentIndex + elements, 0, complexIndex) { StemStartIndex = StemStartIndex };

        internal MatchState NextStage()
        {
            switch (Stage)
            {
                case MatchStage.Literal:
                    if (Node.HasExtensions())
                    {
                        return new(Node, MatchStage.Extension, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }

                    if (Node.ComplexGlobSegments != null && Node.ComplexGlobSegments.Count > 0)
                    {
                        return new(Node, MatchStage.Complex, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }

                    if (Node.WildCard != null)
                    {
                        return new(Node, MatchStage.WildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }

                    if (Node.RecursiveWildCard != null)
                    {
                        return new(Node, MatchStage.RecursiveWildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }
                    break;
                case MatchStage.Extension:
                    if (Node.ComplexGlobSegments != null && Node.ComplexGlobSegments.Count > 0)
                    {
                        return new(Node, MatchStage.Complex, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }

                    if (Node.WildCard != null)
                    {
                        return new(Node, MatchStage.WildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }

                    if (Node.RecursiveWildCard != null)
                    {
                        return new(Node, MatchStage.RecursiveWildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }
                    break;
                case MatchStage.Complex:
                    if (Node.WildCard != null)
                    {
                        return new(Node, MatchStage.WildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }
                    if (Node.RecursiveWildCard != null)
                    {
                        return new(Node, MatchStage.RecursiveWildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }
                    break;
                case MatchStage.WildCard:
                    if (Node.RecursiveWildCard != null)
                    {
                        return new(Node, MatchStage.RecursiveWildCard, SegmentIndex, 0, 0)
                        { StemStartIndex = StemStartIndex };
                    }
                    break;
                case MatchStage.RecursiveWildCard:
                    return new(Node, MatchStage.Done, SegmentIndex, 0, 0)
                    { StemStartIndex = StemStartIndex };
            }

            return default;
        }

        private static MatchStage GetInitialStage(GlobNode node)
        {
            if (node.HasLiterals())
            {
                return MatchStage.Literal;
            }

            if (node.HasExtensions())
            {
                return MatchStage.Extension;
            }

            if (node.ComplexGlobSegments != null && node.ComplexGlobSegments.Count > 0)
            {
                return MatchStage.Complex;
            }

            if (node.WildCard != null)
            {
                return MatchStage.WildCard;
            }

            if (node.RecursiveWildCard != null)
            {
                return MatchStage.RecursiveWildCard;
            }

            return MatchStage.Done;
        }

        internal readonly MatchState NextExtension(int extensionIndex) => new(Node, MatchStage.Extension, SegmentIndex, extensionIndex, ComplexSegmentIndex);

        internal readonly MatchState NextComplex() => new(Node, MatchStage.Complex, SegmentIndex, ExtensionSegmentIndex, ComplexSegmentIndex + 1);

        private readonly string GetDebuggerDisplay()
        {
            return $"Node: {Node}, Stage: {Stage}, SegmentIndex: {SegmentIndex}, ExtensionIndex: {ExtensionSegmentIndex}, ComplexSegmentIndex: {ComplexSegmentIndex}";
        }

    }

    internal enum MatchStage
    {
        Done,
        Literal,
        Extension,
        Complex,
        WildCard,
        RecursiveWildCard
    }

    private static bool TryMatchExtension(GlobNode node, ReadOnlySpan<char> extension, out GlobNode extensionCandidate) =>
#if NET9_0_OR_GREATER
        node.Extensions.TryGetValue(extension, out extensionCandidate);
#else
        node.Extensions.TryGetValue(extension.ToString(), out extensionCandidate);
#endif

    private static bool TryMatchLiteral(GlobNode node, ReadOnlySpan<char> current, out GlobNode nextNode) =>
#if NET9_0_OR_GREATER
        node.Literals.TryGetValue(current, out nextNode);
#else
        node.Literals.TryGetValue(current.ToString(), out nextNode);
#endif

    // The matchContext holds all the state for the underlying matching algorithm.
    // It is reused so that we avoid allocating memory for each match.
    // It is not thread-safe and should not be shared across threads.
    public static MatchContext CreateMatchContext() => new();

    public ref struct MatchContext()
    {
        public ReadOnlySpan<char> Path;
        public string PathString;

        internal List<PathTokenizer.Segment> Segments { get; set; } = [];
        internal Stack<MatchState> MatchStates { get; set; } = [];

        public void SetPathAndReinitialize(string path)
        {
            PathString = path;
            Path = path.AsSpan();
            Segments.Clear();
            MatchStates.Clear();
        }

        public void SetPathAndReinitialize(ReadOnlySpan<char> path)
        {
            Path = path;
            Segments.Clear();
            MatchStates.Clear();
        }

        public void SetPathAndReinitialize(ReadOnlyMemory<char> path)
        {
            Path = path.Span;
            Segments.Clear();
            MatchStates.Clear();
        }

    }
}
