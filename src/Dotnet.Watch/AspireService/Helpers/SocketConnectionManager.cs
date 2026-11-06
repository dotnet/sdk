// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Aspire.Tools.Service;

/// <summary>
/// Manages the set of active socket connections. Since it registers to be notified when a socket has gone bad,
/// it also tracks those CancellationTokenRegistration objects so they can be disposed
/// </summary>
internal class SocketConnectionManager : IDisposable
{
    // Track a single connection per DCP ID
    private ImmutableDictionary<string, WebSocketConnection> _webSocketConnections =
        ImmutableDictionary<string, WebSocketConnection>.Empty;

    private void CleanupSocketConnections()
    {
        var connections = Interlocked.Exchange(ref _webSocketConnections, ImmutableDictionary<string, WebSocketConnection>.Empty);

        foreach (var (_, connection) in connections)
        {
            connection.Dispose();
        }
    }

    public void AddSocketConnection(WebSocket socket, TaskCompletionSource tcs, string dcpId, CancellationToken httpRequestAborted)
    {
        // We only support one connection per DCP ID, therefore if there is
        // already a connection, drop that one before adding this one

        var newConnection = new WebSocketConnection(socket, tcs, dcpId, httpRequestAborted);

        var (oldConnections, _) = ImmutableInterlocked.Transform(ref _webSocketConnections, connections => connections.SetItem(dcpId, newConnection));

        if (oldConnections.TryGetValue(dcpId, out var oldConnection))
        {
            oldConnection.Dispose();
        }

        // Hook up removal from tracked connections on abort after the connection has been added:
        newConnection.RegisterCancellationCallback(RemoveSocketConnection);
    }

    public void RemoveSocketConnection(WebSocketConnection connection)
    {
        // If the connection is not in the dictionary, then it has already been removed and disposed or replaced with another connection.
        if (ImmutableInterlocked.Update(ref _webSocketConnections,
            connections => connections.TryGetValue(connection.DcpId, out var currentConnection) && currentConnection == connection
                ? connections.Remove(connection.DcpId)
                : connections))
        {
            connection.Dispose();
        }
    }

    public WebSocketConnection? GetSocketConnection(string dcpId)
        => _webSocketConnections.GetValueOrDefault(dcpId);

    public void Dispose()
    {
        CleanupSocketConnections();
    }
}
