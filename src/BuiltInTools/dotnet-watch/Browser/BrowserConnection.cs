// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Buffers;
using System.Net.WebSockets;

namespace Microsoft.DotNet.Watch;

internal readonly struct BrowserConnection : IAsyncDisposable
{
    private static int s_lastId;

    public WebSocket ClientSocket { get; }
    public string? SharedSecret { get; }
    public int Id { get; }
    public IReporter Reporter { get; }

    public BrowserConnection(WebSocket clientSocket, string? sharedSecret, IReporter reporter)
    {
        ClientSocket = clientSocket;
        SharedSecret = sharedSecret;
        Id = Interlocked.Increment(ref s_lastId);
        Reporter = new BrowserSpecificReporter(Id, reporter);

        Reporter.Verbose($"Connected to referesh server.");
    }

    public async ValueTask DisposeAsync()
    {
        await ClientSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, default);
        ClientSocket.Dispose();

        Reporter.Verbose($"Disconnected.");
    }

    internal async ValueTask<bool> TrySendMessageAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
    {
        try
        {
            await ClientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Reporter.Verbose($"Failed to send message: {e.Message}");
            return false;
        }

        return true;
    }

    internal async ValueTask<bool> TryReceiveMessageAsync(Action<ReadOnlySpan<byte>, IReporter> receiver, CancellationToken cancellationToken)
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
                Reporter.Verbose($"Failed to receive response: {e.Message}");
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

        receiver(writer.WrittenSpan, Reporter);
        return true;
    }
}
