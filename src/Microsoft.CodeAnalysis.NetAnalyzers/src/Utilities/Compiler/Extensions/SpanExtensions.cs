// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Analyzer.Utilities.Extensions;

internal static class SpanExtensions
{
    public static SpanSplitEnumerator Split(this ReadOnlySpan<char> source, char separator)
    {
        return new SpanSplitEnumerator(source, separator);
    }

    public ref struct SpanSplitEnumerator
    {
        private readonly ReadOnlySpan<char> _source;
        private readonly char _separator;

        private int _currentIndex;

        internal SpanSplitEnumerator(ReadOnlySpan<char> source, char separator)
        {
            _source = source;
            _separator = separator;
        }

        public Range Current { get; private set; }

        public SpanSplitEnumerator GetEnumerator() => this;

        public bool MoveNext()
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

            var index = _source[_currentIndex..].IndexOf(_separator);
            var length = index >= 0 ? index : _source.Length - _currentIndex;

            this.Current = new Range(_currentIndex, _currentIndex + length);
            _currentIndex += length + 1;

            return true;
        }
    }
}
