// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Represents a connection to a browser that facilitates Hot Reload operations.
/// </summary>
internal readonly struct BrowserConnection(WebSocket clientSocket, string? sharedSecret, int id, ILogger serverLogger, ILogger agentLogger) : IDisposable
{
    public readonly TaskCompletionSource<None> Disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        ClientSocket.Dispose();

        Disconnected.TrySetResult(default);
        ServerLogger.LogDebug("Disconnected.");
    }

    public WebSocket ClientSocket => clientSocket;
    public string? SharedSecret => sharedSecret;
    public int Id => id;
    public ILogger ServerLogger => serverLogger;
    public ILogger AgentLogger => agentLogger;

    internal async ValueTask<bool> TrySendMessageAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
    {
#if NET
        var data = messageBytes;
#else
        var data = new ArraySegment<byte>(messageBytes.ToArray());
#endif
        try
        {
            await ClientSocket.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            ServerLogger.LogDebug("Failed to send message: {Message}", e.Message);
            return false;
        }

        return true;
    }

    internal async ValueTask<TResponseResult?> TryReceiveMessageAsync<TResponseResult>(ResponseFunc<TResponseResult> receiver, CancellationToken cancellationToken)
        where TResponseResult : struct
    {
        var writer = new ArrayBufferWriter<byte>(initialCapacity: 1024);

        while (true)
        {
#if NET
            ValueWebSocketReceiveResult result;
            var data = writer.GetMemory();
#else
            WebSocketReceiveResult result;
            var data = writer.GetArraySegment();
#endif
            try
            {
                result = await ClientSocket.ReceiveAsync(data, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ServerLogger.LogDebug("Failed to receive response: {Message}", e.Message);
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            writer.Advance(result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return receiver(writer.WrittenSpan, AgentLogger);
    }
}
