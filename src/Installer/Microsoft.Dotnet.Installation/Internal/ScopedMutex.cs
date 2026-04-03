// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

public class ScopedMutex : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _isReentrant;

    // Track recursive holds on a per-execution-context basis, keyed by mutex name.
    private static readonly AsyncLocal<Dictionary<string, MutexState>> s_heldMutexCounts = new();

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
        Name = name;

        var held = s_heldMutexCounts.Value ??= [with(StringComparer.OrdinalIgnoreCase)];

        if (held.TryGetValue(name, out var state))
        {
            // Re-entrant: this thread already holds this mutex
            ++state.HoldCount;
            _isReentrant = true;
            _mutex = null!;
            return;
        }

        // First acquisition: create and acquire the OS mutex
        _isReentrant = false;

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
            if (!_mutex.WaitOne(0, false))
            {
                // Another process holds the mutex - notify caller before blocking
                OnWaitingForMutex?.Invoke();

                // Now wait for the full timeout
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(TimeoutSeconds), false))
                {
                    _mutex.Dispose();
                    throw new TimeoutException($"Could not acquire mutex '{name}' within {TimeoutSeconds} seconds.");
                }
            }
        }
        catch (AbandonedMutexException)
        {
            // A previous process holding the mutex exited without releasing it.
            // The OS still grants ownership to this thread, so we can proceed safely.
        }

        held[name] = new MutexState { Mutex = _mutex, HoldCount = 1 };
    }

    public string Name { get; }
    public static bool CurrentThreadHoldsMutex => s_heldMutexCounts.Value is { Count: > 0 };

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_isReentrant)
        {
            // Re-entrant: just decrement the hold count
            var held = s_heldMutexCounts.Value ??= [with(StringComparer.OrdinalIgnoreCase)];
            if (held.TryGetValue(Name, out var state))
            {
                state.HoldCount--;
                if (state.HoldCount <= 0)
                {
                    // This shouldn't normally happen (it means too many Disposes),
                    // but clean up anyway
                    held.Remove(Name);
                }
            }
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        finally
        {
            s_heldMutexCounts.Value?.Remove(Name);
            _mutex.Dispose();
        }
    }

    private class MutexState
    {
        public Mutex Mutex { get; init; } = null!;
        public int HoldCount { get; set; }
    }
}
