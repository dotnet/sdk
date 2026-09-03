// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
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
    Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment,
    string dotnetPath,
    WebSocketConfig webSocketConfig,
    bool suppressTimeouts)
    : AbstractBrowserRefreshServer(configureLaunchEnvironment, logger, connectionServerLoggerFactory, connectionAgentLoggerFactory)
{
    protected override bool SuppressTimeouts
        => suppressTimeouts;

    protected override async ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
    {
        var supportsTls = await KestrelWebSocketServer.IsTlsSupportedAsync(dotnetPath, suppressTimeouts, cancellationToken);
        if (!supportsTls)
        {
            webSocketConfig = webSocketConfig.WithSecurePort(null);
        }

        // The browser reaches the provider through the application's own origin, so the provider only
        // listens on loopback. DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME no longer applies to this hop.
        var router = new BrowserToolsEndpointRouter(PublicKey, BrowserToolsUpdateStore, this);
        var server = await KestrelWebSocketServer.StartServerAsync(webSocketConfig.WithHostName(null), router.HandleAsync, cancellationToken);

        // URLs are only available after the server has started.
        return new WebServerHost(server, server.ServerUrls, server.HttpServerUrls);
    }
}

#endif
