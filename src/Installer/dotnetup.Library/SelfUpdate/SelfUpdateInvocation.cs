// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Owns command coordination through synchronous process shutdown, including telemetry flush.</summary>
internal sealed class SelfUpdateInvocation : IDisposable
{
    [ThreadStatic]
    private static SelfUpdateInvocation? s_current;
    private readonly SelfUpdateInvocation? _previous;
    private readonly List<IDisposable> _leases = [];
    private bool _passedGate;
    private bool _disposed;

    public SelfUpdateInvocation(string executablePath, string loadedIdentity)
    {
        Paths = new SelfUpdatePaths(executablePath);
        LoadedIdentity = loadedIdentity;
        _previous = s_current;
        s_current = this;
    }

    public static SelfUpdateInvocation? Current => s_current;
    public SelfUpdatePaths Paths { get; }
    public string LoadedIdentity { get; }

    public void EnterCommand(bool safe)
    {
        if (!safe && !_passedGate)
        {
            Retain(NonSafeCommandGate.Enter(Paths, LoadedIdentity));
            _passedGate = true;
        }

        if (!safe)
        {
            SelfUpdateCleanup.TryRun(Paths.InstalledPath, LoadedIdentity);
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
            s_current = _previous;
        }
    }
}