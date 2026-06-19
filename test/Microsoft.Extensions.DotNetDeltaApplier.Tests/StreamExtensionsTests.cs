// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class StreamExtensionsTests
{
    private static readonly bool[] s_bools = [true, false];

    // Expands a set of values into the cartesian product with useBinaryWriter/useBinaryReader,
    // replacing the xUnit.Combinatorial [CombinatorialData]/[CombinatorialValues] behavior.
    private static IEnumerable<object[]> Expand<T>(IEnumerable<T> values)
        => from value in values
           from useBinaryWriter in s_bools
           from useBinaryReader in s_bools
           select new object[] { value!, useBinaryWriter, useBinaryReader };

    public static IEnumerable<object[]> StringData
        => Expand(new[] { "", "\u1234", "hello" });

    public static IEnumerable<object[]> SevenBitEncodedIntData
        => Expand(new[] { -1, -127, -128, -255, -256, int.MinValue, 0, 1, 10, 127, 128, 255, 256, int.MaxValue });

    public static IEnumerable<object[]> ByteData
        => Expand(new byte[] { 0, 255 });

    public static IEnumerable<object[]> Int32Data
        => Expand(new[] { int.MinValue, 0, int.MaxValue });

    public static IEnumerable<object[]> BoolData
        => Expand(new[] { true, false });

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

        Assert.AreEqual(expected, actual);
        Assert.AreEqual(bytesWritten, stream.Position);
    }

    [TestMethod]
    [DynamicData(nameof(StringData))]
    public async Task ReadWrite_String(
        string expected,
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

    [TestMethod]
    [DynamicData(nameof(SevenBitEncodedIntData))]
    public async Task ReadWrite_7BitEncodedInt(
        int expected,
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

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public async Task ReadWrite_Byte(
        byte expected,
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

    [TestMethod]
    [DynamicData(nameof(Int32Data))]
    public async Task ReadWrite_Int32(
        int expected,
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

    [TestMethod]
    [DynamicData(nameof(BoolData))]
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

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(1234)]
    public async Task ReadWrite_Int32Array(int length)
    {
        var expected = Enumerable.Range(0, length).ToArray();

        var stream = new MemoryStream();

        await stream.WriteAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadIntArrayAsync(CancellationToken.None);
        Assert.AreSequenceEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(1234)]
    public async Task ReadWrite_ByteArray(int length)
    {
        var expected = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        var stream = new MemoryStream();

        await stream.WriteByteArrayAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadByteArrayAsync(CancellationToken.None);
        Assert.AreSequenceEqual(expected, actual);
    }

    [TestMethod]
    public async Task ReadWrite_Guid()
    {
        var expected = Guid.NewGuid();

        var stream = new MemoryStream();

        await stream.WriteAsync(expected, CancellationToken.None);
        stream.Position = 0;

        var actual = await stream.ReadGuidAsync(CancellationToken.None);
        Assert.AreEqual(expected, actual);
    }
}
