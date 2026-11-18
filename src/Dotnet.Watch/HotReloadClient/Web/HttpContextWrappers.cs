// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Wrappers that map HttpListenerContext to HttpContext and related types,
// so that the same request handler can be used for both Kestrel and HttpListener.

#if NETFRAMEWORK

#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Http;

internal readonly struct HttpContext(HttpListenerContext context, CancellationToken requestAborted)
{
    public CancellationToken RequestAborted => requestAborted;

    // wrap immutable state:
    public HttpRequest Request => new(context);
    public WebSocketManager WebSockets => new(context);

    // wraps mutable state:
    public readonly HttpResponse Response = new(context.Response);
}

internal readonly struct HttpRequest(HttpListenerContext context)
{
    public HttpRequestHeaders Headers => new(context);
    public string Method => context.Request.HttpMethod;
    public PathString Path => new(context.Request.Url?.AbsolutePath);
}

internal readonly struct HttpRequestHeaders(HttpListenerContext context)
{
    public string[] Origin => context.Request.Headers.GetValues(nameof(Origin)) ?? [];
}

internal readonly struct PathString(string? value)
{
    public string? Value => value;
}

internal readonly struct HttpResponse(HttpListenerResponse response)
{
    public readonly HttpResponseHeaders Headers = new(response);

    public int StatusCode
    {
        set => response.StatusCode = value;
    }

    public string? ContentType
    {
        set => response.ContentType = value;
    }

    public async Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
    }
}

internal readonly struct HttpResponseHeaders(HttpListenerResponse response)
{
    public string? CacheControl
    {
        set => response.Headers[HttpResponseHeader.CacheControl] = value;
    }

    public void Append(string name, string value)
        => response.Headers.Add(name, value);
}

internal readonly struct WebSocketManager(HttpListenerContext context)
{
    public bool IsWebSocketRequest
        => context.Request.IsWebSocketRequest;

    public string[] WebSocketRequestedProtocols
        => context.Request.Headers["Sec-WebSocket-Protocol"]?
            .Split(',')
            .Select(protocol => protocol.Trim())
            .Where(protocol => protocol.Length > 0)
            .ToArray() ?? [];

    public async Task<WebSocket> AcceptWebSocketAsync(string subProtocol)
        => (await context.AcceptWebSocketAsync(subProtocol)).WebSocket;
}

internal static class HttpMethods
{
    public static readonly string Get = "GET";

    public static bool IsGet(string method)
        => Get.Equals(method, StringComparison.OrdinalIgnoreCase);
}

internal static class StatusCodes
{
    public const int Status204NoContent = 204;
    public const int Status400BadRequest = 400;
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status405MethodNotAllowed = 405;
    public const int Status500InternalServerError = 500;
}

#endif
