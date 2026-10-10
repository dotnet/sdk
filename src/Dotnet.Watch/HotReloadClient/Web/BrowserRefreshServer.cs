// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Kestrel-based Browser Refesh Server implementation.
/// Delegates Kestrel lifecycle to <see cref="KestrelWebSocketServer"/>.
/// </summary>
internal class BrowserRefreshServer(
    ILogger logger,
    Func<int, ILogger> connectionServerLoggerFactory,
    Func<int, ILogger> connectionAgentLoggerFactory,
    Func<SharedSecretProvider?> sessionKeyFactory,
    string middlewareAssemblyPath,
    bool useGatewayProxy,
    bool suppressTimeouts)
    : AbstractBrowserRefreshServer(logger, connectionServerLoggerFactory, connectionAgentLoggerFactory, middlewareAssemblyPath, useGatewayProxy, suppressTimeouts)
{
    protected override async ValueTask<WebServerHost> CreateAndStartHostAsync(WebSocketConfig webSocketConfig, CancellationToken cancellationToken)
    {
        // The browser reaches the provider through the application's own origin, so the provider only
        // listens on loopback. DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME no longer applies to this hop.
        var server = await KestrelWebSocketServer.StartServerAsync(
            webSocketConfig.WithHostName(null),
            context => HandleRequestAsync(webSocketConfig, context),
            cancellationToken);

        // URLs are only available after the server has started.
        return new WebServerHost(server, server.ServerUrls, server.HttpServerUrls);
    }

    /// <summary>
    /// The complete HTTP surface of the browser tools provider.
    ///
    /// The provider serves no executable content: the browser tools client and its configuration are
    /// part of the application build output, which is what makes authenticating the provider with the
    /// build pinned public key meaningful. Its settings response only reports provider availability.
    /// </summary>
    public async Task HandleRequestAsync(WebSocketConfig webSocketConfig, HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest &&
            (!Uri.TryCreate(context.Request.Headers.Origin.FirstOrDefault(), UriKind.Absolute, out var originUri) ||
             !webSocketConfig.GetAllowedOriginDomains().Contains(originUri.Host, StringComparer.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.Headers.CacheControl = "no-store";

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var path = context.Request.Path.Value;

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath)
        {
            context.Response.Headers.Append("Clear-Site-Data", "\"cache\"");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.HotReloadSettingsPath)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{ \"hotReload\": true }", context.RequestAborted);
            return;
        }

        if (path != BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

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

        var sessionKey = sessionKeyFactory();
        if (sessionKey == null)
        {
            // The browser tools build outputs can't be used.
            // The launch fails instead of silently continuing without browser tools, because a provider whose
            // key the application does not pin can never be authenticated by the browser and every browser
            // tools feature would appear to be broken for no visible reason.
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        string sharedSecret;
        using (sessionKey)
        {
            // The browser generated secret, encrypted with the build-pinned public key, is the only
            // credential. Reject before upgrading the connection so an unauthenticated peer never gets
            // a socket.
            try
            {
                sharedSecret = sessionKey.DecryptSecret(WebUtility.UrlDecode(subProtocol));
            }
            catch (Exception e)
            {
                Logger.LogDebug("Rejecting a browser connection with an invalid encrypted secret: {Message}", e.Message);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }

        var clientSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol);

        var connection = OnBrowserConnected(clientSocket, sharedSecret);
        await InitializeBrowserConnectionAsync(connection, context.RequestAborted);
        await connection.Disconnected.Task;
    }
}

#endif
