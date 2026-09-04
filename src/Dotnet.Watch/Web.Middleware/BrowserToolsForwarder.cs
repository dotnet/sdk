// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal sealed class BrowserToolsForwarder : IDisposable
{
    private static readonly HashSet<string> s_hopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
    };

    private readonly Uri _providerAddress;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrowserToolsForwarder> _logger;

    // ClientWebSocketOptions.CollectHttpResponseDetails and ClientWebSocket.HttpStatusCode were added in
    // .NET 7. This assembly targets the lowest runtime it can be injected into, but it executes on the
    // application's runtime, which is newer in practice. Without them a failed upgrade only reports
    // WebSocketError.NotAWebSocket, which says the provider answered with some HTTP status but not which
    // one, so an older host keeps the previous gateway-error behavior rather than inventing a status.
    private static readonly PropertyInfo? s_collectHttpResponseDetails =
        typeof(ClientWebSocketOptions).GetProperty("CollectHttpResponseDetails", BindingFlags.Public | BindingFlags.Instance);

    private static readonly PropertyInfo? s_httpStatusCode =
        typeof(ClientWebSocket).GetProperty("HttpStatusCode", BindingFlags.Public | BindingFlags.Instance);

    public BrowserToolsForwarder(Uri providerAddress, ILogger<BrowserToolsForwarder> logger)
    {
        _providerAddress = providerAddress;
        _logger = logger;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
        });
    }

    public Task ForwardAsync(HttpContext context)
        => context.WebSockets.IsWebSocketRequest
            ? ForwardWebSocketAsync(context)
            : ForwardHttpAsync(context);

    public void Dispose()
        => _httpClient.Dispose();

    private async Task ForwardHttpAsync(HttpContext context)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            CreateProviderUri(context.Request.Path, context.Request.QueryString));

        if (context.Request.ContentLength is not null || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
        }

        CopyRequestHeaders(context.Request.Headers, request);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            context.Response.StatusCode = (int)response.StatusCode;
            CopyResponseHeaders(response.Headers, context.Response.Headers);
            CopyResponseHeaders(response.Content.Headers, context.Response.Headers);
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Unable to forward a browser tools HTTP request to the provider.");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
            }
        }
    }

    private async Task ForwardWebSocketAsync(HttpContext context)
    {
        using var providerSocket = new ClientWebSocket();
        providerSocket.Options.Proxy = null;
        TryCollectHttpResponseDetails(providerSocket.Options);

        foreach (var protocol in context.WebSockets.WebSocketRequestedProtocols)
        {
            providerSocket.Options.AddSubProtocol(protocol);
        }

        CopyWebSocketRequestHeader(context, providerSocket.Options, "Origin");
        CopyWebSocketRequestHeader(context, providerSocket.Options, "User-Agent");

        try
        {
            await providerSocket.ConnectAsync(
                CreateProviderWebSocketUri(context.Request.Path, context.Request.QueryString),
                context.RequestAborted);
        }
        catch (WebSocketException exception)
        {
            // The provider rejects an unauthenticated browser before upgrading the connection, so its
            // deliberate pre-upgrade status is part of the contract and must survive the hop through the
            // application origin. Only an error status the provider actually produced is forwarded; a
            // genuine upstream or network failure, or a handshake that failed after the provider had
            // already switched protocols, has none and stays a gateway error.
            var providerStatus = TryGetProviderStatusCode(providerSocket);
            if (providerStatus is int status)
            {
                _logger.LogDebug(
                    "The browser tools provider rejected a WebSocket handshake with status {StatusCode}.",
                    status);
                context.Response.StatusCode = status;
                return;
            }

            _logger.LogError(exception, "Unable to connect to the browser tools WebSocket provider.");
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var browserSocket = await context.WebSockets.AcceptWebSocketAsync(providerSocket.SubProtocol);
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        var browserToProvider = PumpWebSocketAsync(browserSocket, providerSocket, cancellationSource.Token);
        var providerToBrowser = PumpWebSocketAsync(providerSocket, browserSocket, cancellationSource.Token);

        try
        {
            await Task.WhenAll(browserToProvider, providerToBrowser);
        }
        catch
        {
            cancellationSource.Cancel();
            providerSocket.Abort();
            browserSocket.Abort();
            throw;
        }
    }

    // Enables the upstream response details on runtimes that support them. Best effort: when the property
    // is missing the socket simply reports no status and the caller falls back to a gateway error.
    private static void TryCollectHttpResponseDetails(ClientWebSocketOptions options)
        => s_collectHttpResponseDetails?.SetValue(options, true);

    // Returns the HTTP status the provider rejected the upgrade with, or null when it produced none or
    // answered something other than a rejection. With CollectHttpResponseDetails the runtime records the
    // status before it validates the upgrade response, so a 101 whose handshake later failed validation is
    // also reported here; relaying that would make the application claim a protocol switch that never
    // happened. Only an error status is a deliberate rejection; anything else is a gateway failure.
    private static int? TryGetProviderStatusCode(ClientWebSocket socket)
    {
        if (s_httpStatusCode?.GetValue(socket) is HttpStatusCode statusCode && (int)statusCode >= 400)
        {
            return (int)statusCode;
        }

        return null;
    }

    private Uri CreateProviderUri(PathString path, QueryString query)
    {
        var builder = new UriBuilder(_providerAddress)
        {
            Path = path.Value,
            Query = query.HasValue ? query.Value![1..] : string.Empty,
        };
        return builder.Uri;
    }

    private Uri CreateProviderWebSocketUri(PathString path, QueryString query)
    {
        var builder = new UriBuilder(CreateProviderUri(path, query))
        {
            Scheme = Uri.UriSchemeWs,
        };
        return builder.Uri;
    }

    private static void CopyRequestHeaders(IHeaderDictionary source, HttpRequestMessage destination)
    {
        foreach (var (name, values) in source)
        {
            if (string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) || s_hopByHopHeaders.Contains(name))
            {
                continue;
            }

            if (!destination.Headers.TryAddWithoutValidation(name, values.ToArray()) && destination.Content != null)
            {
                destination.Content.Headers.TryAddWithoutValidation(name, values.ToArray());
            }
        }
    }

    private static void CopyResponseHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> source,
        IHeaderDictionary destination)
    {
        foreach (var (name, values) in source)
        {
            if (!s_hopByHopHeaders.Contains(name))
            {
                destination[name] = new StringValues([.. values]);
            }
        }
    }

    private static void CopyWebSocketRequestHeader(
        HttpContext context,
        ClientWebSocketOptions options,
        string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var value) && !StringValues.IsNullOrEmpty(value))
        {
            options.SetRequestHeader(headerName, value.ToString());
        }
    }

    private static async Task PumpWebSocketAsync(
        WebSocket source,
        WebSocket destination,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (source.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (destination.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        await destination.CloseOutputAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            result.CloseStatusDescription,
                            cancellationToken);
                    }

                    return;
                }

                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
