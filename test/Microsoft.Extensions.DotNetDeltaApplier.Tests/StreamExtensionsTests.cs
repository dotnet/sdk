// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class StreamExtensionsTests
{
    private static async Task TestAsync<T>(
        T expected,
        Action<BinaryWriter, T> syncWrite,
        Func<BinaryReader, T> syncRead,
        Func<Stream, T, CancellationToken, ValueTask> asyncWrite,
        Func<Stream, CancellationToken, ValueTask<T>> asyncRead,
        bool useBinaryReader,
        bool useBinaryWriter)
    {
        var stream = new MemoryStream();

        if (useBinaryWriter)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            syncWrite(writer, expected);
        }
        else
        {
            await asyncWrite(stream, expected, CancellationToken.None);
        }

        var bytesWritten = stream.Position;
        stream.Position = 0;

        T actual;
        if (useBinaryReader)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            actual = syncRead(reader);
        }
        else
        {
            actual = await asyncRead(stream, CancellationToken.None);
        }

        Assert.Equal(expected, actual);
        Assert.Equal(bytesWritten, stream.Position);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_String(
        [CombinatorialValues("", "\u1234", "hello")] string expected,
        bool useBinaryWriter,
        bool useBinaryReader)
    {
        await TestAsync(
            expected,
            (w, v) => w.Write(v),
            r => r.ReadString(),
            StreamExtesions.WriteAsync,
            StreamExtesions.ReadStringAsync,
            useBinaryWriter,
            useBinaryReader);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_7BitEncodedInt(
        [CombinatorialValues(-1, -127, -128, -255, -256, int.MinValue, 0, 1, 10, 127, 128, 255, 256, int.MaxValue)] int expected,
        bool useBinaryWriter,
        bool useBinaryReader)
    {
        await TestAsync(
            expected,
            (w, v) => w.Write7BitEncodedInt(v),
            r => r.Read7BitEncodedInt(),
            StreamExtesions.Write7BitEncodedIntAsync,
            StreamExtesions.Read7BitEncodedIntAsync,
            useBinaryWriter,
            useBinaryReader);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_Byte(
        [CombinatorialValues(0, 255)] byte expected,
        bool useBinaryWriter,
        bool useBinaryReader)
    {
        await TestAsync(
            expected,
            (w, v) => w.Write(v),
            r => r.ReadByte(),
            StreamExtesions.WriteAsync,
            StreamExtesions.ReadByteAsync,
            useBinaryWriter,
            useBinaryReader);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_Int32(
        [CombinatorialValues(int.MinValue, 0, int.MaxValue)] int expected,
        bool useBinaryWriter,
        bool useBinaryReader)
    {
        await TestAsync(
            expected,
            (w, v) => w.Write(v),
            r => r.ReadInt32(),
            StreamExtesions.WriteAsync,
            StreamExtesions.ReadInt32Async,
            useBinaryWriter,
            useBinaryReader);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_Bool(
        bool expected,
        bool useBinaryWriter,
        bool useBinaryReader)
    {
        await TestAsync(
            expected,
            (w, v) => w.Write(v),
            r => r.ReadBoolean(),
            StreamExtesions.WriteAsync,
            StreamExtesions.ReadBooleanAsync,
            useBinaryWriter,
            useBinaryReader);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_Int32Array(
        [CombinatorialValues(0, 1, 1234)] int length)
    {
        var expected = Enumerable.Range(0, length).ToArray();

        var stream = new MemoryStream();

        await stream.WriteAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadIntArrayAsync(CancellationToken.None);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [CombinatorialData]
    public async Task ReadWrite_ByteArray(
        [CombinatorialValues(0, 1, 1234)] int length)
    {
        var expected = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        var stream = new MemoryStream();

        await stream.WriteByteArrayAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadByteArrayAsync(CancellationToken.None);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadWrite_Guid()
    {
        var expected = Guid.NewGuid();

        var stream = new MemoryStream();

        await stream.WriteAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadGuidAsync(CancellationToken.None);
        Assert.Equal(expected, actual);
    }
}
