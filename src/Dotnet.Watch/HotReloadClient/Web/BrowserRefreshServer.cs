// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Kestrel-based Browser Refesh Server implementation.
/// Delegates Kestrel lifecycle to <see cref="KestrelWebSocketServer"/>.
/// </summary>
internal sealed class BrowserRefreshServer(
    ILogger logger,
    Func<int, ILogger> connectionServerLoggerFactory,
    Func<int, ILogger> connectionAgentLoggerFactory,
    string dotnetPath,
    WebSocketConfig webSocketConfig,
    bool suppressTimeouts)
    : AbstractBrowserRefreshServer(logger, connectionServerLoggerFactory, connectionAgentLoggerFactory)
{
    private BrowserToolsEndpointRouter? _browserToolsEndpointRouter;

    protected override bool SuppressTimeouts
        => suppressTimeouts;

    protected override async ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
    {
        var supportsTls = await KestrelWebSocketServer.IsTlsSupportedAsync(dotnetPath, suppressTimeouts, cancellationToken);
        if (!supportsTls)
        {
            webSocketConfig = webSocketConfig.WithSecurePort(null);
        }

        var server = await KestrelWebSocketServer.StartServerAsync(webSocketConfig, HandleRequestAsync, cancellationToken);
        _browserToolsEndpointRouter = new BrowserToolsEndpointRouter(
            Guid.NewGuid(),
            PublicKey,
            BrowserToolsUpdateStore,
            this);

        // URLs are only available after the server has started.
        return new WebServerHost(
            server,
            webSocketEndpoints: server.ServerUrls,
            httpEndpoints: server.HttpServerUrls);
    }

    private Task HandleRequestAsync(HttpContext context)
    {
        return (_browserToolsEndpointRouter ?? throw new InvalidOperationException("Server not started")).HandleAsync(context);
    }
}

#endif
