// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// An <see cref="ITestOutputHelper"/> wrapper that forwards only a bounded amount of output to an
    /// inner helper. The first <c>maxHeadCharacters</c> characters are forwarded as they are written,
    /// the most recent <c>maxTailCharacters</c> characters are buffered and emitted by
    /// <see cref="WriteBufferedTail"/>, and everything in between is dropped (replaced with a short note
    /// describing how much was omitted). A character budget is used rather than a line count because
    /// individual lines can be arbitrarily long.
    /// </summary>
    /// <remarks>
    /// This is useful for commands that intentionally produce an enormous amount of output (for example
    /// <c>dotnet test -v diag</c>). Echoing every line to xUnit's output buffer can flush hundreds of
    /// megabytes over the test host's single IPC channel when the test completes, which is enough to
    /// starve the blame hang-dump collector's inactivity timer and trigger a spurious timeout.
    /// </remarks>
    public sealed class TruncatingTestOutputHelper : ITestOutputHelper
    {
        private const int DefaultHeadCharacters = 50_000;
        private const int DefaultTailCharacters = 50_000;

        private readonly ITestOutputHelper _inner;
        private readonly int _maxHeadCharacters;
        private readonly int _maxTailCharacters;
        private readonly object _lock = new();
        private readonly Queue<string> _tail = new();

        private int _headCharactersWritten;
        private bool _headFull;
        private int _tailCharacters;
        private long _omittedCharacters;
        private int _omittedLines;
        private bool _flushed;

        public TruncatingTestOutputHelper(ITestOutputHelper inner, int maxHeadCharacters = DefaultHeadCharacters, int maxTailCharacters = DefaultTailCharacters)
        {
            _inner = inner;
            _maxHeadCharacters = maxHeadCharacters;
            _maxTailCharacters = maxTailCharacters;
        }

        public void WriteLine(string message)
        {
            message ??= string.Empty;

            lock (_lock)
            {
                if (!_headFull)
                {
                    // Still within the head budget: forward immediately so the start of the output is
                    // preserved even if the test later throws before the tail is flushed.
                    _inner.WriteLine(message);
                    _headCharactersWritten += message.Length;
                    if (_headCharactersWritten >= _maxHeadCharacters)
                    {
                        _headFull = true;
                    }

                    return;
                }

                // Past the head budget: keep only the most recent lines within the tail budget.
                _tail.Enqueue(message);
                _tailCharacters += message.Length;

                while (_tailCharacters > _maxTailCharacters && _tail.Count > 1)
                {
                    string dropped = _tail.Dequeue();
                    _tailCharacters -= dropped.Length;
                    _omittedCharacters += dropped.Length;
                    _omittedLines++;
                }
            }
        }

        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

        /// <summary>
        /// Emits the buffered tail (preceded by a note describing any omitted output) to the inner
        /// helper. Call this once the command being logged has finished. Safe to call more than once.
        /// </summary>
        public void WriteBufferedTail()
        {
            lock (_lock)
            {
                if (_flushed)
                {
                    return;
                }

                _flushed = true;

                if (_omittedLines > 0)
                {
                    _inner.WriteLine($"... [{_omittedLines} lines / {_omittedCharacters} characters of output omitted by {nameof(TruncatingTestOutputHelper)}] ...");
                }

                foreach (string line in _tail)
                {
                    _inner.WriteLine(line);
                }

                _tail.Clear();
                _tailCharacters = 0;
            }
        }
    }
}
