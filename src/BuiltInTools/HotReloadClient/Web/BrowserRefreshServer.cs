// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
    ILoggerFactory loggerFactory,
    string middlewareAssemblyPath,
    string dotnetPath,
    string? autoReloadWebSocketHostName,
    int? autoReloadWebSocketPort,
    bool suppressTimeouts)
    : AbstractBrowserRefreshServer(middlewareAssemblyPath, logger, loggerFactory)
{
    protected override bool SuppressTimeouts
        => suppressTimeouts;

    protected override async ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
    {
        var hostName = autoReloadWebSocketHostName ?? "127.0.0.1";
        var port = autoReloadWebSocketPort ?? 0;
        var supportsTls = await KestrelWebSocketServer.IsTlsSupportedAsync(dotnetPath, suppressTimeouts, cancellationToken);

        var server = new KestrelWebSocketServer(Logger, WebSocketRequestAsync);
        await server.StartServerAsync(hostName, port, supportsTls, cancellationToken);

        // URLs are only available after the server has started.
        return new WebServerHost(server, GetServerUrls(server.ServerUrls), virtualDirectory: "/");
    }

    private ImmutableArray<string> GetServerUrls(ImmutableArray<string> serverUrls)
    {
        Debug.Assert(serverUrls.Length > 0);

        if (autoReloadWebSocketHostName is null)
        {
            return [.. serverUrls.Select(s =>
                s.Replace("http://127.0.0.1", "ws://localhost", StringComparison.Ordinal)
                .Replace("https://127.0.0.1", "wss://localhost", StringComparison.Ordinal))];
        }

        return
        [
            serverUrls
                .First()
                .Replace("https://", "wss://", StringComparison.Ordinal)
                .Replace("http://", "ws://", StringComparison.Ordinal)
        ];
    }

    private async Task WebSocketRequestAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        if (context.WebSockets.WebSocketRequestedProtocols is not [var subProtocol])
        {
            subProtocol = null;
        }

        var clientSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol);

        var connection = OnBrowserConnected(clientSocket, subProtocol);
        await connection.Disconnected.Task;
    }
}

#endif
