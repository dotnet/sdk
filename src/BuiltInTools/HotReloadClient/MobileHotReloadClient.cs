// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Hot reload client that uses WebSocket instead of named pipes.
/// Used for mobile platforms (Android, iOS) where named pipes don't work over the network.
/// 
/// Server-side implementation using Kestrel + WebSockets, extends KestrelWebSocketServer.
/// </summary>
internal sealed class MobileHotReloadClient : HotReloadClient
{
    private readonly int _port;
    private readonly HotReloadWebSocketServer _server;

    private Task<ImmutableArray<string>>? _capabilitiesTask;
    private bool _managedCodeUpdateFailedOrCancelled;

    public MobileHotReloadClient(ILogger logger, ILogger agentLogger, int port)
        : base(logger, agentLogger)
    {
        _port = port;
        _server = new HotReloadWebSocketServer(logger);
    }

    // for testing
    internal int Port => _port;

    public override void Dispose()
    {
        _server.Dispose();
    }

    public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
    {
        environmentBuilder[AgentEnvironmentVariables.DotNetModifiableAssemblies] = "debug";

        // For WebSocket transport (mobile platforms), the hot reload agent is built into the app itself
        // via the platform workload, so we don't need to inject the startup hook.
        // Set the WebSocket endpoint for the app to connect to.
        var serverUrl = _server.ServerUrl;
        if (serverUrl != null)
        {
            environmentBuilder[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint] = serverUrl;
        }
    }

    public override void InitiateConnection(CancellationToken cancellationToken)
    {
        // Start Kestrel server with WebSocket support
        _server.StartServerAsync("localhost", _port, cancellationToken).AsTask().GetAwaiter().GetResult();

        // Wait for connection asynchronously
        _capabilitiesTask = WaitForCapabilitiesAsync(cancellationToken);
    }

    private async Task<ImmutableArray<string>> WaitForCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Wait for the client to connect
            var socket = await _server.WaitForConnectionAsync(cancellationToken);
            if (socket == null)
            {
                return [];
            }

            // Read the initialization message (capabilities)
            using var stream = await _server.ReceiveMessageAsync(cancellationToken);
            if (stream == null)
            {
                return [];
            }

            // Parse capabilities
            var capabilities = await ClientInitializationResponse.ReadAsync(stream, cancellationToken);
            var result = AddImplicitCapabilities(capabilities.Capabilities.Split(' '));

            Logger.Log(LogEvents.Capabilities, string.Join(" ", result));

            return result;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogError("Failed to read capabilities: {Message}", e.Message);
            return [];
        }
    }

    [MemberNotNull(nameof(_capabilitiesTask))]
    private Task<ImmutableArray<string>> GetCapabilitiesTask()
        => _capabilitiesTask ?? throw new InvalidOperationException();

    [MemberNotNull(nameof(_capabilitiesTask))]
    private void RequireReadyForUpdates()
    {
        // should only be called after connection has been created:
        _ = GetCapabilitiesTask();
    }

    public override Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
        => GetCapabilitiesTask();

    public override Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        => GetCapabilitiesTask();

    private ResponseLoggingLevel ResponseLoggingLevel
        => Logger.IsEnabled(LogLevel.Debug) ? ResponseLoggingLevel.Verbose : ResponseLoggingLevel.WarningsAndErrors;

    public override async Task<ApplyStatus> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
    {
        RequireReadyForUpdates();

        if (_managedCodeUpdateFailedOrCancelled)
        {
            Logger.LogDebug("Previous changes failed to apply. Further changes are not applied to this process.");
            return ApplyStatus.Failed;
        }

        var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
        if (applicableUpdates.Count == 0)
        {
            Logger.LogDebug("No updates applicable to this process");
            return ApplyStatus.NoChangesApplied;
        }

        var request = new ManagedCodeUpdateRequest(ToRuntimeUpdates(applicableUpdates), ResponseLoggingLevel);

        var success = false;
        try
        {
            success = await SendAndReceiveUpdateAsync(request, isProcessSuspended, cancellationToken);
        }
        finally
        {
            if (!success)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogWarning("Further changes won't be applied to this process.");
                }

                _managedCodeUpdateFailedOrCancelled = true;
            }
        }

        if (success)
        {
            Logger.Log(LogEvents.UpdatesApplied, applicableUpdates.Count, updates.Length);
        }

        return
            !success ? ApplyStatus.Failed :
            (applicableUpdates.Count < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;

        static ImmutableArray<RuntimeManagedCodeUpdate> ToRuntimeUpdates(IEnumerable<HotReloadManagedCodeUpdate> updates)
            => [.. updates.Select(static update => new RuntimeManagedCodeUpdate(update.ModuleId,
               ImmutableCollectionsMarshal.AsArray(update.MetadataDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.ILDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.PdbDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.UpdatedTypes)!))];
    }

    public async override Task<ApplyStatus> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken)
    {
        RequireReadyForUpdates();

        var appliedUpdateCount = 0;

        foreach (var update in updates)
        {
            var request = new StaticAssetUpdateRequest(
                new RuntimeStaticAssetUpdate(
                    update.AssemblyName,
                    update.RelativePath,
                    ImmutableCollectionsMarshal.AsArray(update.Content)!,
                    update.IsApplicationProject),
                ResponseLoggingLevel);

            Logger.LogDebug("Sending static file update request for asset '{Url}'.", update.RelativePath);

            var success = await SendAndReceiveUpdateAsync(request, isProcessSuspended, cancellationToken);
            if (success)
            {
                appliedUpdateCount++;
            }
        }

        Logger.Log(LogEvents.UpdatesApplied, appliedUpdateCount, updates.Length);

        return
            (appliedUpdateCount == 0) ? ApplyStatus.Failed :
            (appliedUpdateCount < updates.Length) ? ApplyStatus.SomeChangesApplied : ApplyStatus.AllChangesApplied;
    }

    private ValueTask<bool> SendAndReceiveUpdateAsync<TRequest>(TRequest request, bool isProcessSuspended, CancellationToken cancellationToken)
        where TRequest : IUpdateRequest
    {
        return SendAndReceiveUpdateAsync(
            send: SendAndReceiveAsync,
            isProcessSuspended,
            suspendedResult: true,
            cancellationToken);

        async ValueTask<bool> SendAndReceiveAsync(int batchId, CancellationToken cancellationToken)
        {
            Logger.LogDebug("Sending update batch #{UpdateId}", batchId);

            try
            {
                await WriteRequestAsync(request, cancellationToken);

                if (await ReceiveUpdateResponseAsync(cancellationToken))
                {
                    Logger.LogDebug("Update batch #{UpdateId} completed.", batchId);
                    return true;
                }

                Logger.LogDebug("Update batch #{UpdateId} failed.", batchId);
            }
            catch (Exception e)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogDebug("Update batch #{UpdateId} canceled.", batchId);
                }
                else
                {
                    Logger.LogError("Update batch #{UpdateId} failed with error: {Message}", batchId, e.Message);
                }
            }

            return false;
        }
    }

    private async ValueTask WriteRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IUpdateRequest
    {
        // Serialize the request
        using var buffer = new MemoryStream();
        await buffer.WriteAsync((byte)request.Type, cancellationToken);
        await request.WriteAsync(buffer, cancellationToken);

        await _server.SendMessageAsync(new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), cancellationToken);
    }

    private async ValueTask<bool> ReceiveUpdateResponseAsync(CancellationToken cancellationToken)
    {
        // Read response from WebSocket
        using var stream = await _server.ReceiveMessageAsync(cancellationToken);
        if (stream == null)
        {
            return false;
        }

        // Read response type
        var responseType = (ResponseType)await stream.ReadByteAsync(cancellationToken);

        if (responseType == ResponseType.UpdateResponse)
        {
            var (success, log) = await UpdateResponse.ReadAsync(stream, cancellationToken);

            await foreach (var (message, severity) in log)
            {
                ReportLogEntry(AgentLogger, message, severity);
            }

            return success;
        }
        else if (responseType == ResponseType.HotReloadExceptionNotification)
        {
            var notification = await HotReloadExceptionCreatedNotification.ReadAsync(stream, cancellationToken);
            RuntimeRudeEditDetected(notification.Code, notification.Message);
            return false;
        }

        Logger.LogError("Unexpected response type: {ResponseType}", responseType);
        return false;
    }

    public override async Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
    {
        RequireReadyForUpdates();

        if (_managedCodeUpdateFailedOrCancelled)
        {
            return;
        }

        try
        {
            // Send InitialUpdatesCompleted message
            using var buffer = new MemoryStream();
            await buffer.WriteAsync((byte)RequestType.InitialUpdatesCompleted, cancellationToken);

            await _server.SendMessageAsync(new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), cancellationToken);

            Logger.LogDebug("Sent InitialUpdatesCompleted");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Logger.LogError("Failed to send InitialUpdatesCompleted: {Message}", e.Message);
            }
        }
    }

    /// <summary>
    /// WebSocket server for hot reload communication.
    /// Extends KestrelWebSocketServer to handle a single client connection.
    /// </summary>
    private sealed class HotReloadWebSocketServer : KestrelWebSocketServer
    {
        private WebSocket? _clientSocket;
        private readonly TaskCompletionSource<WebSocket?> _clientConnectedSource = new();

        public HotReloadWebSocketServer(ILogger logger) : base(logger)
        {
        }

        public string? ServerUrl => ServerUrls.FirstOrDefault();

        public async ValueTask StartServerAsync(string hostName, int port, CancellationToken cancellationToken)
        {
            await base.StartServerAsync(hostName, port, useTls: false, cancellationToken);
        }

        protected override async Task HandleRequestAsync(HttpContext context)
        {
            var webSocket = await AcceptWebSocketAsync(context);
            if (webSocket == null)
            {
                return;
            }

            Logger.LogDebug("WebSocket client connected");

            _clientSocket = webSocket;
            _clientConnectedSource.TrySetResult(webSocket);

            // Keep the connection alive until it's closed
            // The actual message handling is done via ReceiveMessageAsync
            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(100);
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
            base.Dispose();
        }
    }
}

#endif
