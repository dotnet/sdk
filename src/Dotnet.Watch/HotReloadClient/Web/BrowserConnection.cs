// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Represents a connection to a browser that facilitates Hot Reload operations.
/// </summary>
internal readonly struct BrowserConnection(
    WebSocket clientSocket,
    string? sharedSecret,
    int id,
    ILogger serverLogger,
    ILogger agentLogger,
    ImmutableArray<BrowserToolsUpdateBatch> pendingReplayUpdates) : IDisposable
{
    public readonly TaskCompletionSource<None> Disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes with true once the browser acknowledged the session initialization message, which
    /// carries the updates produced before this connection was established. Live messages must not
    /// be sent to the connection before that, otherwise they could be applied out of order or twice.
    /// Completes with false if the connection is torn down before it is initialized.
    /// </summary>
    public readonly TaskCompletionSource<bool> Initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        ClientSocket.Dispose();

        Initialized.TrySetResult(false);
        Disconnected.TrySetResult(default);
        ServerLogger.LogDebug("Disconnected.");
    }

    public WebSocket ClientSocket => clientSocket;
    public string? SharedSecret => sharedSecret;
    public int Id => id;
    public ILogger ServerLogger => serverLogger;
    public ILogger AgentLogger => agentLogger;

    /// <summary>
    /// Updates that were produced before this connection was established, captured atomically with
    /// the publication of the connection.
    /// </summary>
    public ImmutableArray<BrowserToolsUpdateBatch> PendingReplayUpdates => pendingReplayUpdates;

    internal async ValueTask<bool> WaitForInitializationAsync(CancellationToken cancellationToken)
    {
        var initialized = Initialized.Task;
        if (initialized.IsCompleted)
        {
            return initialized.Status == TaskStatus.RanToCompletion && initialized.Result;
        }

        // Work around lack of Task.WaitAsync(CancellationToken) on .NET Framework:
        var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false), cancellation);

        var completed = await Task.WhenAny(initialized, cancellation.Task);
        return completed == initialized && initialized.Result;
    }

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
