// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Analyzer.Utilities.Extensions;

internal static class SpanExtensions
{
    public static SpanSplitEnumerator Split(this ReadOnlySpan<char> source, char separator, StringSplitOptions options = StringSplitOptions.None)
    {
        return new SpanSplitEnumerator(source, separator, options);
    }

    public static SpanSplitEnumerator Split(this ReadOnlySpan<char> source, ReadOnlySpan<char> separator, StringSplitOptions options = StringSplitOptions.None)
    {
        return new SpanSplitEnumerator(source, separator, options);
    }

    public static int Split(this ReadOnlySpan<char> source, Span<Range> destination, char separator, StringSplitOptions options = StringSplitOptions.None)
    {
        ReadOnlySpan<char> separatorSpan = stackalloc[] { separator };
        return Split(source, destination, separatorSpan, options);
    }

    public static int Split(this ReadOnlySpan<char> source, Span<Range> destination, ReadOnlySpan<char> separator, StringSplitOptions options = StringSplitOptions.None)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }
        else if (separator.IsEmpty)
        {
            destination[0] = 0..(source.Length - 1);
            return 1;
        }

        var i = 0;
        foreach (Range range in source.Split(separator, options))
        {
            if (range.Start.Value == range.End.Value && options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
            {
                continue;
            }

            destination[i++] = range;

            if (i == destination.Length)
            {
                destination[i - 1] = range.Start..source.Length;
                break;
            }
        }

        return i;
    }

    public ref struct SpanSplitEnumerator
    {
        private readonly ReadOnlySpan<char> _source;
        private readonly ReadOnlySpan<char> _separatorSpan;
        private readonly char _separator;
        private readonly int _separatorLength;
        private readonly StringSplitOptions _options;

        private int _currentIndex;

        internal SpanSplitEnumerator(ReadOnlySpan<char> source, char separator, StringSplitOptions options)
        {
            _source = source;
            _separator = separator;
            _options = options;
            _separatorLength = 1;
        }

        internal SpanSplitEnumerator(ReadOnlySpan<char> source, ReadOnlySpan<char> separator, StringSplitOptions options)
        {
            _source = source;
            _separatorSpan = separator;
            _options = options;
            _separatorLength = separator.Length;
        }

        public Range Current { get; private set; }

        public SpanSplitEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            while (MoveNextPart())
            {
                if (this.Current.Start.Value == this.Current.End.Value && _options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool MoveNextPart()
        {
            if (_currentIndex > _source.Length || _source.Length == 0)
            {
                return false;
            }

            if (_currentIndex == _source.Length)
            {
                this.Current = new Range(_currentIndex, _currentIndex++);
                return true;
            }

            var slice = _source[_currentIndex..];
            var index = _separatorSpan.IsEmpty ? slice.IndexOf(_separator) : slice.IndexOf(_separatorSpan);

            var partLength = index >= 0 ? index : _source.Length - _currentIndex;

            this.Current = new Range(_currentIndex, _currentIndex + partLength);
            _currentIndex += partLength + _separatorLength;

            return true;
        }
    }
}
