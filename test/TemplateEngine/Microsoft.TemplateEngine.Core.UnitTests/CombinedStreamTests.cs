// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class CombinedStreamTests
    {
        [Theory]
        [InlineData(2 * 1024 * 1024)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10000)]
        [InlineData(1024 * 1024 + 10000)]
        public void CanReadStream(int requestedCount)
        {
            Random rnd = new Random();
            byte[] valueBytes1 = new byte[1024 * 1024];
            byte[] valueBytes2 = new byte[1024 * 1024];
            rnd.NextBytes(valueBytes1);
            rnd.NextBytes(valueBytes2);
            Stream stream1 = new ChunkMemoryStream(valueBytes1, 1024);
            Stream stream2 = new ChunkMemoryStream(valueBytes2, 1024);

            CombinedStream stream = new CombinedStream(stream1, stream2, inner => stream2 = inner);

            byte[] read = new byte[2 * 1024 * 1024];
            int nRead = stream.Read(read, 0, requestedCount);

            Assert.Equal(requestedCount, nRead);

            var upperBound = requestedCount > 1024 * 1024 ? 1024 * 1024 : requestedCount;
            for (int i = 0; i < upperBound; i++)
            {
                Assert.Equal(valueBytes1[i], read[i]);
            }
            if (requestedCount > 1024 * 1024)
            {
                for (int i = 0; i < requestedCount - 1024 * 1024; i++)
                {
                    Assert.Equal(valueBytes2[i], read[i + 1024 * 1024]);
                }
            }
            for (int i = requestedCount; i < 2 * 1024 * 1024; i++)
            {
                Assert.Equal(0, read[i]);
            }
        }

        private class ChunkMemoryStream : MemoryStream
        {
            private readonly int _chunkSize;

            internal ChunkMemoryStream(int chunkSize) : base()
            {
                _chunkSize = chunkSize;
            }

            internal ChunkMemoryStream(byte[] buffer, int chunkSize) : base(buffer)
            {
                _chunkSize = chunkSize;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count > _chunkSize)
                {
                    count = _chunkSize;
                }
                return base.Read(buffer, offset, count);
            }
        }
    }
}
