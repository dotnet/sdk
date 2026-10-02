// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Owns an acquired memory-mapped view pointer and releases it during disposal or finalization.
/// </summary>
internal sealed unsafe class MappedViewLease : DisposableBase.Finalizable
{
    private MemoryMappedViewAccessor? _accessor;

    /// <summary>
    ///  Initializes a lease that owns <paramref name="accessor"/> and its acquired pointer.
    /// </summary>
    /// <param name="accessor">The mapped view to own.</param>
    internal MappedViewLease(MemoryMappedViewAccessor accessor)
    {
        byte* pointer = null;
        try
        {
            long pointerOffset = accessor.PointerOffset;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            _accessor = accessor;
            Pointer = pointer + pointerOffset;
        }
        catch
        {
            accessor.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  The adjusted pointer to the first mapped byte.
    /// </summary>
    internal byte* Pointer { get; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCore();
            return;
        }

        try
        {
            DisposeCore();
        }
        catch
        {
        }
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _accessor, value: null) is not { } accessor)
        {
            return;
        }

        try
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
        finally
        {
            accessor.Dispose();
        }
    }
}