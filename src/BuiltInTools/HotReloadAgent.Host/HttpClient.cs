// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// HTTP-based client for hot reload communication.
/// Used for mobile platforms (Android, iOS, MacCatalyst) where named pipes don't work over the network.
/// </summary>
internal sealed class HotReloadHttpClient(string baseUrl, IHotReloadAgent agent, Action<string> log, int connectionTimeoutMS = 5000)
{
    /// <summary>
    /// Messages to the server sent after the initial <see cref="ClientInitializationResponse"/> is sent
    /// need to be sent while holding this lock in order to synchronize responses.
    /// </summary>
    private readonly SemaphoreSlim _messageToServerLock = new(initialCount: 1);

    // Use infinite timeout for the HttpClient since polling can take arbitrarily long.
    // The connectionTimeoutMS is used for the initial connect via CancellationToken.
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = Timeout.InfiniteTimeSpan
    };

    private readonly int _connectionTimeoutMS = connectionTimeoutMS;

    public Task Listen(CancellationToken cancellationToken)
    {
        // Connect synchronously to ensure initial updates are applied before the app starts.
        // See PipeListener for detailed comments on why this is important.

        log($"Connecting to hot-reload server via HTTP {baseUrl}");

        try
        {
            // Block execution of the app until initial updates are applied:
            InitializeAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                log($"Connection failure: {e}");
            }

            _httpClient.Dispose();
            agent.Dispose();

            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                await PollForUpdatesAsync(initialUpdates: false, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log(e.Message);
            }
            finally
            {
                _httpClient.Dispose();
                agent.Dispose();
            }
        }, cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        log("Sending capabilities: " + agent.Capabilities);

        // Use a timeout for the initial connect request
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(_connectionTimeoutMS);

        // Send capabilities to the server via POST to /connect
        var initPayload = new ClientInitializationResponse(agent.Capabilities);
        using var connectStream = new MemoryStream();
        await initPayload.WriteAsync(connectStream, connectCts.Token);
        connectStream.Position = 0;

        using var connectContent = new StreamContent(connectStream);
        log($"POST connect ({connectStream.Length} bytes)");
        using var connectResponse = await _httpClient.PostAsync("connect", connectContent, connectCts.Token);
        connectResponse.EnsureSuccessStatusCode();

        log($"Connected (status {(int)connectResponse.StatusCode}).");

        // Apply updates made before this process was launched to avoid executing unupdated versions of the affected modules.
        await PollForUpdatesAsync(initialUpdates: true, cancellationToken);
    }

    private async Task PollForUpdatesAsync(bool initialUpdates, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Poll for updates by sending a request to /poll
                // The server will hold the connection open until there's an update to send
                log(initialUpdates ? "GET poll (initial updates)" : "GET poll");
                using var pollResponse = await _httpClient.GetAsync("poll", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                pollResponse.EnsureSuccessStatusCode();

                using var responseStream = await pollResponse.Content.ReadAsStreamAsync(cancellationToken);

                var payloadType = (RequestType)await responseStream.ReadByteAsync(cancellationToken);
                log($"Received {payloadType}");
                switch (payloadType)
                {
                    case RequestType.ManagedCodeUpdate:
                        await ReadAndApplyManagedCodeUpdateAsync(responseStream, cancellationToken);
                        break;

                    case RequestType.StaticAssetUpdate:
                        await ReadAndApplyStaticAssetUpdateAsync(responseStream, cancellationToken);
                        break;

                    case RequestType.InitialUpdatesCompleted when initialUpdates:
                        return;

                    default:
                        // can't continue, received unknown payload
                        throw new InvalidOperationException($"Unexpected payload type: {payloadType}");
                }
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                // Server may have disconnected, wait a bit and retry
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async ValueTask ReadAndApplyManagedCodeUpdateAsync(Stream requestStream, CancellationToken cancellationToken)
    {
        var request = await ManagedCodeUpdateRequest.ReadAsync(requestStream, cancellationToken);
        log($"Applying {request.Updates.Count} managed code update(s)");

        bool success;
        try
        {
            agent.ApplyManagedCodeUpdates(request.Updates);
            success = true;
        }
        catch (Exception e)
        {
            agent.Reporter.Report($"The runtime failed to applying the change: {e.Message}", AgentMessageSeverity.Error);
            agent.Reporter.Report("Further changes won't be applied to this process.", AgentMessageSeverity.Warning);
            success = false;
        }

        var logEntries = agent.Reporter.GetAndClearLogEntries(request.ResponseLoggingLevel);

        await SendResponseAsync(new UpdateResponse(logEntries, success), cancellationToken);
    }

    private async ValueTask ReadAndApplyStaticAssetUpdateAsync(Stream requestStream, CancellationToken cancellationToken)
    {
        var request = await StaticAssetUpdateRequest.ReadAsync(requestStream, cancellationToken);
        log($"Applying static asset update: {request.Update.RelativePath}");

        try
        {
            agent.ApplyStaticAssetUpdate(request.Update);
        }
        catch (Exception e)
        {
            agent.Reporter.Report($"Failed to apply static asset update: {e.Message}", AgentMessageSeverity.Error);
        }

        var logEntries = agent.Reporter.GetAndClearLogEntries(request.ResponseLoggingLevel);

        // Updating static asset only invokes ContentUpdate metadata update handlers.
        // Failures of these handlers are reported to the log and ignored.
        // Therefore, this request always succeeds.
        await SendResponseAsync(new UpdateResponse(logEntries, success: true), cancellationToken);
    }

    internal async ValueTask SendResponseAsync<T>(T response, CancellationToken cancellationToken)
        where T : IResponse
    {
        try
        {
            await _messageToServerLock.WaitAsync(cancellationToken);

            using var responseStream = new MemoryStream();
            await responseStream.WriteAsync((byte)response.Type, cancellationToken);
            await response.WriteAsync(responseStream, cancellationToken);
            responseStream.Position = 0;

            log($"POST response ({response.Type}, {responseStream.Length} bytes)");
            using var content = new StreamContent(responseStream);
            using var httpResponse = await _httpClient.PostAsync("response", content, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();
        }
        finally
        {
            _messageToServerLock.Release();
        }
    }
}
