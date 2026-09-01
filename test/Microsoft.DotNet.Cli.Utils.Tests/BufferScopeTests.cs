// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Ported from dotnet/msbuild (src/Framework.UnitTests/BufferScope_Tests.cs) to MSTest + FluentAssertions.

using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils;

[TestClass]
public class BufferScopeTests
{
    [TestMethod]
    public void MinimumLengthConstructor_RentsAtLeastRequestedSize()
    {
        using BufferScope<char> buffer = new(16);
        buffer.Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [TestMethod]
    public void InitialBufferConstructor_UsesProvidedSpan()
    {
        Span<char> initial = stackalloc char[8];
        using BufferScope<char> buffer = new(initial);
        buffer.Length.Should().Be(8);
    }

    [TestMethod]
    public void InitialBufferWithMinimum_UsesInitialWhenLargeEnough()
    {
        Span<byte> initial = stackalloc byte[32];
        using BufferScope<byte> buffer = new(initial, 16);
        buffer.Length.Should().Be(32);
    }

    [TestMethod]
    public void InitialBufferWithMinimum_RentsWhenInitialTooSmall()
    {
        Span<byte> initial = stackalloc byte[4];
        using BufferScope<byte> buffer = new(initial, 128);
        buffer.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [TestMethod]
    public void Indexer_GetsAndSetsValues()
    {
        using BufferScope<int> buffer = new(4);
        buffer[0] = 10;
        buffer[1] = 20;
        buffer[2] = 30;
        buffer[0].Should().Be(10);
        buffer[1].Should().Be(20);
        buffer[2].Should().Be(30);
    }

    [TestMethod]
    public void Slice_ReturnsRequestedRange()
    {
        using BufferScope<char> buffer = new(10);
        buffer[0] = 'a';
        buffer[1] = 'b';
        buffer[2] = 'c';
        buffer[3] = 'd';

        Span<char> slice = buffer.Slice(1, 2);
        slice.Length.Should().Be(2);
        slice[0].Should().Be('b');
        slice[1].Should().Be('c');
    }

    [TestMethod]
    public void ToString_ReturnsSpanContents()
    {
        Span<char> initial = stackalloc char[5];
        using BufferScope<char> buffer = new(initial);
        buffer[0] = 'h';
        buffer[1] = 'e';
        buffer[2] = 'l';
        buffer[3] = 'l';
        buffer[4] = 'o';

        buffer.ToString().Should().Be("hello");
    }

    [TestMethod]
    public void EnsureCapacity_NoOpWhenAlreadyLargeEnough()
    {
        using BufferScope<int> buffer = new(64);
        int originalLength = buffer.Length;
        buffer.EnsureCapacity(32);
        buffer.Length.Should().Be(originalLength);
    }

    [TestMethod]
    public void EnsureCapacity_GrowsWhenNeeded()
    {
        Span<byte> initial = stackalloc byte[4];
        using BufferScope<byte> buffer = new(initial);
        buffer.Length.Should().Be(4);

        buffer.EnsureCapacity(128);
        buffer.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [TestMethod]
    public void EnsureCapacity_WithCopy_PreservesExistingContents()
    {
        Span<int> initial = stackalloc int[4];
        using BufferScope<int> buffer = new(initial);
        buffer[0] = 1;
        buffer[1] = 2;
        buffer[2] = 3;
        buffer[3] = 4;

        buffer.EnsureCapacity(64, copy: true);

        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);
        buffer[2].Should().Be(3);
        buffer[3].Should().Be(4);
    }

    [TestMethod]
    public void AsSpan_ReturnsUnderlyingSpan()
    {
        using BufferScope<int> buffer = new(8);
        Span<int> span = buffer.AsSpan();
        span.Length.Should().Be(buffer.Length);
    }

    [TestMethod]
    public void ImplicitSpanConversion_Works()
    {
        using BufferScope<int> buffer = new(8);
        buffer[0] = 42;
        Span<int> span = buffer;
        span[0].Should().Be(42);
    }

    [TestMethod]
    public void ImplicitReadOnlySpanConversion_Works()
    {
        using BufferScope<int> buffer = new(8);
        buffer[0] = 42;
        ReadOnlySpan<int> span = buffer;
        span[0].Should().Be(42);
    }

    [TestMethod]
    public void GetEnumerator_IteratesOverElements()
    {
        using BufferScope<int> buffer = new(stackalloc int[3]);
        buffer[0] = 1;
        buffer[1] = 2;
        buffer[2] = 3;

        int sum = 0;
        foreach (int value in buffer)
        {
            sum += value;
        }
        sum.Should().Be(6);
    }

    [TestMethod]
    public void MinimumLengthConstructor_HandlesZeroLength()
    {
        using BufferScope<int> buffer = new(0);
        buffer.Length.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void InitialBufferConstructor_HandlesEmptySpan()
    {
        using BufferScope<int> buffer = new([]);
        buffer.Length.Should().Be(0);
    }

    [TestMethod]
    public void InitialBufferWithMinimum_UsesInitialWhenEqualToMinimum()
    {
        using BufferScope<char> buffer = new(stackalloc char[10], 10);
        buffer.Length.Should().Be(10);
    }

    [TestMethod]
    public void EnsureCapacity_GrowWithoutCopy_ExpandsBuffer()
    {
        using BufferScope<int> buffer = new(10);
        buffer[0] = 42;
        buffer[5] = 100;

        buffer.EnsureCapacity(50, copy: false);

        buffer.Length.Should().BeGreaterThanOrEqualTo(50);
    }

    [TestMethod]
    public void EnsureCapacity_MultipleGrows_PreservesCopiedData()
    {
        using BufferScope<int> buffer = new(5);
        buffer[0] = 1;
        buffer[1] = 2;

        buffer.EnsureCapacity(10, copy: true);
        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);

        // Write to the top of the grown buffer so the second grow's copy is verified at
        // both the lower and upper bounds of the used range, not just the first elements.
        buffer[9] = 10;

        buffer.EnsureCapacity(50, copy: true);
        buffer.Length.Should().BeGreaterThanOrEqualTo(50);
        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);
        buffer[9].Should().Be(10);
    }

    [TestMethod]
    public void RangeSlicing_FullRange_ReturnsAllElements()
    {
        using BufferScope<char> buffer = new(stackalloc char[5]);
        buffer[0] = 'A';
        buffer[1] = 'B';
        buffer[2] = 'C';
        buffer[3] = 'D';
        buffer[4] = 'E';

        Span<char> slice = buffer[..];
        slice.Length.Should().Be(5);
        slice[0].Should().Be('A');
        slice[4].Should().Be('E');
    }

    [TestMethod]
    public void RangeSlicing_PartialRange_ReturnsExpectedElements()
    {
        using BufferScope<int> buffer = new(stackalloc int[10]);
        for (int i = 0; i < 10; i++)
        {
            buffer[i] = i;
        }

        Span<int> slice = buffer[2..8];
        slice.Length.Should().Be(6);
        slice[0].Should().Be(2);
        slice[5].Should().Be(7);
    }

    [TestMethod]
    public void RangeSlicing_EmptyRange_ReturnsEmptySpan()
    {
        using BufferScope<byte> buffer = new(10);
        Span<byte> slice = buffer[5..5];
        slice.Length.Should().Be(0);
    }

    [TestMethod]
    public void Slice_CanReturnZeroLengthSpan()
    {
        using BufferScope<int> buffer = new(10);
        Span<int> slice = buffer.Slice(5, 0);
        slice.Length.Should().Be(0);
    }

    [TestMethod]
    public void GetEnumerator_EmptyBuffer_YieldsNoElements()
    {
        using BufferScope<string> buffer = new([]);

        int count = 0;
        foreach (string value in buffer)
        {
            _ = value;
            count++;
        }

        count.Should().Be(0);
    }

    [TestMethod]
    public void ToString_EmptyBuffer_ReturnsEmptyString()
    {
        using BufferScope<char> buffer = new([]);
        buffer.ToString().Should().Be(string.Empty);
    }

    [TestMethod]
    public void GetPinnableReference_CanModifyUnderlyingMemory()
    {
        using BufferScope<byte> buffer = new(stackalloc byte[10]);
        buffer[0] = 255;
        buffer[9] = 128;

        ref byte reference = ref buffer.GetPinnableReference();
        reference.Should().Be((byte)255);
        reference = 100;

        buffer[0].Should().Be((byte)100);
    }

    [TestMethod]
    public void GetPinnableReference_EmptyBuffer_DoesNotThrow()
    {
        using BufferScope<int> buffer = new([]);
        buffer.GetPinnableReference();
        buffer.Length.Should().Be(0);
    }

    [TestMethod]
    public void Fixed_PinsPooledBuffer()
    {
        using BufferScope<char> buffer = new(64);
        buffer[0] = 'Y';

        unsafe
        {
            fixed (char* p = buffer)
            {
                (*p).Should().Be('Y');
                *p = 'Z';
            }
        }

        buffer[0].Should().Be('Z');
    }

    [TestMethod]
    public void WorksWithReferenceTypes()
    {
        using BufferScope<string> buffer = new(5);
        buffer[0] = "Hello";
        buffer[1] = "World";

        buffer[0].Should().Be("Hello");
        buffer[1].Should().Be("World");
    }

    [TestMethod]
    public void WorksWithValueTypeStructs()
    {
        using BufferScope<DateTime> buffer = new(3);
        DateTime date1 = new(2025, 1, 1);
        DateTime date2 = new(2025, 12, 31);

        buffer[0] = date1;
        buffer[1] = date2;

        buffer[0].Should().Be(date1);
        buffer[1].Should().Be(date2);
    }

    [TestMethod]
    public void CombinedOperations_GrowSliceAndEnumerate()
    {
        using BufferScope<int> buffer = new(stackalloc int[5], 10);

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = i + 1;
        }

        buffer.EnsureCapacity(20, copy: true);

        for (int i = 0; i < 5; i++)
        {
            buffer[i].Should().Be(i + 1);
        }

        Span<int> slice = buffer[1..4];
        slice.Length.Should().Be(3);
        slice[0].Should().Be(2);
        slice[2].Should().Be(4);

        int sum = 0;
        foreach (int value in buffer.AsSpan()[..5])
        {
            sum += value;
        }

        sum.Should().Be(15);
    }

    [TestMethod]
    public void Dispose_ClearsSpan()
    {
        BufferScope<byte> buffer = new(16);
        buffer.Length.Should().BeGreaterThan(0);
        buffer.Dispose();
        buffer.Length.Should().Be(0);
    }

    [TestMethod]
    public void Dispose_SafeToCallMultipleTimes()
    {
        BufferScope<int> buffer = new(8);
        buffer.Dispose();
        // Calling Dispose a second time must not throw. ref structs cannot be
        // captured by a lambda, so invoke directly.
        buffer.Dispose();
    }

    [TestMethod]
    public void Fixed_PinsUnderlyingMemory()
    {
        using BufferScope<char> buffer = new(stackalloc char[8]);
        buffer[0] = 'x';
        unsafe
        {
            fixed (char* p = buffer)
            {
                (*p).Should().Be('x');
            }
        }
    }
}
