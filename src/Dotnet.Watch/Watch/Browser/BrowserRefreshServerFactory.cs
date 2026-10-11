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
/// The instance is reused while the associated process is still running (for example
/// when the project graph is reloaded). When process termination is requested the
/// server is disposed first so that forwarded browser connections do not block
/// application shutdown; a new server is created on the next launch.
///
/// Each connection loads the current browser tools session key from the key pair the project's
/// build wrote to its intermediate output. Loading on connect allows the server to start before
/// <c>dotnet run</c> builds the application and naturally observes key rotation after a clean.
/// </summary>
internal sealed class BrowserRefreshServerFactory : IDisposable
{
    private readonly Lock _serversGuard = new();

    // Null value is cached for project instances that are not web projects or do not support browser refresh for other reason.
    private readonly Dictionary<ProjectInstanceId, BrowserRefreshServer?> _servers = [];

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
    }

    /// <summary>
    /// Disposes the refresh server associated with <paramref name="projectNode"/>, if any.
    /// Call this when process termination is requested so that forwarded browser connections
    /// do not block application shutdown. A subsequent launch creates a new server.
    /// </summary>
    public void DisposeRefreshServer(ProjectGraphNode projectNode)
    {
        BrowserRefreshServer? server;
        var key = projectNode.ProjectInstance.GetId();

        lock (_serversGuard)
        {
            if (!_servers.TryGetValue(key, out server) || server == null)
            {
                return;
            }

            _servers.Remove(key);
        }

        server.Dispose();
    }

    public async ValueTask<BrowserRefreshServer?> GetOrCreateBrowserRefreshServerAsync(ProjectGraphNode projectNode, WebApplicationAppModel appModel, CancellationToken cancellationToken)
    {
        BrowserRefreshServer? server;
        bool hasExistingServer;

        var key = projectNode.ProjectInstance.GetId();
        var browserToolsProject = appModel.BrowserToolsProject;

        lock (_serversGuard)
        {
            hasExistingServer = _servers.TryGetValue(key, out server);

            if (server != null)
            {
                if (BrowserToolsBuildOutputs.TryGetFor(browserToolsProject, server.Logger) is not { } outputs)
                {
                    server.Dispose();
                    _servers.Remove(key);
                    server = null;
                    hasExistingServer = false;
                }
                else
                {
                    server.UpdateSessionKeyFactory(outputs.CreateSessionKey);
                }
            }

            if (!hasExistingServer)
            {
                server = appModel.TryCreateRefreshServer(browserToolsProject);
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
