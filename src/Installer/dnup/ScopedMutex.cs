// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class ScopedMutex : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;
    // Track recursive holds on a per-thread basis so we can assert manifest access without re-acquiring.
    private static readonly ThreadLocal<int> _holdCount = new(() => 0);

    public ScopedMutex(string name)
    {
        try
        {
            // On Linux and Mac, "Global\" prefix doesn't work - strip it if present
            string mutexName = name;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT && mutexName.StartsWith("Global\\"))
            {
                mutexName = mutexName.Substring(7);
            }

            _mutex = new Mutex(false, mutexName);
            _hasHandle = _mutex.WaitOne(TimeSpan.FromSeconds(120), false);
            if (_hasHandle)
            {
                _holdCount.Value = _holdCount.Value + 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not create or acquire mutex '{name}': {ex.Message}");
            throw;
        }
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
