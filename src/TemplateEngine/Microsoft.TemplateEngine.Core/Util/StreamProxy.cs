// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.TemplateEngine.Core.Util
{
    /// <summary>
    /// Simple implementation of stream proxy to be used in case where destination stream is being re-read and adjusted in place multiple times.
    /// Direct I/O usage is unnecessarily costly in those situations (even with BCL buffered streams).
    /// This implementation flips to destination stream as soon as the cumulative size of the stream exceeds fixed threshold.
    /// </summary>
    internal class StreamProxy : Stream
    {
        /// <summary>
        /// Upper limit of a  sane size of a source file of good factored new codebase.
        /// </summary>
        public const int MaxRecommendedBufferedFileSize = 100 * 1024;

        private readonly Stream _targetStream;
        private readonly Stream? _memoryStream;
        private readonly int _maximumMemoryWindowSize;
        private Stream _currentTargetStream;

        public StreamProxy(Stream underlyingStream, int initialSize)
        : this(underlyingStream, initialSize, MaxRecommendedBufferedFileSize)
        { }

        public StreamProxy(Stream underlyingStream, int initialSize, int maximumMemoryWindowSize)
        {
            this._maximumMemoryWindowSize = maximumMemoryWindowSize;
            this._targetStream = underlyingStream;

            if (initialSize > maximumMemoryWindowSize)
            {
                _currentTargetStream = _targetStream;
            }
            else
            {
                _memoryStream = new MemoryStream(initialSize);
                _currentTargetStream = _memoryStream;
            }
        }

        public override bool CanRead => _currentTargetStream.CanRead;

        public override bool CanSeek => _currentTargetStream.CanSeek;

        public override bool CanWrite => _currentTargetStream.CanWrite;

        public override long Length => _currentTargetStream.Length;

        public override long Position
        {
            get => _currentTargetStream.Position;

            set => _currentTargetStream.Position = value;
        }

        public override void Flush() => _currentTargetStream.Flush();

        public override long Seek(long offset, SeekOrigin origin) => _currentTargetStream.Seek(offset, origin);

        public override void SetLength(long value)
        {
            CheckStreamExpectedSize(value);

            _currentTargetStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count) => _currentTargetStream.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckStreamExpectedSize(this.Position + count);

            _currentTargetStream.Write(buffer, offset, count);
        }

        public void FlushToTarget()
        {
            CopyMemoryToTargetStream();

            _currentTargetStream.Flush();
        }

        private void CheckStreamExpectedSize(long newExpectedSize)
        {
            if (newExpectedSize > _maximumMemoryWindowSize && _currentTargetStream == _memoryStream)
            {
                CopyMemoryToTargetStream();
            }
        }

        private void CopyMemoryToTargetStream()
        {
            if (_currentTargetStream != _memoryStream)
            {
                return;
            }

            long tempPosition = _memoryStream.Position;
            _memoryStream.Flush();
            _memoryStream.Position = 0;
            _targetStream.Position = 0;
            _memoryStream.CopyTo(_targetStream);
            _targetStream.Position = tempPosition;
            _memoryStream.Dispose();

            _currentTargetStream = _targetStream;
        }
    }
}
