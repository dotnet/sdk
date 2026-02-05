// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

#if !NET9_0_OR_GREATER
public ref struct PathTokenizer(ReadOnlySpan<char> path)
{
    private readonly ReadOnlySpan<char> _path = path;
    int _index = -1;
    int _nextSeparatorIndex = -1;

    public readonly Segment Current => 
        new (_index, (_nextSeparatorIndex == -1 ? _path.Length : _nextSeparatorIndex) - _index);

    public bool MoveNext()
    {
        if (_index != -1 && _nextSeparatorIndex == -1)
        {
            return false;
        }

        _index = _nextSeparatorIndex + 1;
        _nextSeparatorIndex = GetSeparator();
        return true;
    }

    internal SegmentCollection Fill(List<Segment> segments)
    {
        while (MoveNext())
        {
            if (Current.Length > 0 &&
                !_path.Slice(Current.Start, Current.Length).Equals(".".AsSpan(), StringComparison.Ordinal) &&
                !_path.Slice(Current.Start, Current.Length).Equals("..".AsSpan(), StringComparison.Ordinal))
            {
                segments.Add(Current);
            }
        }

        return new SegmentCollection(_path, segments);
    }

    private readonly int GetSeparator() => _path.Slice(_index).IndexOfAny(OSPath.DirectoryPathSeparators.Span) switch
    {
        -1 => -1,
        var index => index + _index
    };

    public struct Segment(int start, int length)
    {
        public int Start { get; set; } = start;
        public int Length { get; set; } = length;
    }

    public readonly ref struct SegmentCollection(ReadOnlySpan<char> path, List<Segment> segments)
    {
        private readonly ReadOnlySpan<char> _path = path;
        private readonly int _index = 0;

        private SegmentCollection(ReadOnlySpan<char> path, List<Segment> segments, int index) : this(path, segments) =>
            _index = index;

        public int Count => segments.Count - _index;

        public ReadOnlySpan<char> this[int index] => _path.Slice(segments[index + _index].Start, segments[index + _index].Length);

        public ReadOnlyMemory<char> this[ReadOnlyMemory<char> path, int index] => path.Slice(segments[index + _index].Start, segments[index + _index].Length);

        internal SegmentCollection Slice(int segmentIndex) => new (_path, segments, segmentIndex);
    }
}
#else
public ref struct PathTokenizer(ReadOnlySpan<char> path)
{
    private readonly ReadOnlySpan<char> _path = path;

    public struct Segment(int start, int length)
    {
        public int Start { get; set; } = start;
        public int Length { get; set; } = length;
    }

    internal SegmentCollection Fill(List<Segment> segments)
    {
        foreach (var range in MemoryExtensions.SplitAny(_path, OSPath.DirectoryPathSeparators.Span))
        {
            var length = range.End.Value - range.Start.Value;
            if (length > 0 &&
                !_path.Slice(range.Start.Value, length).Equals(".".AsSpan(), StringComparison.Ordinal) &&
                !_path.Slice(range.Start.Value, length).Equals("..".AsSpan(), StringComparison.Ordinal))
            {
                segments.Add(new(range.Start.Value, length));
            }
        }

        return new SegmentCollection(_path, segments);
    }

    public readonly ref struct SegmentCollection(ReadOnlySpan<char> path, List<Segment> segments)
    {
        private readonly ReadOnlySpan<char> _path = path;
        private readonly int _index = 0;

        private SegmentCollection(ReadOnlySpan<char> path, List<Segment> segments, int index) : this(path, segments) =>
            _index = index;

        public int Count => segments.Count - _index;

        public ReadOnlySpan<char> this[int index] => _path.Slice(segments[index + _index].Start, segments[index + _index].Length);

        public ReadOnlyMemory<char> this[ReadOnlyMemory<char> path, int index] => path.Slice(segments[index + _index].Start, segments[index + _index].Length);

        internal SegmentCollection Slice(int segmentIndex) => new(_path, segments, segmentIndex);
    }
}
#endif
