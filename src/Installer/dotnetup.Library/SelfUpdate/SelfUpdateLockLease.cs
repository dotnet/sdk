// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Owns an acquired update/activity pair and releases activity before update.
/// Neither permanent lock file is deleted when the lease is disposed.
/// </summary>
internal sealed class SelfUpdateLockLease : IDisposable
{
    private readonly ScopedLockFile _updateLock;
    private readonly ScopedLockFile _activityLock;
    private int _disposed;

    internal SelfUpdateLockLease(ScopedLockFile updateLock, ScopedLockFile activityLock)
    {
        _updateLock = updateLock;
        _activityLock = activityLock;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _activityLock.Dispose();
        }
        finally
        {
            _updateLock.Dispose();
        }
    }
}