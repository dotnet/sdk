// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal readonly struct BrowserConnection : IAsyncDisposable
{
    public const string ServerLogComponentName = $"{nameof(BrowserConnection)}:Server";
    public const string AgentLogComponentName = $"{nameof(BrowserConnection)}:Agent";

    private static int s_lastId;

    public WebSocket ClientSocket { get; }
    public string? SharedSecret { get; }
    public int Id { get; }
    public ILogger ServerLogger { get; }
    public ILogger AgentLogger { get; }

    public BrowserConnection(WebSocket clientSocket, string? sharedSecret, ILoggerFactory loggerFactory)
    {
        ClientSocket = clientSocket;
        SharedSecret = sharedSecret;
        Id = Interlocked.Increment(ref s_lastId);

        var displayName = $"Browser #{Id}";
        ServerLogger = loggerFactory.CreateLogger(ServerLogComponentName, displayName);
        AgentLogger = loggerFactory.CreateLogger(AgentLogComponentName, displayName);

        ServerLogger.LogDebug("Connected to referesh server.");
    }

    public async ValueTask DisposeAsync()
    {
        await ClientSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, default);
        ClientSocket.Dispose();

        ServerLogger.LogDebug("Disconnected.");
    }

    internal async ValueTask<bool> TrySendMessageAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
    {
        try
        {
            await ClientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            ServerLogger.LogDebug("Failed to send message: {Message}", e.Message);
            return false;
        }

        return true;
    }

    internal async ValueTask<bool> TryReceiveMessageAsync(Action<ReadOnlySpan<byte>, ILogger> receiver, CancellationToken cancellationToken)
    {
        var writer = new ArrayBufferWriter<byte>(initialCapacity: 1024);

        while (true)
        {
            ValueWebSocketReceiveResult result;
            try
            {
                result = await ClientSocket.ReceiveAsync(writer.GetMemory(), cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ServerLogger.LogDebug("Failed to receive response: {Message}", e.Message);
                return false;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return false;
            }

            writer.Advance(result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        receiver(writer.WrittenSpan, AgentLogger);
        return true;
    }
}
