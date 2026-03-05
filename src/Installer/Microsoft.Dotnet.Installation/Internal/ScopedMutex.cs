// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

public class ScopedMutex : IDisposable
{
    private readonly Mutex? _mutex;
    private readonly bool _isReentrant;

    // Track recursive holds on a per-thread basis, keyed by mutex name.
#pragma warning disable IDE0028 // Collection expression not applicable with IEqualityComparer ctor
    private static readonly ThreadLocal<Dictionary<string, MutexState>> s_heldMutexes = new(() => new Dictionary<string, MutexState>(StringComparer.OrdinalIgnoreCase));
#pragma warning restore IDE0028

    /// <summary>
    /// Timeout in seconds for mutex acquisition. Default is 5 minutes.
    /// </summary>
    public static int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Optional callback invoked when we need to wait for the mutex (another process holds it).
    /// </summary>
    public static Action? OnWaitingForMutex { get; set; }

    public ScopedMutex(string name)
    {
        Name = name;
        var held = s_heldMutexes.Value!;

        if (held.TryGetValue(name, out var state))
        {
            // Re-entrant: this thread already holds this mutex
            state.HoldCount++;
            _isReentrant = true;
            _mutex = null;
            HasHandle = true;
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

        // First try immediate acquisition to see if we need to wait
        HasHandle = _mutex.WaitOne(0, false);
        if (!HasHandle)
        {
            // Another process holds the mutex - notify caller before blocking
            OnWaitingForMutex?.Invoke();

            // Now wait for the full timeout
            HasHandle = _mutex.WaitOne(TimeSpan.FromSeconds(TimeoutSeconds), false);
        }

        if (HasHandle)
        {
            held[name] = new MutexState { Mutex = _mutex, HoldCount = 1 };
        }
    }

    public string Name { get; }
    public bool HasHandle { get; }
    public static bool CurrentThreadHoldsMutex => s_heldMutexes.Value!.Count > 0;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_isReentrant)
        {
            // Re-entrant: just decrement the hold count
            var held = s_heldMutexes.Value!;
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

        if (HasHandle && _mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            finally
            {
                s_heldMutexes.Value!.Remove(Name);
            }
        }

        _mutex?.Dispose();
    }

    private class MutexState
    {
        public Mutex Mutex { get; init; } = null!;
        public int HoldCount { get; set; }
    }
}
