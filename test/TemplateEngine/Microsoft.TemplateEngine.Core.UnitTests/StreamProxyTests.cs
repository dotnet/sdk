// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class StreamProxyTests
    {
        [Fact]
        public void BytesBufferedIfSizeUnderThreshold()
        {
            byte[] bytes = new byte[256];
            MemoryStream ms = new MemoryStream(bytes);

            StreamProxy proxy = new StreamProxy(ms, 10, 100);

            int numBytesFirstBatch = 15;
            byte[] bytesToWrite = Enumerable.Range(0, numBytesFirstBatch).Select(i => (byte)i).ToArray();
            proxy.Write(bytesToWrite, 0, numBytesFirstBatch);

            ms.Position.Should().Be(0);
            bytes.Should().AllBeEquivalentTo(0);
            proxy.Position.Should().Be(numBytesFirstBatch);
            proxy.Position = 0;
            byte[] tmp = new byte[numBytesFirstBatch];
            proxy.Read(tmp, 0, numBytesFirstBatch).Should().Be(numBytesFirstBatch);
            tmp.Should().BeEquivalentTo(bytesToWrite, options => options.WithStrictOrdering());
            proxy.Position = numBytesFirstBatch;

            int numBytesSecondBatch = 30;
            byte[] bytesToWrite2 = Enumerable.Range(numBytesFirstBatch, numBytesSecondBatch).Select(i => (byte)i).ToArray();
            proxy.Write(bytesToWrite2, 0, numBytesSecondBatch);

            ms.Position.Should().Be(0);
            bytes.Should().AllBeEquivalentTo(0);
            proxy.Position.Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            proxy.Position = 0;
            byte[] tmp2 = new byte[numBytesFirstBatch + numBytesSecondBatch];
            proxy.Read(tmp2, 0, numBytesFirstBatch + numBytesSecondBatch).Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            tmp2.Should().BeEquivalentTo(bytesToWrite.Concat(bytesToWrite2), options => options.WithStrictOrdering());
            proxy.Position = numBytesFirstBatch + numBytesSecondBatch;

            proxy.Flush();
            ms.Position.Should().Be(0);
            bytes.Should().AllBeEquivalentTo(0);

            proxy.FlushToTarget();
            ms.Position.Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            bytes.AsSpan(0, numBytesFirstBatch + numBytesSecondBatch).ToArray()
                .Should().BeEquivalentTo(bytesToWrite.Concat(bytesToWrite2), options => options.WithStrictOrdering());
        }

        [Fact]
        public void BytesSendToTagetIfSizeOverThreshold()
        {
            byte[] bytes = new byte[256];
            MemoryStream ms = new MemoryStream(bytes);

            StreamProxy proxy = new StreamProxy(ms, 10, 16);

            int numBytesFirstBatch = 15;
            byte[] bytesToWrite = Enumerable.Range(0, numBytesFirstBatch).Select(i => (byte)i).ToArray();
            proxy.Write(bytesToWrite, 0, numBytesFirstBatch);

            ms.Position.Should().Be(0);
            bytes.Should().AllBeEquivalentTo(0);
            proxy.Position.Should().Be(numBytesFirstBatch);
            proxy.Position = 0;
            byte[] tmp = new byte[numBytesFirstBatch];
            proxy.Read(tmp, 0, numBytesFirstBatch).Should().Be(numBytesFirstBatch);
            tmp.Should().BeEquivalentTo(bytesToWrite, options => options.WithStrictOrdering());
            proxy.Position = numBytesFirstBatch;

            int numBytesSecondBatch = 30;
            byte[] bytesToWrite2 = Enumerable.Range(numBytesFirstBatch, numBytesSecondBatch).Select(i => (byte)i).ToArray();
            proxy.Write(bytesToWrite2, 0, numBytesSecondBatch);

            ms.Position.Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            bytes.AsSpan(0, numBytesFirstBatch + numBytesSecondBatch).ToArray()
                .Should().BeEquivalentTo(bytesToWrite.Concat(bytesToWrite2), options => options.WithStrictOrdering());

            proxy.Position.Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            proxy.Position = 0;
            byte[] tmp2 = new byte[numBytesFirstBatch + numBytesSecondBatch];
            proxy.Read(tmp2, 0, numBytesFirstBatch + numBytesSecondBatch).Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            tmp2.Should().BeEquivalentTo(bytesToWrite.Concat(bytesToWrite2), options => options.WithStrictOrdering());
            proxy.Position = numBytesFirstBatch + numBytesSecondBatch;

            proxy.FlushToTarget();
            ms.Position.Should().Be(numBytesFirstBatch + numBytesSecondBatch);
            bytes.AsSpan(0, numBytesFirstBatch + numBytesSecondBatch).ToArray()
                .Should().BeEquivalentTo(bytesToWrite.Concat(bytesToWrite2), options => options.WithStrictOrdering());
        }
    }
}
