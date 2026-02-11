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
/// Used for projects with the HotReloadWebSockets capability (e.g., Android, iOS).
/// Mobile workloads add this capability since named pipes don't work over the network.
/// 
/// Server-side implementation using Kestrel + WebSockets, extends KestrelWebSocketServer.
/// </summary>
internal sealed class MobileHotReloadClient : HotReloadClient
{
    private readonly int _requestedPort;
    private readonly string? _startupHookPath;
    private readonly HotReloadWebSocketServer _server;

    private Task<ImmutableArray<string>>? _capabilitiesTask;
    private bool _managedCodeUpdateFailedOrCancelled;

    public MobileHotReloadClient(ILogger logger, ILogger agentLogger, int port, string? startupHookPath = null)
        : base(logger, agentLogger)
    {
        _requestedPort = port;
        _startupHookPath = startupHookPath;
        _server = new HotReloadWebSocketServer(logger);
    }

    // for testing
    internal int Port => _server.BoundPort;

    public override void Dispose()
    {
        _server.Dispose();
    }

    public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
    {
        // Start the server now so we know the actual bound port (when using port 0 for auto-assign)
        EnsureServerStarted();

        environmentBuilder[AgentEnvironmentVariables.DotNetModifiableAssemblies] = "debug";

        // Set the WebSocket endpoint for the app to connect to.
        // Use the actual bound URL from the server (important when port 0 was requested).
        environmentBuilder[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint] = _server.WebSocketUrl;

        // Set the RSA public key for the client to encrypt its shared secret.
        // This is the same authentication mechanism used by BrowserRefreshServer.
        environmentBuilder[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketKey] = _server.PublicKey;

        // Pass the startup hook path as an environment variable so the workload can deploy it.
        // This gets passed via `dotnet run -e` and becomes available as @(RuntimeEnvironmentVariable)
        // items in MSBuild targets (build, DeployToDevice, ComputeRunArguments).
        if (_startupHookPath != null)
        {
            environmentBuilder.InsertListItem(AgentEnvironmentVariables.DotNetStartupHooks, _startupHookPath, Path.PathSeparator);
        }
    }

    private void EnsureServerStarted()
    {
        if (_server.IsStarted)
        {
            return;
        }

        // Start Kestrel server with WebSocket support.
        // Use 127.0.0.1 instead of "localhost" because Kestrel doesn't support dynamic port binding with "localhost".
        // System.InvalidOperationException: Dynamic port binding is not supported when binding to localhost. You must either bind to 127.0.0.1:0 or [::1]:0, or both.
        _server.StartServerAsync("127.0.0.1", _requestedPort, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override void InitiateConnection(CancellationToken cancellationToken)
    {
        // Server should already be started by ConfigureLaunchEnvironment, but ensure it's started
        EnsureServerStarted();

        // Wait for connection asynchronously
        _capabilitiesTask = WaitForCapabilitiesAsync(cancellationToken);
    }

    private async Task<ImmutableArray<string>> WaitForCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Wait for the client to connect
            if (await _server.WaitForConnectionAsync(cancellationToken) == null)
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

    public async override Task<Task<bool>> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
    {
        RequireReadyForUpdates();

        if (_managedCodeUpdateFailedOrCancelled)
        {
            Logger.LogDebug("Previous changes failed to apply. Further changes are not applied to this process.");
            return Task.FromResult(false);
        }

        var applicableUpdates = await FilterApplicableUpdatesAsync(updates, cancellationToken);
        if (applicableUpdates.Count == 0)
        {
            Logger.LogDebug("No updates applicable to this process");
            return Task.FromResult(true);
        }

        var request = new ManagedCodeUpdateRequest(ToRuntimeUpdates(applicableUpdates), ResponseLoggingLevel);

        // Only cancel apply operation when the process exits:
        var updateCompletionTask = QueueUpdateBatch(
            sendAndReceive: async batchId =>
            {
                Logger.LogDebug("Sending update batch #{UpdateId}", batchId);
                await WriteRequestAsync(request, applyOperationCancellationToken);
                var success = await ReceiveUpdateResponseAsync(applyOperationCancellationToken);
                Logger.Log(success ? LogEvents.UpdateBatchCompleted : LogEvents.UpdateBatchFailed, batchId);
                return success;
            },
            applyOperationCancellationToken);

        return CompleteApplyOperationAsync();

        async Task<bool> CompleteApplyOperationAsync()
        {
            if (await updateCompletionTask)
            {
                return true;
            }

            Logger.LogWarning("Further changes won't be applied to this process.");
            _managedCodeUpdateFailedOrCancelled = true;

            return false;
        }

        static ImmutableArray<RuntimeManagedCodeUpdate> ToRuntimeUpdates(IEnumerable<HotReloadManagedCodeUpdate> updates)
            => [.. updates.Select(static update => new RuntimeManagedCodeUpdate(update.ModuleId,
               ImmutableCollectionsMarshal.AsArray(update.MetadataDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.ILDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.PdbDelta)!,
               ImmutableCollectionsMarshal.AsArray(update.UpdatedTypes)!))];
    }

    public override async Task<Task<bool>> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
    {
        RequireReadyForUpdates();

        var completionTasks = updates.Select(update =>
        {
            var request = new StaticAssetUpdateRequest(
                new RuntimeStaticAssetUpdate(
                    update.AssemblyName,
                    update.RelativePath,
                    ImmutableCollectionsMarshal.AsArray(update.Content)!,
                    update.IsApplicationProject),
                ResponseLoggingLevel);

            Logger.LogDebug("Sending static file update request for asset '{Url}'.", update.RelativePath);

            // Only cancel apply operation when the process exits:
            return QueueUpdateBatch(
                sendAndReceive: async batchId =>
                {
                    Logger.LogDebug("Sending update batch #{UpdateId}", batchId);
                    await WriteRequestAsync(request, applyOperationCancellationToken);
                    var success = await ReceiveUpdateResponseAsync(applyOperationCancellationToken);
                    Logger.Log(success ? LogEvents.UpdateBatchCompleted : LogEvents.UpdateBatchFailed, batchId);
                    return success;
                },
                applyOperationCancellationToken);
        });

        return CompleteApplyOperationAsync();

        async Task<bool> CompleteApplyOperationAsync()
        {
            var results = await Task.WhenAll(completionTasks);
            return results.All(isSuccess => isSuccess);
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
    /// Uses RSA-based shared secret for authentication (same as BrowserRefreshServer).
    /// </summary>
    private sealed class HotReloadWebSocketServer : KestrelWebSocketServer
    {
        private WebSocket? _clientSocket;
        private readonly TaskCompletionSource<WebSocket?> _clientConnectedSource = new();
        private readonly SharedSecretProvider _sharedSecretProvider = new();

        public HotReloadWebSocketServer(ILogger logger) : base(logger)
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
        public string WebSocketUrl => ServerUrls.FirstOrDefault() ?? throw new InvalidOperationException("Server not started");

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
}

#endif
