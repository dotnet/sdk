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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not create or acquire mutex '{name}': {ex.Message}");
            throw ex;
        }
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
