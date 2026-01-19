// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload
{
    /// <summary>
    /// Hot reload client that uses HTTP instead of named pipes.
    /// Used for mobile platforms (Android, iOS, MacCatalyst) where named pipes don't work over the network.
    /// </summary>
    internal sealed class MobileHotReloadClient : HotReloadClient
    {
        private readonly int _port;
        private readonly string _serverUrl;

        private Task<ImmutableArray<string>>? _capabilitiesTask;
        private HttpListener? _httpListener;
        private bool _managedCodeUpdateFailedOrCancelled;

        // Current request stream for communication
        private Stream? _currentRequestStream;
        private Stream? _currentResponseStream;
        private HttpListenerContext? _currentContext;
        private readonly SemaphoreSlim _requestLock = new(1, 1);

        // The status of the last update response.
        private TaskCompletionSource<bool> _updateStatusSource = new();

        // Signals when the first poll request has been received
        private TaskCompletionSource _pollReceivedSource = new();

        public MobileHotReloadClient(ILogger logger, ILogger agentLogger, int port)
            : base(logger, agentLogger)
        {
            _port = port;

            // TODO: HTTPS, secret
            // Use localhost - this must match what the Android workload writes to the environment file
            _serverUrl = $"http://localhost:{port}/hotreload/";
        }

        // for testing
        internal int Port => _port;

        public override void Dispose()
        {
            DisposeListener();
            _requestLock.Dispose();
        }

        private void DisposeListener()
        {
            if (_httpListener != null)
            {
                Logger.LogDebug("Disposing HTTP hot reload listener");

                try
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _currentRequestStream = null;
            _currentResponseStream = null;
            _currentContext = null;
        }

        public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
        {
            environmentBuilder[AgentEnvironmentVariables.DotNetModifiableAssemblies] = "debug";

            // For HTTP transport (mobile platforms), the hot reload agent is built into the app itself
            // via the platform workload, so we don't need to inject the startup hook.
            // Only set the HTTP endpoint for the app to connect to.
            environmentBuilder[AgentEnvironmentVariables.DotNetWatchHotReloadHttpEndpoint] = _serverUrl;
        }

        public override void InitiateConnection(CancellationToken cancellationToken)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(_serverUrl);

            try
            {
                _httpListener.Start();
                Logger.LogDebug("HTTP listener started successfully on {Url}, IsListening={IsListening}", _serverUrl, _httpListener.IsListening);
                Console.WriteLine($"[HTTP DEBUG] HttpListener started on {_serverUrl}, IsListening={_httpListener.IsListening}");
            }
            catch (HttpListenerException ex)
            {
                Logger.LogError("Failed to start HTTP listener on {Url}: {Message}", _serverUrl, ex.Message);
                Console.WriteLine($"[HTTP DEBUG] HttpListener FAILED: {ex.Message}");
                throw;
            }

            // It is important to establish the connection before we return,
            // so that the client can connect after starting.
            _capabilitiesTask = ConnectAsync();

            async Task<ImmutableArray<string>> ConnectAsync()
            {
                try
                {
                    Logger.LogDebug("Waiting for application to connect to HTTP endpoint {Url}.", _serverUrl);
                    Logger.LogDebug("HttpListener prefixes: {Prefixes}", string.Join(", ", _httpListener!.Prefixes));
                    Console.WriteLine($"[HTTP DEBUG] Waiting for connection on {_serverUrl}...");

                    // Wait for the initial connection from the client
                    Logger.LogDebug("Calling GetContextAsync()...");
                    var context = await _httpListener!.GetContextAsync().WaitAsync(cancellationToken);
                    Console.WriteLine($"[HTTP DEBUG] GOT CONTEXT! {context.Request.HttpMethod} {context.Request.Url}");
                    Logger.LogDebug("Received HTTP request: {Method} {Url}", context.Request.HttpMethod, context.Request.Url);

                    if (context.Request.Url?.AbsolutePath != "/hotreload/connect")
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        throw new InvalidOperationException($"Expected /hotreload/connect, got {context.Request.Url?.AbsolutePath}");
                    }

                    // Read capabilities from the request body
                    var capabilities = await ClientInitializationResponse.ReadAsync(context.Request.InputStream, cancellationToken);

                    var result = AddImplicitCapabilities(capabilities.Capabilities.Split(' '));

                    // Send OK response
                    context.Response.StatusCode = 200;
                    context.Response.Close();

                    Logger.Log(LogEvents.Capabilities, string.Join(" ", result));

                    // fire and forget:
                    _ = ListenForResponsesAsync(cancellationToken);

                    return result;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHttpException(e, "capabilities", cancellationToken);
                    return [];
                }
            }
        }

        private void ReportHttpException(Exception e, string responseType, CancellationToken cancellationToken)
        {
            // Don't report a warning when cancelled or the listener has been disposed. The process has terminated or the host is shutting down in that case.
            if (e is ObjectDisposedException or HttpListenerException || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Logger.LogError("Failed to read {ResponseType} from HTTP: {Message}", responseType, e.Message);
        }

        private async Task ListenForResponsesAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(_httpListener != null);

            try
            {
                while (!cancellationToken.IsCancellationRequested && _httpListener.IsListening)
                {
                    var context = await _httpListener.GetContextAsync().WaitAsync(cancellationToken);

                    try
                    {
                        var path = context.Request.Url?.AbsolutePath ?? "";

                        if (path == "/hotreload/response")
                        {
                            // Read the response type
                            var type = (ResponseType)await context.Request.InputStream.ReadByteAsync(cancellationToken);

                            switch (type)
                            {
                                case ResponseType.UpdateResponse:
                                    _updateStatusSource.SetResult(await ReadUpdateResponseAsync(context.Request.InputStream, cancellationToken));
                                    break;

                                case ResponseType.HotReloadExceptionNotification:
                                    var notification = await HotReloadExceptionCreatedNotification.ReadAsync(context.Request.InputStream, cancellationToken);
                                    RuntimeRudeEditDetected(notification.Code, notification.Message);
                                    break;

                                default:
                                    Logger.LogError("Unexpected response received from the agent: {ResponseType}", type);
                                    context.Response.StatusCode = 400;
                                    context.Response.Close();
                                    continue;
                            }

                            context.Response.StatusCode = 200;
                            context.Response.Close();
                        }
                        else if (path == "/hotreload/poll")
                        {
                            // Client is polling for updates - store the context for sending requests
                            await _requestLock.WaitAsync(cancellationToken);
                            try
                            {
                                _currentContext?.Response.Close();
                                _currentContext = context;
                                _currentRequestStream = context.Request.InputStream;
                                _currentResponseStream = context.Response.OutputStream;

                                // Signal that the poll request has been received
                                _pollReceivedSource.TrySetResult();
                            }
                            finally
                            {
                                _requestLock.Release();
                            }
                            // Don't close the context yet - we'll use it to send updates
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            context.Response.Close();
                        }
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Logger.LogDebug("Error handling HTTP request: {Message}", e.Message);
                        try
                        {
                            context.Response.StatusCode = 500;
                            context.Response.Close();
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHttpException(e, "response", cancellationToken);
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
                    // Don't report a warning when cancelled. The process has terminated or the host is shutting down in that case.
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogWarning("Further changes won't be applied to this process.");
                    }

                    _managedCodeUpdateFailedOrCancelled = true;
                    DisposeListener();
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
                    // Don't report an error when cancelled. The process has terminated or the host is shutting down in that case.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogDebug("Update batch #{UpdateId} canceled.", batchId);
                    }
                    else
                    {
                        Logger.LogError("Update batch #{UpdateId} failed with error: {Message}", batchId, e.Message);
                        Logger.LogDebug("Update batch #{UpdateId} exception stack trace: {StackTrace}", batchId, e.StackTrace);
                    }
                }

                return false;
            }
        }

        private async ValueTask WriteRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
            where TRequest : IUpdateRequest
        {
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                if (_currentResponseStream == null || _currentContext == null)
                {
                    throw new InvalidOperationException("No active HTTP connection from the client. The client may have disconnected.");
                }

                await _currentResponseStream.WriteAsync((byte)request.Type, cancellationToken);
                await request.WriteAsync(_currentResponseStream, cancellationToken);
                await _currentResponseStream.FlushAsync(cancellationToken);

                // Close the response to signal completion
                _currentContext.Response.Close();
                _currentContext = null;
                _currentResponseStream = null;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private async ValueTask<bool> ReceiveUpdateResponseAsync(CancellationToken cancellationToken)
        {
            var result = await _updateStatusSource.Task;
            _updateStatusSource = new TaskCompletionSource<bool>();
            return result;
        }

        private async ValueTask<bool> ReadUpdateResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            var (success, log) = await UpdateResponse.ReadAsync(stream, cancellationToken);

            await foreach (var (message, severity) in log)
            {
                ReportLogEntry(AgentLogger, message, severity);
            }

            return success;
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
                // Wait for the poll request to arrive before sending InitialUpdatesCompleted
                Logger.LogDebug("Waiting for poll request before sending InitialUpdatesCompleted...");
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
                await _pollReceivedSource.Task.WaitAsync(timeoutCts.Token);

                await _requestLock.WaitAsync(cancellationToken);
                try
                {
                    if (_currentResponseStream == null || _currentContext == null)
                    {
                        Logger.LogDebug("No active HTTP connection for InitialUpdatesCompleted.");
                        return;
                    }

                    await _currentResponseStream.WriteAsync((byte)RequestType.InitialUpdatesCompleted, cancellationToken);
                    await _currentResponseStream.FlushAsync(cancellationToken);

                    _currentContext.Response.Close();
                    _currentContext = null;
                    _currentResponseStream = null;
                }
                finally
                {
                    _requestLock.Release();
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Don't report an error when cancelled. The process has terminated or the host is shutting down in that case.
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogError("Failed to send {RequestType}: {Message}", nameof(RequestType.InitialUpdatesCompleted), e.Message);
                }
            }
        }
    }
}

#endif
