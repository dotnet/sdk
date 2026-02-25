// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

public class ScopedMutex : IDisposable
{
    private readonly Mutex _mutex;
    // Track recursive holds on a per-thread basis so we can assert manifest access without re-acquiring.
    private static readonly ThreadLocal<int> s_holdCount = new(() => 0);

    /// <summary>
    /// Timeout in seconds for mutex acquisition. Default is 10 minutes.
    /// CI agents (especially Helix) can be very slow, so this must be generous.
    /// </summary>
    public static int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Optional callback invoked when we need to wait for the mutex (another process holds it).
    /// </summary>
    public static Action? OnWaitingForMutex { get; set; }

    public ScopedMutex(string name)
    {
        // On Linux and Mac, "Global\" prefix doesn't work - strip it if present
        string mutexName = name;
        if (Environment.OSVersion.Platform != PlatformID.Win32NT && mutexName.StartsWith("Global\\"))
        {
            mutexName = mutexName.Substring(7);
        }

        _mutex = new Mutex(false, mutexName);

        try
        {
            // First try immediate acquisition to see if we need to wait
            HasHandle = _mutex.WaitOne(0, false);
            if (!HasHandle)
            {
                // Another process holds the mutex - notify caller before blocking
                OnWaitingForMutex?.Invoke();

                // Now wait for the full timeout
                HasHandle = _mutex.WaitOne(TimeSpan.FromSeconds(TimeoutSeconds), false);
            }
        }
        catch (AbandonedMutexException)
        {
            // A previous process holding the mutex exited without releasing it.
            // The OS still grants ownership to this thread, so we can proceed safely.
            HasHandle = true;
        }

        if (HasHandle)
        {
            s_holdCount.Value = s_holdCount.Value + 1;
        }
        // Note: If HasHandle is false, caller should check HasHandle property.
        // We don't throw here because:
        // 1. The mutex may be acquired multiple times in a single process flow
        // 2. The caller may want to handle the failure gracefully
        // Telemetry for lock contention is recorded by the caller when HasHandle is false.
    }

    public bool HasHandle { get; }
    public static bool CurrentThreadHoldsMutex => s_holdCount.Value > 0;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (HasHandle)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            finally
            {
                // Decrement hold count even if release throws.
                if (s_holdCount.Value > 0)
                {
                    s_holdCount.Value = s_holdCount.Value - 1;
                }
            }
        }
        _mutex.Dispose();
    }
}
