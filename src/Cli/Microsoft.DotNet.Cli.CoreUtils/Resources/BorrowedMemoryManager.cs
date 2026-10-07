// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Exposes caller-owned unmanaged memory as <see cref="Memory{T}"/> without taking ownership.
/// </summary>
internal sealed unsafe class BorrowedMemoryManager : MemoryManager<byte>
{
    private readonly byte* _pointer;
    private readonly int _length;

    /// <summary>
    ///  Initializes a manager over caller-owned unmanaged memory.
    /// </summary>
    /// <param name="pointer">A pointer to the first byte.</param>
    /// <param name="length">The number of bytes available from <paramref name="pointer"/>.</param>
    internal BorrowedMemoryManager(byte* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan() => new(_pointer, _length);

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)elementIndex, (uint)_length);
        return new(_pointer + elementIndex);
    }

    /// <inheritdoc/>
    public override void Unpin()
    {
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}