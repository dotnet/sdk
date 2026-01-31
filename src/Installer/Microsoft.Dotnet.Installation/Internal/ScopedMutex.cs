// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Dotnet.Installation.Internal;

public class ScopedMutex : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;
    // Track recursive holds on a per-thread basis so we can assert manifest access without re-acquiring.
    private static readonly ThreadLocal<int> _holdCount = new(() => 0);

    /// <summary>
    /// Timeout in seconds for mutex acquisition. Default is 5 minutes.
    /// </summary>
    public static int TimeoutSeconds { get; set; } = 300;

    public ScopedMutex(string name)
    {
        // On Linux and Mac, "Global\" prefix doesn't work - strip it if present
        string mutexName = name;
        if (Environment.OSVersion.Platform != PlatformID.Win32NT && mutexName.StartsWith("Global\\"))
        {
            mutexName = mutexName.Substring(7);
        }

        _mutex = new Mutex(false, mutexName);
        _hasHandle = _mutex.WaitOne(TimeSpan.FromSeconds(TimeoutSeconds), false);
        if (_hasHandle)
        {
            _holdCount.Value = _holdCount.Value + 1;
        }
        // Note: If _hasHandle is false, caller should check HasHandle property.
        // We don't throw here because:
        // 1. The mutex may be acquired multiple times in a single process flow
        // 2. The caller may want to handle the failure gracefully
        // Telemetry for lock contention is recorded by the caller when HasHandle is false.
    }

    public bool HasHandle => _hasHandle;
    public static bool CurrentThreadHoldsMutex => _holdCount.Value > 0;

    public void Dispose()
    {
        if (_hasHandle)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            finally
            {
                // Decrement hold count even if release throws.
                if (_holdCount.Value > 0)
                {
                    _holdCount.Value = _holdCount.Value - 1;
                }
            }
        }
        _mutex.Dispose();
    }
}