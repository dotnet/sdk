// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#if NET
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Hosts the browser tools provider and communicates with browser tools clients.
/// Associated with a project instance.
/// </summary>
internal abstract class AbstractBrowserRefreshServer(
    Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment,
    SharedSecretProvider sessionKey,
    ILogger logger,
    Func<int, ILogger> connectionServerLoggerFactory,
    Func<int, ILogger> connectionAgentLoggerFactory) : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static int s_lastConnectionId;

    /// <summary>
    /// The RSA key pair whose public half is pinned into the application build output for the
    /// lifetime of the <c>dotnet watch</c> invocation. Owned by the caller and deliberately not
    /// disposed here: several servers share a single invocation scoped key.
    /// </summary>
    protected SharedSecretProvider SessionKey => sessionKey;

    /// <summary>
    /// Guards the connection list, the retained updates and the baseline epoch together.
    /// Publishing a connection with its replay snapshot and appending an update batch with the list
    /// of connections that receive it live must be mutually atomic, otherwise a browser connecting
    /// concurrently would either apply a batch twice or miss it entirely.
    /// </summary>
    private readonly object _stateGuard = new();

    private readonly List<BrowserConnection> _activeConnections = [];
    private ImmutableArray<BrowserToolsUpdateBatch> _retainedUpdates = [];
    private int _epoch;

    private readonly TaskCompletionSource<None> _browserConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // initialized by StartAsync
    private WebServerHost? _lazyHost;

    public virtual void Dispose()
    {
        BrowserConnection[] connectionsToDispose;
        lock (_stateGuard)
        {
            connectionsToDispose = [.. _activeConnections];
            _activeConnections.Clear();
        }

        foreach (var connection in connectionsToDispose)
        {
            connection.Dispose();
        }

        _lazyHost?.Dispose();

        // The session key is owned by the watcher and shared by all providers of the invocation.
    }

    protected abstract ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken);
    protected abstract bool SuppressTimeouts { get; }

    public ILogger Logger
        => logger;

    internal Uri ProviderAddress
        => new((_lazyHost ?? throw new InvalidOperationException("Server not started")).HttpEndPoints.First(
            static endpoint => endpoint.StartsWith("http:", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Discards the updates retained for the previous application baseline and closes the browser
    /// connections bound to it, so that browsers connecting afterwards only observe the new
    /// baseline. Returns the epoch that identifies the new baseline; update batches produced by a
    /// client of an earlier baseline are dropped.
    /// </summary>
    internal int ResetUpdates()    {
        BrowserConnection[] connectionsToDispose;
        int epoch;

        lock (_stateGuard)
        {
            _retainedUpdates = [];
            epoch = ++_epoch;
            connectionsToDispose = [.. _activeConnections];
            _activeConnections.Clear();
        }

        foreach (var connection in connectionsToDispose)
        {
            connection.Dispose();
        }

        return epoch;
    }

    /// <summary>
    /// The updates a browser connecting right now would replay before observing any live message.
    /// </summary>
    internal ImmutableArray<BrowserToolsUpdateBatch> GetRetainedUpdates()
    {
        lock (_stateGuard)
        {
            return _retainedUpdates;
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_lazyHost != null)
        {
            throw new InvalidOperationException("Server already started");
        }

        _lazyHost = await CreateAndStartHostAsync(cancellationToken);
        logger.Log(LogEvents.RefreshServerRunningAt, string.Join(",", _lazyHost.EndPoints));
    }

    /// <summary>
    /// Configures the application process to expose the browser tools provider on its own origin.
    /// How that is done depends on the host, so the app model supplies the implementation.
    /// </summary>
    public void ConfigureLaunchEnvironment(IDictionary<string, string> builder)
        => configureLaunchEnvironment(builder, this);

    /// <summary>
    /// Takes ownership of the <paramref name="clientSocket"/>.
    /// Publishes the connection and captures the updates it has to replay atomically.
    /// </summary>
    protected BrowserConnection OnBrowserConnected(WebSocket clientSocket, string? sharedSecret)
    {
        bool connectionPublished = false;
        try
        {
            var connectionId = Interlocked.Increment(ref s_lastConnectionId);
            var serverLogger = connectionServerLoggerFactory(connectionId);
            var agentLogger = connectionAgentLoggerFactory(connectionId);

            BrowserConnection connection;
            lock (_stateGuard)
            {
                connection = new BrowserConnection(clientSocket, sharedSecret, connectionId, serverLogger, agentLogger, _retainedUpdates);
                _activeConnections.Add(connection);
            }

            connectionPublished = true;

            serverLogger.Log(LogEvents.ConnectedToRefreshServer);
            _browserConnected.TrySetResult(default);
            return connection;
        }
        finally
        {
            if (!connectionPublished)
            {
                clientSocket.Dispose();
            }
        }
    }

    /// <summary>
    /// Sends the session initialization message, which carries the updates the browser has to apply
    /// before it observes any live message, and waits for the browser to acknowledge it. Live
    /// messages queued for the connection in the meantime are released once this completes.
    /// </summary>
    protected async ValueTask InitializeBrowserConnectionAsync(BrowserConnection connection, CancellationToken cancellationToken)
    {
        var message = SerializeJson(new JsonInitializeSessionRequest
        {
            SharedSecret = connection.SharedSecret,
            Updates = connection.PendingReplayUpdates,
        });

        bool? initialized = null;
        if (await connection.TrySendMessageAsync(message, cancellationToken))
        {
            initialized = await connection.TryReceiveMessageAsync(
                new ResponseFunc<bool>(static (value, logger) => ReceiveUpdateApplyResponse(value, logger)),
                cancellationToken);
        }

        if (initialized != true)
        {
            connection.ServerLogger.LogDebug("Failed to initialize the browser tools session.");
            connection.Dispose();
            return;
        }

        connection.Initialized.TrySetResult(true);
    }

    internal static bool ReceiveUpdateApplyResponse(ReadOnlySpan<byte> value, ILogger logger)
    {
        var data = DeserializeJson<JsonApplyDeltasResponse>(value);

        foreach (var entry in data.Log)
        {
            HotReloadClient.ReportLogEntry(logger, entry.Message, (AgentMessageSeverity)entry.Severity);
        }

        return data.Success;
    }

#if NET
    internal async Task AcceptBrowserConnectionAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var subProtocol = context.WebSockets.WebSocketRequestedProtocols is [var requestedSubProtocol]
            ? requestedSubProtocol
            : null;
        if (subProtocol == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // The browser generated secret, encrypted with the build-pinned public key, is the only
        // credential. Reject before upgrading the connection so an unauthenticated peer never gets
        // a socket.
        string sharedSecret;
        try
        {
            sharedSecret = SessionKey.DecryptSecret(WebUtility.UrlDecode(subProtocol));
        }
        catch (Exception e)
        {
            logger.LogDebug("Rejecting a browser connection with an invalid encrypted secret: {Message}", e.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var clientSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol);

        var connection = OnBrowserConnected(clientSocket, sharedSecret);
        await InitializeBrowserConnectionAsync(connection, context.RequestAborted);
        await connection.Disconnected.Task;
    }
#endif

    /// <summary>
    /// For testing.
    /// </summary>
    internal void EmulateClientConnected()
    {
        _browserConnected.TrySetResult(default);
    }

    public async Task WaitForClientConnectionAsync(CancellationToken cancellationToken)
    {
        using var progressCancellationSource = new CancellationTokenSource();

        // It make take a while to connect since the app might need to build first.
        // Indicate progress in the output. Start with 60s and then report progress every 10s.
        var firstReportSeconds = TimeSpan.FromSeconds(60);
        var nextReportSeconds = TimeSpan.FromSeconds(10);

        var reportDelayInSeconds = firstReportSeconds;
        var connectionAttemptReported = false;

        var progressReportingTask = Task.Run(async () =>
        {
            try
            {
                while (!progressCancellationSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(SuppressTimeouts ? TimeSpan.MaxValue : reportDelayInSeconds, progressCancellationSource.Token);

                    connectionAttemptReported = true;
                    reportDelayInSeconds = nextReportSeconds;
                    logger.LogInformation("Connecting to the browser ...");
                }
            }
            catch (OperationCanceledException)
            {
                // nop
            }
        }, progressCancellationSource.Token);

        // Work around lack of Task.WaitAsync(cancellationToken) on .NET Framework:
        cancellationToken.Register(() => _browserConnected.TrySetCanceled());

        try
        {
            await _browserConnected.Task;
        }
        finally
        {
            progressCancellationSource.Cancel();
        }

        if (connectionAttemptReported)
        {
            logger.LogInformation("Browser connection established.");
        }
    }

    private IReadOnlyCollection<BrowserConnection> GetOpenBrowserConnections()
    {
        lock (_stateGuard)
        {
            return [.. _activeConnections.Where(b => b.ClientSocket.State == WebSocketState.Open)];
        }
    }

    /// <summary>
    /// Retains <paramref name="batch"/> for browsers that connect later and captures the connections
    /// that have to receive it live. Both happen under a single lock: a browser that registers before
    /// the append receives the batch live and not in its replay snapshot, and a browser that registers
    /// after it receives it in the snapshot and is not in the captured list. Hence every browser
    /// applies every batch exactly once, in order, without any wire level update identity.
    ///
    /// Returns false if <paramref name="epoch"/> is stale, which means the batch was produced by a
    /// client of a previous application baseline and must be dropped.
    /// </summary>
    private bool TryAppendUpdate(int epoch, BrowserToolsUpdateBatch batch, out IReadOnlyCollection<BrowserConnection> liveConnections)
    {
        lock (_stateGuard)
        {
            if (epoch != _epoch)
            {
                liveConnections = [];
                return false;
            }

            _retainedUpdates = _retainedUpdates.Add(batch);
            liveConnections = [.. _activeConnections.Where(b => b.ClientSocket.State == WebSocketState.Open)];
            return true;
        }
    }

    /// <summary>
    /// Retains a managed code update batch and delivers it to the browsers connected at that moment.
    /// </summary>
    /// <returns>
    /// True unless a browser reported that it failed to apply the update, or if the batch belongs to
    /// a superseded baseline and was dropped. When several browsers are connected the result is the
    /// last reported one, which matches the pre-existing behavior of this path.
    /// </returns>
    internal async ValueTask<bool> SendManagedCodeUpdateAsync<TRequest>(
        int epoch,
        BrowserToolsUpdateBatch batch,
        Func<string?, TRequest> request,
        CancellationToken cancellationToken)
    {
        if (!TryAppendUpdate(epoch, batch, out var liveConnections))
        {
            logger.LogDebug("Discarding an update batch produced for a superseded application baseline.");
            return true;
        }

        var result = await SendAndReceiveAsync(
            liveConnections,
            request,
            new ResponseFunc<bool>(static (value, logger) => ReceiveUpdateApplyResponse(value, logger)),
            cancellationToken);

        return result ?? true;
    }

    private void DisposeClosedBrowserConnections()
    {
        List<BrowserConnection>? lazyConnectionsToDispose = null;

        lock (_stateGuard)
        {
            var j = 0;
            for (var i = 0; i < _activeConnections.Count; i++)
            {
                var connection = _activeConnections[i];
                if (connection.ClientSocket.State == WebSocketState.Open)
                {
                    _activeConnections[j++] = connection;
                }
                else
                {
                    lazyConnectionsToDispose ??= [];
                    lazyConnectionsToDispose.Add(connection);
                }
            }

            _activeConnections.RemoveRange(j, _activeConnections.Count - j);
        }

        if (lazyConnectionsToDispose != null)
        {
            foreach (var connection in lazyConnectionsToDispose)
            {
                connection.Dispose();
            }
        }
    }

    public static ReadOnlyMemory<byte> SerializeJson<TValue>(TValue value)
        => JsonSerializer.SerializeToUtf8Bytes(value, s_jsonSerializerOptions);

    public static TValue DeserializeJson<TValue>(ReadOnlySpan<byte> value)
        => JsonSerializer.Deserialize<TValue>(value, s_jsonSerializerOptions) ?? throw new InvalidDataException("Unexpected null object");

    public ValueTask SendJsonMessageAsync<TValue>(TValue value, CancellationToken cancellationToken)
        => SendAsync(SerializeJson(value), cancellationToken);

    public ValueTask SendReloadMessageAsync(CancellationToken cancellationToken)
    {
        logger.Log(LogEvents.ReloadingBrowser);
        return SendAsync(JsonReloadRequest.Message, cancellationToken);
    }

    public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken)
    {
        logger.Log(LogEvents.SendingWaitMessage);
        return SendAsync(JsonWaitRequest.Message, cancellationToken);
    }

    private async ValueTask SendAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
    {
        await SendAndReceiveAsync<ReadOnlyMemory<byte>, None>(request: _ => messageBytes, response: null, cancellationToken);
    }

    internal ValueTask<TResult?> SendAndReceiveAsync<TRequest, TResult>(
        Func<string?, TRequest>? request,
        ResponseFunc<TResult>? response,
        CancellationToken cancellationToken)
        where TResult : struct
        => SendAndReceiveAsync(GetOpenBrowserConnections(), request, response, cancellationToken);

    internal virtual async ValueTask<TResult?> SendAndReceiveAsync<TRequest, TResult>(
        IReadOnlyCollection<BrowserConnection> openConnections,
        Func<string?, TRequest>? request,
        ResponseFunc<TResult>? response,
        CancellationToken cancellationToken)
        where TResult : struct
    {
        var responded = false;
        var result = default(TResult?);

        // Each connection owns its socket, so run them concurrently. Sequential delivery would let a
        // browser that has not acknowledged its session initialization yet hold up every other
        // browser, since the wait below has no timeout by design.
        var exchanges = new List<Task<(bool received, TResult? result, bool responded)>>(openConnections.Count);
        foreach (var connection in openConnections)
        {
            exchanges.Add(ExchangeAsync(connection));
        }

        // Fold in connection order so the observable outcome matches a sequential exchange.
        foreach (var (received, connectionResult, connectionResponded) in await Task.WhenAll(exchanges))
        {
            if (received)
            {
                result = connectionResult;
            }

            responded |= connectionResponded;
        }

        if (openConnections.Count == 0)
        {
            logger.Log(LogEvents.NoBrowserConnected);
        }
        else if (response != null && !responded)
        {
            logger.Log(LogEvents.FailedToReceiveResponseFromConnectedBrowser);
        }

        DisposeClosedBrowserConnections();
        return result;

        async Task<(bool received, TResult? result, bool responded)> ExchangeAsync(BrowserConnection connection)
        {
            // Live messages must not overtake the session initialization message, which carries the
            // updates produced before the connection was established.
            if (!await connection.WaitForInitializationAsync(cancellationToken))
            {
                return (false, null, false);
            }

            if (request != null)
            {
                var requestValue = request(connection.SharedSecret);
                var requestBytes = requestValue is ReadOnlyMemory<byte> bytes ? bytes : SerializeJson(requestValue);

                if (!await connection.TrySendMessageAsync(requestBytes, cancellationToken))
                {
                    return (false, null, false);
                }
            }

            if (response == null)
            {
                return (false, null, true);
            }

            var connectionResult = await connection.TryReceiveMessageAsync(response, cancellationToken);
            return (true, connectionResult, connectionResult != null);
        }
    }

    public ValueTask RefreshBrowserAsync(CancellationToken cancellationToken)
    {
        logger.Log(LogEvents.RefreshingBrowser);
        return SendAsync(JsonRefreshBrowserRequest.Message, cancellationToken);
    }

    public ValueTask ReportCompilationErrorsInBrowserAsync(ImmutableArray<string> compilationErrors, CancellationToken cancellationToken)
    {
        logger.Log(LogEvents.UpdatingDiagnostics);
        return SendJsonMessageAsync(new JsonReportDiagnosticsRequest { Diagnostics = compilationErrors }, cancellationToken);
    }

    public async ValueTask UpdateStaticAssetsAsync(IEnumerable<string> relativeUrls, CancellationToken cancellationToken)
    {
        // Serialize all requests sent to a single server:
        foreach (var relativeUrl in relativeUrls)
        {
            logger.Log(LogEvents.SendingStaticAssetUpdateRequest, relativeUrl);
            var message = JsonSerializer.SerializeToUtf8Bytes(new JasonUpdateStaticFileRequest { Path = relativeUrl }, s_jsonSerializerOptions);
            await SendAsync(message, cancellationToken);
        }
    }

    private readonly struct JsonWaitRequest
    {
        public string Type => "Wait";
        public static readonly ReadOnlyMemory<byte> Message = JsonSerializer.SerializeToUtf8Bytes(new JsonWaitRequest(), s_jsonSerializerOptions);
    }

    private readonly struct JsonReloadRequest
    {
        public string Type => "Reload";
        public static readonly ReadOnlyMemory<byte> Message = JsonSerializer.SerializeToUtf8Bytes(new JsonReloadRequest(), s_jsonSerializerOptions);
    }

    private readonly struct JsonRefreshBrowserRequest
    {
        public string Type => "RefreshBrowser";
        public static readonly ReadOnlyMemory<byte> Message = JsonSerializer.SerializeToUtf8Bytes(new JsonRefreshBrowserRequest(), s_jsonSerializerOptions);
    }

    private readonly struct JsonReportDiagnosticsRequest
    {
        public string Type => "ReportDiagnostics";

        public IEnumerable<string> Diagnostics { get; init; }
    }

    private readonly struct JasonUpdateStaticFileRequest
    {
        public string Type => "UpdateStaticFile";
        public string Path { get; init; }
    }

    /// <summary>
    /// The first message sent on an accepted connection. It echoes the browser generated secret back
    /// so that the browser can authenticate the provider, and carries the updates produced before the
    /// connection was established. The browser applies them and acknowledges with
    /// <see cref="JsonApplyDeltasResponse"/> before it starts observing live messages.
    /// </summary>
    private readonly struct JsonInitializeSessionRequest
    {
        public string Type => "InitializeSession";
        public string? SharedSecret { get; init; }
        public ImmutableArray<BrowserToolsUpdateBatch> Updates { get; init; }
    }

    internal readonly struct JsonApplyDeltasResponse
    {
        public bool Success { get; init; }
        public IEnumerable<JsonLogEntry> Log { get; init; }
    }

    internal readonly struct JsonLogEntry
    {
        public string Message { get; init; }
        public int Severity { get; init; }
    }
}
