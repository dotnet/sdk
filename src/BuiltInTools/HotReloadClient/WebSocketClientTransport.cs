// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// WebSocket transport for communication between dotnet-watch and the hot reload agent.
/// Used for projects with the HotReloadWebSockets capability (e.g., Android, iOS)
/// where named pipes don't work over the network.
/// </summary>
internal sealed class WebSocketClientTransport(int port, ILogger logger) : ClientTransport
{
    private readonly AgentWebSocketServer _server = new(logger);

    /// <summary>
    /// The bound port number, for testing. Only valid after server has started.
    /// </summary>
    internal int Port => _server.BoundPort;

    public override void ConfigureEnvironment(IDictionary<string, string> env)
    {
        // Start the server now so we know the actual bound port (when using port 0 for auto-assign)
        EnsureServerStarted();

        // Set the WebSocket endpoint for the app to connect to.
        // Use the actual bound URL from the server (important when port 0 was requested).
        env[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint] = _server.WebSocketUrl;

        // Set the RSA public key for the client to encrypt its shared secret.
        // This is the same authentication mechanism used by BrowserRefreshServer.
        env[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketKey] = _server.PublicKey;
    }

    private void EnsureServerStarted()
    {
        if (_server.IsStarted)
        {
            return;
        }

        // Start Kestrel server with WebSocket support.
        // Use 127.0.0.1 instead of "localhost" because Kestrel doesn't support dynamic port binding with "localhost".
        // System.InvalidOperationException: Dynamic port binding is not supported when binding to localhost.
        // You must either bind to 127.0.0.1:0 or [::1]:0, or both.
        _server.StartServerAsync("127.0.0.1", port, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override async Task<string> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        // Server should already be started by ConfigureEnvironment, but ensure it's started
        EnsureServerStarted();

        // Wait for the client to connect
        if (await _server.WaitForConnectionAsync(cancellationToken) == null)
        {
            return "";
        }

        // Read the initialization message (capabilities)
        using var stream = await _server.ReceiveMessageAsync(cancellationToken);
        if (stream == null)
        {
            return "";
        }

        // Parse capabilities
        var capabilities = await ClientInitializationResponse.ReadAsync(stream, cancellationToken);
        return capabilities.Capabilities;
    }

    public override async ValueTask WriteAsync(byte type, Func<Stream, CancellationToken, ValueTask>? writePayload, CancellationToken cancellationToken)
    {
        // Serialize the complete message to a buffer, then send as a single WebSocket message
        using var buffer = new MemoryStream();
        await buffer.WriteAsync(type, cancellationToken);

        if (writePayload != null)
        {
            await writePayload(buffer, cancellationToken);
        }

        await _server.SendMessageAsync(new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), cancellationToken);
    }

    public override async ValueTask<ClientTransportResponse?> ReadAsync(CancellationToken cancellationToken)
    {
        // Receive a complete WebSocket message
        var stream = await _server.ReceiveMessageAsync(cancellationToken);
        if (stream == null)
        {
            return null;
        }

        // Read the response type byte from the message
        var type = (ResponseType)await stream.ReadByteAsync(cancellationToken);
        return new ClientTransportResponse(type, stream, disposeStream: true);
    }

    public override void Dispose()
    {
        logger.LogDebug("Disposing agent websocket transport");
        _server.Dispose();
    }
}

#endif
