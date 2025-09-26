// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class ScopedMutex : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;

    public ScopedMutex(string name)
    {
        _mutex = new Mutex(false, name);
        _hasHandle = _mutex.WaitOne(TimeSpan.FromSeconds(10), false);
    }

    public bool HasHandle => _hasHandle;

    public void Dispose()
    {
        if (_hasHandle)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
