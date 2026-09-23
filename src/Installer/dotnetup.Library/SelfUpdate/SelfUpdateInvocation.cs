// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Owns command coordination through synchronous process shutdown, including telemetry flush.</summary>
internal sealed class SelfUpdateInvocation : IDisposable
{
    private readonly SelfUpdateInvocation? _previous;
    private readonly List<IDisposable> _leases = [];
    private bool _passedGate;
    private bool _disposed;

    public SelfUpdateInvocation(string executablePath, string loadedVersion)
    {
        Paths = new SelfUpdatePaths(executablePath);
        LoadedVersion = loadedVersion;
        _previous = Current;
        Current = this;
    }

    [field: ThreadStatic]
    public static SelfUpdateInvocation? Current { get; private set; }
    public SelfUpdatePaths Paths { get; }
    public string LoadedVersion { get; }

    public void EnterCommand(bool safe)
    {
        if (!safe && !_passedGate)
        {
            Retain(NonSafeCommandGate.Enter(Paths, LoadedVersion));
            _passedGate = true;
        }

        if (!safe)
        {
            SelfUpdateCleanup.TryRun(Paths.InstalledPath, LoadedVersion);
        }

    }

    public void Retain(IDisposable lease) => _leases.Add(lease);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            for (var index = _leases.Count - 1; index >= 0; index--)
            {
                _leases[index].Dispose();
            }
        }
        finally
        {
            Current = _previous;
        }
    }
}