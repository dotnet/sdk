// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Copied from
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Memory/tests/ArrayBufferWriter/ArrayBufferWriterTests.T.cs and
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Memory/tests/ArrayBufferWriter/ArrayBufferWriterTests.Byte.cs
// and added asserts for GetArraySegment.

namespace Microsoft.DotNet.HotReload.UnitTests;

internal static class TestExtensions
{
#if NET
    public static ArraySegment<T> GetArraySegment<T>(this ArrayBufferWriter<T> writer, int sizeHint = 0)
    {
        Assert.IsTrue(MemoryMarshal.TryGetArray(writer.GetMemory(sizeHint), out ArraySegment<T> segment));
        return segment;
    }
#endif

    public static T GetItem<T>(this ArraySegment<T> segment, int index)
        => segment.Array![segment.Offset + index];
}

[TestClass]
public abstract class ArrayBufferWriterTests<T> where T : IEquatable<T>
{
    [TestMethod]
    public void ArrayBufferWriter_Ctor()
    {
        {
            var output = new ArrayBufferWriter<T>();
            Assert.AreEqual(0, output.FreeCapacity);
            Assert.AreEqual(0, output.Capacity);
            Assert.AreEqual(0, output.WrittenCount);
            Assert.IsTrue(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
            Assert.IsTrue(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        }

        {
            var output = new ArrayBufferWriter<T>(200);
            Assert.IsTrue(output.FreeCapacity >= 200);
            Assert.IsTrue(output.Capacity >= 200);
            Assert.AreEqual(0, output.WrittenCount);
            Assert.IsTrue(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
            Assert.IsTrue(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        }

        {
            ArrayBufferWriter<T> output = null!;
            Assert.IsNull(output);
        }
    }

    [TestMethod]
    public void Invalid_Ctor()
    {
        if (Type.GetType("Mono.Runtime") is not null)
        {
            Assert.Inconclusive("Active issue: https://github.com/mono/mono/issues/15002");
        }

        Assert.ThrowsExactly<ArgumentException>(() => new ArrayBufferWriter<T>(0));
        Assert.ThrowsExactly<ArgumentException>(() => new ArrayBufferWriter<T>(-1));
        Assert.ThrowsExactly<OutOfMemoryException>(() => new ArrayBufferWriter<T>(int.MaxValue));
    }

    [TestMethod]
    public void Clear()
    {
        var output = new ArrayBufferWriter<T>(256);
        int previousAvailable = output.FreeCapacity;
        WriteData(output, 2);
        Assert.IsTrue(output.FreeCapacity < previousAvailable);
        Assert.IsTrue(output.WrittenCount > 0);
        Assert.IsFalse(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
        Assert.IsFalse(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));

        ReadOnlyMemory<T> transientMemory = output.WrittenMemory;
        ReadOnlySpan<T> transientSpan = output.WrittenSpan;
        T t0 = transientMemory.Span[0];
        T t1 = transientSpan[1];
        Assert.AreNotEqual(default, t0);
        Assert.AreNotEqual(default, t1);
        output.Clear();
        Assert.AreEqual(default, transientMemory.Span[0]);
        Assert.AreEqual(default, transientSpan[1]);

        Assert.AreEqual(0, output.WrittenCount);
        Assert.IsTrue(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
        Assert.IsTrue(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.AreEqual(previousAvailable, output.FreeCapacity);
    }

    [TestMethod]
    public void ResetWrittenCount()
    {
        var output = new ArrayBufferWriter<T>(256);
        int previousAvailable = output.FreeCapacity;
        WriteData(output, 2);
        Assert.IsTrue(output.FreeCapacity < previousAvailable);
        Assert.IsTrue(output.WrittenCount > 0);
        Assert.IsFalse(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
        Assert.IsFalse(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));

        ReadOnlyMemory<T> transientMemory = output.WrittenMemory;
        ReadOnlySpan<T> transientSpan = output.WrittenSpan;
        T t0 = transientMemory.Span[0];
        T t1 = transientSpan[1];
        Assert.AreNotEqual(default, t0);
        Assert.AreNotEqual(default, t1);
        output.ResetWrittenCount();
        Assert.AreEqual(t0, transientMemory.Span[0]);
        Assert.AreEqual(t1, transientSpan[1]);

        Assert.AreEqual(0, output.WrittenCount);
        Assert.IsTrue(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
        Assert.IsTrue(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.AreEqual(previousAvailable, output.FreeCapacity);
    }

    [TestMethod]
    public void Advance()
    {
        {
            var output = new ArrayBufferWriter<T>();
            int capacity = output.Capacity;
            Assert.AreEqual(capacity, output.FreeCapacity);
            output.Advance(output.FreeCapacity);
            Assert.AreEqual(capacity, output.WrittenCount);
            Assert.AreEqual(0, output.FreeCapacity);
        }

        {
            var output = new ArrayBufferWriter<T>();
            output.Advance(output.Capacity);
            Assert.AreEqual(output.Capacity, output.WrittenCount);
            Assert.AreEqual(0, output.FreeCapacity);
            int previousCapacity = output.Capacity;
            Span<T> _ = output.GetSpan();
            Assert.IsTrue(output.Capacity > previousCapacity);
        }

        {
            var output = new ArrayBufferWriter<T>(256);
            WriteData(output, 2);
            ReadOnlyMemory<T> previousMemory = output.WrittenMemory;
            ReadOnlySpan<T> previousSpan = output.WrittenSpan;
            Assert.IsTrue(previousSpan.SequenceEqual(previousMemory.Span));
            output.Advance(10);
            Assert.IsFalse(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
            Assert.IsFalse(previousSpan.SequenceEqual(output.WrittenSpan));
            Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
        }

        {
            var output = new ArrayBufferWriter<T>();
            _ = output.GetSpan(20);
            WriteData(output, 10);
            ReadOnlyMemory<T> previousMemory = output.WrittenMemory;
            ReadOnlySpan<T> previousSpan = output.WrittenSpan;
            Assert.IsTrue(previousSpan.SequenceEqual(previousMemory.Span));
            Assert.ThrowsExactly<InvalidOperationException>(() => output.Advance(247));
            output.Advance(10);
            Assert.IsFalse(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
            Assert.IsFalse(previousSpan.SequenceEqual(output.WrittenSpan));
            Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
        }
    }

    [TestMethod]
    public void AdvanceZero()
    {
        var output = new ArrayBufferWriter<T>();
        WriteData(output, 2);
        Assert.AreEqual(2, output.WrittenCount);
        ReadOnlyMemory<T> previousMemory = output.WrittenMemory;
        ReadOnlySpan<T> previousSpan = output.WrittenSpan;
        Assert.IsTrue(previousSpan.SequenceEqual(previousMemory.Span));
        output.Advance(0);
        Assert.AreEqual(2, output.WrittenCount);
        Assert.IsTrue(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(previousSpan.SequenceEqual(output.WrittenSpan));
        Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
    }

    [TestMethod]
    public void InvalidAdvance()
    {
        {
            var output = new ArrayBufferWriter<T>();
            Assert.ThrowsExactly<ArgumentException>(() => output.Advance(-1));
            Assert.ThrowsExactly<InvalidOperationException>(() => output.Advance(output.Capacity + 1));
        }

        {
            var output = new ArrayBufferWriter<T>();
            WriteData(output, 100);
            Assert.ThrowsExactly<InvalidOperationException>(() => output.Advance(output.FreeCapacity + 1));
        }
    }

    [TestMethod]
    public void GetSpan_DefaultCtor()
    {
        var output = new ArrayBufferWriter<T>();
        var span = output.GetSpan();
        Assert.AreEqual(256, span.Length);
    }

    [TestMethod]
    [DynamicData(nameof(SizeHints))]
    public void GetSpan_DefaultCtor_WithSizeHint(int sizeHint)
    {
        var output = new ArrayBufferWriter<T>();
        var span = output.GetSpan(sizeHint);
        Assert.AreEqual(sizeHint <= 256 ? 256 : sizeHint, span.Length);
    }

    [TestMethod]
    public void GetSpan_InitSizeCtor()
    {
        var output = new ArrayBufferWriter<T>(100);
        var span = output.GetSpan();
        Assert.AreEqual(100, span.Length);
    }

    [TestMethod]
    [DynamicData(nameof(SizeHints))]
    public void GetSpan_InitSizeCtor_WithSizeHint(int sizeHint)
    {
        {
            var output = new ArrayBufferWriter<T>(256);
            var span = output.GetSpan(sizeHint);
            Assert.AreEqual(sizeHint <= 256 ? 256 : sizeHint + 256, span.Length);
        }

        {
            var output = new ArrayBufferWriter<T>(1000);
            var span = output.GetSpan(sizeHint);
            Assert.AreEqual(sizeHint <= 1000 ? 1000 : sizeHint + 1000, span.Length);
        }
    }

    [TestMethod]
    public void GetMemory_DefaultCtor()
    {
        var output = new ArrayBufferWriter<T>();
        var memory = output.GetMemory();
        Assert.AreEqual(256, memory.Length);

        var segment = output.GetArraySegment();
        Assert.AreEqual(memory.Length, segment.Count);
    }

    [TestMethod]
    [DynamicData(nameof(SizeHints))]
    public void GetMemory_DefaultCtor_WithSizeHint(int sizeHint)
    {
        var output = new ArrayBufferWriter<T>();
        var memory = output.GetMemory(sizeHint);
        Assert.AreEqual(sizeHint <= 256 ? 256 : sizeHint, memory.Length);

        var segment = output.GetArraySegment(sizeHint);
        Assert.AreEqual(memory.Length, segment.Count);
    }

    [TestMethod]
    public void GetMemory_ExceedMaximumBufferSize_WithSmallStartingSize()
    {
        var output = new ArrayBufferWriter<T>(256);
        Assert.ThrowsExactly<OutOfMemoryException>(() => output.GetMemory(int.MaxValue));
        Assert.ThrowsExactly<OutOfMemoryException>(() => output.GetArraySegment(int.MaxValue));
    }

    [TestMethod]
    public void GetMemory_InitSizeCtor()
    {
        var output = new ArrayBufferWriter<T>(100);
        var memory = output.GetMemory();
        Assert.AreEqual(100, memory.Length);

        var segment = output.GetArraySegment();
        Assert.AreEqual(memory.Length, segment.Count);
    }

    [TestMethod]
    [DynamicData(nameof(SizeHints))]
    public void GetMemory_InitSizeCtor_WithSizeHint(int sizeHint)
    {
        {
            var output = new ArrayBufferWriter<T>(256);
            var memory = output.GetMemory(sizeHint);
            Assert.AreEqual(sizeHint <= 256 ? 256 : sizeHint + 256, memory.Length);

            var segment = output.GetArraySegment();
            Assert.AreEqual(memory.Length, segment.Count);
        }

        {
            var output = new ArrayBufferWriter<T>(1000);
            var memory = output.GetMemory(sizeHint);
            Assert.AreEqual(sizeHint <= 1000 ? 1000 : sizeHint + 1000, memory.Length);

            var segment = output.GetArraySegment();
            Assert.AreEqual(memory.Length, segment.Count);
        }
    }

    // NOTE: InvalidAdvance_Large test is constrained to run on Windows and MacOSX because it causes
    //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
    //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
    //       time the memory is accessed which triggers the full memory allocation.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX)]
    [Is64BitProcessCondition]
    [TestCategory("OuterLoop")]
    public void InvalidAdvance_Large()
    {
        try
        {
            {
                var output = new ArrayBufferWriter<T>(2_000_000_000);
                WriteData(output, 1_000);
                Assert.ThrowsExactly<InvalidOperationException>(() => output.Advance(int.MaxValue));
                Assert.ThrowsExactly<InvalidOperationException>(() => output.Advance(2_000_000_000 - 1_000 + 1));
            }
        }
        catch (OutOfMemoryException) { }
    }

    [TestMethod]
    public void GetMemoryAndSpan()
    {
        {
            var output = new ArrayBufferWriter<T>();
            WriteData(output, 2);
            var span = output.GetSpan();
            var memory = output.GetMemory();
            var segment = output.GetArraySegment();
            Span<T> memorySpan = memory.Span;
            Assert.IsTrue(span.Length > 0);
            Assert.AreEqual(span.Length, memorySpan.Length);
            Assert.AreEqual(span.Length, segment.Count);

            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(default, span[i]);
                Assert.AreEqual(default, memorySpan[i]);
                Assert.AreEqual(default, segment.GetItem(i));
            }
        }

        {
            var output = new ArrayBufferWriter<T>();
            WriteData(output, 2);
            ReadOnlyMemory<T> writtenSoFarMemory = output.WrittenMemory;
            ReadOnlySpan<T> writtenSoFar = output.WrittenSpan;
            Assert.IsTrue(writtenSoFarMemory.Span.SequenceEqual(writtenSoFar));
            int previousAvailable = output.FreeCapacity;
            var span = output.GetSpan(500);
            Assert.IsTrue(span.Length >= 500);
            Assert.IsTrue(output.FreeCapacity >= 500);
            Assert.IsTrue(output.FreeCapacity > previousAvailable);

            Assert.AreEqual(writtenSoFar.Length, output.WrittenCount);
            Assert.IsFalse(writtenSoFar.SequenceEqual(span.Slice(0, output.WrittenCount)));

            var memory = output.GetMemory();
            var segment = output.GetArraySegment();
            Span<T> memorySpan = memory.Span;
            Assert.IsTrue(span.Length >= 500);
            Assert.IsTrue(memorySpan.Length >= 500);
            Assert.AreEqual(span.Length, memorySpan.Length);
            Assert.AreEqual(span.Length, segment.Count);
            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(default, span[i]);
                Assert.AreEqual(default, memorySpan[i]);
                Assert.AreEqual(default, segment.GetItem(i));
            }

            memory = output.GetMemory(500);
            segment = output.GetArraySegment(500);
            memorySpan = memory.Span;
            Assert.IsTrue(memorySpan.Length >= 500);
            Assert.AreEqual(span.Length, memorySpan.Length);
            for (int i = 0; i < memorySpan.Length; i++)
            {
                Assert.AreEqual(default, memorySpan[i]);
                Assert.AreEqual(default, segment.GetItem(i));
            }
        }
    }

    [TestMethod]
    public void GetSpanShouldAtleastDoubleWhenGrowing()
    {
        var output = new ArrayBufferWriter<T>(256);
        WriteData(output, 100);
        int previousAvailable = output.FreeCapacity;

        _ = output.GetSpan(previousAvailable);
        Assert.AreEqual(previousAvailable, output.FreeCapacity);

        _ = output.GetSpan(previousAvailable + 1);
        Assert.IsTrue(output.FreeCapacity >= previousAvailable * 2);
    }

    [TestMethod]
    public void GetSpanOnlyGrowsAboveThreshold()
    {
        {
            var output = new ArrayBufferWriter<T>();
            _ = output.GetSpan();
            int previousAvailable = output.FreeCapacity;

            for (int i = 0; i < 10; i++)
            {
                _ = output.GetSpan();
                Assert.AreEqual(previousAvailable, output.FreeCapacity);
            }
        }

        {
            var output = new ArrayBufferWriter<T>();
            _ = output.GetSpan(10);
            int previousAvailable = output.FreeCapacity;

            for (int i = 0; i < 10; i++)
            {
                _ = output.GetSpan(previousAvailable);
                Assert.AreEqual(previousAvailable, output.FreeCapacity);
            }
        }
    }

    [TestMethod]
    public void InvalidGetMemoryAndSpan()
    {
        var output = new ArrayBufferWriter<T>();
        WriteData(output, 2);
        Assert.ThrowsExactly<ArgumentException>(() => output.GetSpan(-1));
        Assert.ThrowsExactly<ArgumentException>(() => output.GetMemory(-1));
        Assert.ThrowsExactly<ArgumentException>(() => output.GetArraySegment(-1));
    }

    protected abstract void WriteData(IBufferWriter<T> bufferWriter, int numBytes);

    public static IEnumerable<object[]> SizeHints
    {
        get
        {
            return new List<object[]>
            {
                new object[] { 0 },
                new object[] { 1 },
                new object[] { 2 },
                new object[] { 3 },
                new object[] { 99 },
                new object[] { 100 },
                new object[] { 101 },
                new object[] { 255 },
                new object[] { 256 },
                new object[] { 257 },
                new object[] { 1000 },
                new object[] { 2000 },
            };
        }
    }
}

[TestClass]
public class ArrayBufferWriterTests_Byte : ArrayBufferWriterTests<byte>
{
    protected override void WriteData(IBufferWriter<byte> bufferWriter, int numBytes)
    {
        Span<byte> outputSpan = bufferWriter.GetSpan(numBytes);
        Assert.IsTrue(outputSpan.Length >= numBytes);
        var random = new Random(42);

        var data = new byte[numBytes];
        random.NextBytes(data);
        data.CopyTo(outputSpan);

        bufferWriter.Advance(numBytes);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WriteAndCopyToStream(bool clearContent)
    {
        System.Buffers.ArrayBufferWriter<byte> output = new();
        WriteData(output, 100);

        using MemoryStream memStream = new(100);

        Assert.AreEqual(100, output.WrittenCount);

        ReadOnlySpan<byte> outputSpan = output.WrittenMemory.ToArray();

        ReadOnlyMemory<byte> transientMemory = output.WrittenMemory;
        ReadOnlySpan<byte> transientSpan = output.WrittenSpan;

        Assert.IsTrue(transientSpan.SequenceEqual(transientMemory.Span));

        Assert.IsTrue(transientSpan[0] != 0);
        byte expectedFirstByte = transientSpan[0];

        memStream.Write(transientSpan.ToArray(), 0, transientSpan.Length);

        if (clearContent)
        {
            expectedFirstByte = 0;
            output.Clear();
        }
        else
        {
            output.ResetWrittenCount();
        }

        Assert.AreEqual(expectedFirstByte, transientSpan[0]);
        Assert.AreEqual(expectedFirstByte, transientMemory.Span[0]);

        Assert.AreEqual(0, output.WrittenCount);
        byte[] streamOutput = memStream.ToArray();

        Assert.IsTrue(ReadOnlyMemory<byte>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(ReadOnlySpan<byte>.Empty.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));

        Assert.AreEqual(outputSpan.Length, streamOutput.Length);
        Assert.IsTrue(outputSpan.SequenceEqual(streamOutput));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WriteAndCopyToStreamAsync(bool clearContent)
    {
        System.Buffers.ArrayBufferWriter<byte> output = new();
        WriteData(output, 100);

        using MemoryStream memStream = new(100);

        Assert.AreEqual(100, output.WrittenCount);

        ReadOnlyMemory<byte> outputMemory = output.WrittenMemory.ToArray();

        ReadOnlyMemory<byte> transient = output.WrittenMemory;

        Assert.IsTrue(transient.Span[0] != 0);
        byte expectedFirstByte = transient.Span[0];

        await memStream.WriteAsync(transient.ToArray(), 0, transient.Length, CancellationToken.None);

        if (clearContent)
        {
            expectedFirstByte = 0;
            output.Clear();
        }
        else
        {
            output.ResetWrittenCount();
        }

        Assert.IsTrue(transient.Span[0] == expectedFirstByte);

        Assert.AreEqual(0, output.WrittenCount);
        byte[] streamOutput = memStream.ToArray();

        Assert.IsTrue(ReadOnlyMemory<byte>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
        Assert.IsTrue(ReadOnlySpan<byte>.Empty.SequenceEqual(output.WrittenMemory.Span));

        Assert.AreEqual(outputMemory.Length, streamOutput.Length);
        Assert.IsTrue(outputMemory.Span.SequenceEqual(streamOutput));
    }
}
