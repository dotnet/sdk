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
    /// Timeout in seconds for mutex acquisition. Default is 5 minutes.
    /// </summary>
    public static int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Optional callback invoked when we need to wait for the mutex (another process holds it).
    /// </summary>
    public static Action? OnWaitingForMutex { get; set; }

    // Process-wide count of non-reentrant mutex holds across all async contexts.
    // Used to suppress the "waiting" callback when the holder is within this same process.
    private static int s_processActiveHolds;

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
        if (Environment.OSVersion.Platform != PlatformID.Win32NT && mutexName.StartsWith("Global\\", StringComparison.Ordinal))
        {
            mutexName = mutexName.Substring(7);
        }

        _mutex = new Mutex(false, mutexName);

        // First try immediate acquisition to see if we need to wait
        if (!_mutex.WaitOne(0, false))
        {
            // Only invoke the callback when an *external* process holds the mutex.
            // Suppress it when another task within this process holds it so we don't
            // show misleading "waiting for mutex" text when we're waiting on ourselves.
            if (Volatile.Read(ref s_processActiveHolds) == 0)
            {
                OnWaitingForMutex?.Invoke();
            }

            // Now wait for the full timeout
            if (!_mutex.WaitOne(TimeSpan.FromSeconds(TimeoutSeconds), false))
            {
                _mutex.Dispose();
                throw new TimeoutException($"Could not acquire mutex '{name}' within {TimeoutSeconds} seconds.");
            }
        }

        Interlocked.Increment(ref s_processActiveHolds);
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
            Interlocked.Decrement(ref s_processActiveHolds);
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
