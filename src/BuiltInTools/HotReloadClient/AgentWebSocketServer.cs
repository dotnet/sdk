// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// WebSocket server for hot reload communication with the agent in the target process/device.
/// Extends KestrelWebSocketServer to handle a single client connection.
/// Uses RSA-based shared secret for authentication (same as BrowserRefreshServer).
/// </summary>
internal sealed class AgentWebSocketServer : KestrelWebSocketServer
{
    private WebSocket? _clientSocket;
    private readonly TaskCompletionSource<WebSocket?> _clientConnectedSource = new();
    private readonly SharedSecretProvider _sharedSecretProvider = new();

    public AgentWebSocketServer(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// Returns true if the server has been started.
    /// </summary>
    public bool IsStarted => Host != null;

    /// <summary>
    /// Gets the first bound WebSocket URL (e.g., "ws://127.0.0.1:12345").
    /// Only valid after server has started.
    /// </summary>
    public string WebSocketUrl
        => ServerUrls.Select(ConvertToWebSocketUrl).FirstOrDefault() ?? throw new InvalidOperationException("Server not started");

    /// <summary>
    /// Gets the bound port number. Only valid after server has started.
    /// </summary>
    public int BoundPort => new Uri(WebSocketUrl).Port;

    /// <summary>
    /// Gets the RSA public key (Base64-encoded X.509 SubjectPublicKeyInfo) for client authentication.
    /// </summary>
    public string PublicKey => _sharedSecretProvider.GetPublicKey();

    public async ValueTask StartServerAsync(string hostName, int port, CancellationToken cancellationToken)
    {
        await base.StartServerAsync(hostName, port, useTls: false, cancellationToken);
    }

    protected override async Task HandleRequestAsync(HttpContext context)
    {
        // Validate the shared secret from the subprotocol
        if (context.WebSockets.WebSocketRequestedProtocols is not [var subProtocol] || string.IsNullOrEmpty(subProtocol))
        {
            Logger.LogWarning("WebSocket connection rejected: missing subprotocol (shared secret)");
            context.Response.StatusCode = 401;
            return;
        }

        // Decrypt and validate the secret
        // The client sends URL-safe Base64 (- instead of +, _ instead of /, no padding)
        // because WebSocket subprotocol tokens can't contain those characters.
        string? decryptedSecret;
        try
        {
            decryptedSecret = _sharedSecretProvider.DecryptSecret(Base64Url.DecodeToStandardBase64(subProtocol));
        }
        catch (Exception ex)
        {
            Logger.LogWarning("WebSocket connection rejected: invalid shared secret - {Message}", ex.Message);
            context.Response.StatusCode = 401;
            return;
        }

        if (string.IsNullOrEmpty(decryptedSecret))
        {
            Logger.LogWarning("WebSocket connection rejected: empty shared secret");
            context.Response.StatusCode = 401;
            return;
        }

        var webSocket = await AcceptWebSocketAsync(context, subProtocol);
        if (webSocket == null)
        {
            return;
        }

        Logger.LogDebug("WebSocket client connected");

        _clientSocket = webSocket;
        _clientConnectedSource.TrySetResult(webSocket);

        // Keep the request alive until the connection is closed or aborted
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Expected when the client disconnects or the request is aborted
        }

        Logger.LogDebug("WebSocket client disconnected");
    }

    public async ValueTask<WebSocket?> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        return await _clientConnectedSource.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask SendMessageAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
    {
        if (_clientSocket == null || _clientSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("No active WebSocket connection from the client.");
        }

        await _clientSocket.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
    }

    public async ValueTask<MemoryStream?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        if (_clientSocket == null || _clientSocket.State != WebSocketState.Open)
        {
            return null;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    stream.Dispose();
                    return null;
                }
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            stream.Position = 0;
            return stream;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override void Dispose()
    {
        _clientSocket?.Dispose();
        _sharedSecretProvider.Dispose();
        base.Dispose();
    }
}

#endif
