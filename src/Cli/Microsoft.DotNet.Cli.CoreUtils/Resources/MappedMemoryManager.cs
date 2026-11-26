// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.MemoryMappedFiles;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  A <see cref="MemoryManager{T}"/> that exposes a read-only memory-mapped view of a file as
///  <see cref="System.Memory{T}"/> / <see cref="ReadOnlyMemory{T}"/> without copying its contents.
/// </summary>
/// <remarks>
///  <para>
///   The mapped view has a fixed address for its lifetime, so <see cref="Pin"/> and <see cref="Unpin"/>
///   are trivial; <see cref="MemoryManager{T}"/> is only used to turn the native view pointer into a
///   <see cref="ReadOnlyMemory{T}"/>. The view keeps the mapping alive, so the file and mapping handles
///   are released as soon as the view exists; only the view is held for the life of this manager.
///  </para>
///  <para>
///   The backing view is mapped read-only. <see cref="MemoryManager{T}"/> requires the buffer to be
///   exposed as a writable <see cref="Span{T}"/> / <see cref="System.Memory{T}"/>, but the contents
///   must be treated as read-only: writing through the returned span or memory faults with an
///   <see cref="AccessViolationException"/> because the underlying pages are read-only. Prefer
///   consuming it as <see cref="ReadOnlyMemory{T}"/> / <see cref="ReadOnlySpan{T}"/>.
///  </para>
///  <para>
///   Retain and dispose the manager itself to unmap the view. Do not use the memory after disposing.
///  </para>
/// </remarks>
internal sealed unsafe class MappedMemoryManager : MemoryManager<byte>
{
    private readonly MappedViewLease _lease;
    private readonly byte* _pointer;
    private readonly int _length;
    private bool _disposed;

    private MappedMemoryManager(MappedViewLease lease, int length)
    {
        _lease = lease;
        _pointer = lease.Pointer;
        _length = length;
    }

    /// <summary>
    ///  Creates a <see cref="MappedMemoryManager"/> over a read-only memory mapping of the file at
    ///  <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the file to map.</param>
    /// <returns>A manager that owns the mapping until it is disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file is empty or larger than <see cref="int.MaxValue"/> bytes.</exception>
    public static MappedMemoryManager CreateFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        long length = stream.Length;
        if (length is <= 0 or > int.MaxValue)
        {
            throw new IOException($"'{path}' is empty or too large to memory-map.");
        }

        using MemoryMappedFile file = MemoryMappedFile.CreateFromFile(
            stream,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);

        MemoryMappedViewAccessor accessor = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
        MappedViewLease lease = new(accessor);
        return new MappedMemoryManager(lease, (int)length);
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new(_pointer, _length);
    }

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, _length);
        return new(_pointer + elementIndex, handle: default, this);
    }

    /// <inheritdoc/>
    public override void Unpin()
    {
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lease.Dispose();
    }
}
