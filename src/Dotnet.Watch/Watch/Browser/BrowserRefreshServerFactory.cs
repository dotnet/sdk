// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Creates <see cref="BrowserRefreshServer"/> instances.
///
/// An instance is created for each project that supports browser launching.
/// When the project is rebuilt and restarted we reuse the same refresh server and browser instance.
/// Reload message is sent to the browser in that case.
///
/// The instances are also reused if the project file is updated or the project graph is reloaded.
///
/// Owns the browser tools session key. A single key pair is created per <c>dotnet watch</c>
/// invocation, before any project is built, because the public half has to be passed to the build as
/// a global MSBuild property and global properties apply to the whole project graph. Per project keys
/// are therefore not expressible: the key has to exist before the graph and the app model are known.
/// Project isolation is unaffected - each provider listens on its own loopback port that is only
/// reachable through its own application's forwarder, and all providers of an invocation live in this
/// process and share its trust domain. Aspire runs a separate watcher process per app host, so it
/// gets a separate key.
/// </summary>
internal sealed class BrowserRefreshServerFactory : IDisposable
{
    private readonly Lock _serversGuard = new();

    // Null value is cached for project instances that are not web projects or do not support browser refresh for other reason.
    private readonly Dictionary<ProjectInstanceId, BrowserRefreshServer?> _servers = [];

    /// <summary>
    /// The private half never leaves this process: it is not written to disk, to build output, to
    /// MSBuild properties or to logs. Only the public half is handed to the build.
    /// </summary>
    private readonly SharedSecretProvider _sessionKey = new();

    /// <summary>
    /// Base64 encoded X.509 SubjectPublicKeyInfo of the session key, stable for the lifetime of the
    /// watch invocation including incremental rebuilds. A new invocation rotates it.
    /// </summary>
    public string PublicKey { get; }

    public SharedSecretProvider SessionKey
        => _sessionKey;

    public BrowserRefreshServerFactory()
    {
        PublicKey = _sessionKey.GetPublicKey();
    }

    public void Dispose()
    {
        BrowserRefreshServer?[] serversToDispose;

        lock (_serversGuard)
        {
            serversToDispose = [.. _servers.Values];
            _servers.Clear();
        }

        foreach (var server in serversToDispose)
        {
            server?.Dispose();
        };

        _sessionKey.Dispose();
    }

    public async ValueTask<BrowserRefreshServer?> GetOrCreateBrowserRefreshServerAsync(ProjectGraphNode projectNode, WebApplicationAppModel appModel, CancellationToken cancellationToken)
    {
        BrowserRefreshServer? server;
        bool hasExistingServer;

        var key = projectNode.ProjectInstance.GetId();

        lock (_serversGuard)
        {
            hasExistingServer = _servers.TryGetValue(key, out server);
            if (!hasExistingServer)
            {
                server = appModel.TryCreateRefreshServer(projectNode);
                _servers.Add(key, server);
            }
        }

        if (server == null)
        {
            // browser refresh server isn't supported
            return null;
        }

        if (!hasExistingServer)
        {
            // Start the server we just created:
            await server.StartAsync(cancellationToken);
        }

        return server;
    }

    public bool TryGetRefreshServer(ProjectGraphNode projectNode, [NotNullWhen(true)] out BrowserRefreshServer? server)
    {
        var key = projectNode.ProjectInstance.GetId();

        lock (_serversGuard)
        {
            return _servers.TryGetValue(key, out server) && server != null;
        }
    }
}
