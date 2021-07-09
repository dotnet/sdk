// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.TemplateEngine.Core.Util
{
    internal class CombinedStream : Stream
    {
        private readonly Stream _stream1;
        private readonly Stream _stream2;
        private readonly Action<Stream> _reassign;
        private bool _isStream1Depleted;
        private bool _isReassigned;

        public CombinedStream(Stream stream1, Stream stream2, Action<Stream> reassign)
        {
            _stream1 = stream1;
            _stream2 = stream2;
            _reassign = reassign;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count + offset > buffer.Length)
            {
                //cannot read more than available buffer length
                count = buffer.Length - offset;
            }

            int totalBytesRead = 0;
            if (!_isStream1Depleted)
            {
                while (totalBytesRead < count)
                {
                    int bytesRead = _stream1.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    totalBytesRead += bytesRead;
                }
                if (totalBytesRead == count)
                {
                    return totalBytesRead;
                }

                _isStream1Depleted = true;
                _stream1.Dispose();
                _reassign(_stream2);
                _isReassigned = true;
            }

            while (totalBytesRead < count)
            {
                int bytesRead = _stream2.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!_isReassigned)
            {
                _stream1.Dispose();
                _stream2.Dispose();
            }
        }
    }
}
