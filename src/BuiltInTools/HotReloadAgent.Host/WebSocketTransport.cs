// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// WebSocket-based client for hot reload communication.
/// Used for projects with the HotReloadWebSockets capability (e.g., Android, iOS).
/// Mobile workloads add this capability since named pipes don't work over the network.
/// </summary>
internal sealed class WebSocketTransport : Transport
{
    private readonly string _serverUrl;
    private readonly int _connectionTimeoutMS;
    private readonly ClientWebSocket _webSocket = new();

    // Buffer for receiving messages - WebSocket messages need to be read completely
    private MemoryStream? _receiveBuffer;

    public WebSocketTransport(string serverUrl, Action<string> log, int connectionTimeoutMS)
        : base(log)
    {
        // Convert http:// to ws:// or https:// to wss://
        _serverUrl = serverUrl
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);

        _connectionTimeoutMS = connectionTimeoutMS;
    }

    public override void Dispose()
    {
        _webSocket.Dispose();
        _receiveBuffer?.Dispose();
    }

    public override string DisplayName
        => $"WebSocket {_serverUrl}";

    public override async ValueTask SendAsync(IResponse response, CancellationToken cancellationToken)
    {
        // Connect on first send (which is InitializationResponse)
        if (response.Type == ResponseType.InitializationResponse)
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_connectionTimeoutMS);

            try
            {
                Log($"Connecting to {_serverUrl}...");
                await _webSocket.ConnectAsync(new Uri(_serverUrl), connectCts.Token);
                Log("Connected.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Failed to connect in {_connectionTimeoutMS}ms.");
            }
        }

        // Serialize the response to a buffer
        using var buffer = new MemoryStream();

        // Write response type prefix (except for InitializationResponse which doesn't need it)
        if (response.Type != ResponseType.InitializationResponse)
        {
            await buffer.WriteAsync((byte)response.Type, cancellationToken);
        }

        await response.WriteAsync(buffer, cancellationToken);

        Log($"Sending {response.Type} ({buffer.Length} bytes)");

        // Send as binary WebSocket message
        await _webSocket.SendAsync(
            new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken);
    }

    public override async ValueTask<RequestStream> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            return new RequestStream(stream: null, disposeOnCompletion: false);
        }

        // Read the complete WebSocket message into a buffer
        _receiveBuffer ??= new MemoryStream();
        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log("Server closed connection.");
                    return new RequestStream(stream: null, disposeOnCompletion: false);
                }

                _receiveBuffer.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Log($"Received {_receiveBuffer.Length} bytes");
        _receiveBuffer.Position = 0;

        // Return a stream that doesn't dispose the underlying buffer (we reuse it)
        return new RequestStream(_receiveBuffer, disposeOnCompletion: false);
    }
}
