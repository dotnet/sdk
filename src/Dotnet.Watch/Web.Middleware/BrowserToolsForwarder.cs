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
