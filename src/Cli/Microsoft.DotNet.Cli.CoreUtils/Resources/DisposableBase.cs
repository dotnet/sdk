// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Base class for implementing <see cref="IDisposable"/> with double disposal protection.
/// </summary>
internal abstract partial class DisposableBase : IDisposable
{
    private int _disposedValue;

    /// <summary>
    ///  Gets a value indicating whether the object has been disposed.
    /// </summary>
    /// <value><see langword="true"/> if the object has been disposed; otherwise, <see langword="false"/>.</value>
    protected bool Disposed => _disposedValue != 0;

    /// <summary>
    ///  Called when the component is being disposed or finalized.
    /// </summary>
    /// <param name="disposing">
    ///  <see langword="false"/> if called via a destructor on the finalizer queue. Do not access object fields
    ///  unless <see langword="true"/>.
    /// </param>
    protected abstract void Dispose(bool disposing);

    private void DisposeInternal(bool disposing)
    {
        // Want to ensure both paths are guarded against double disposal.
        if (Interlocked.Exchange(ref _disposedValue, 1) == 1)
        {
            return;
        }

        Dispose(disposing);
    }

    /// <summary>
    ///  Disposes the component.
    /// </summary>
    public void Dispose()
    {
        DisposeInternal(disposing: true);
        GC.SuppressFinalize(this);
    }
}
