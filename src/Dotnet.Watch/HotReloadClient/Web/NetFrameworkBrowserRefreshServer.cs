// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

#nullable enable

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class NetFrameworkBrowserRefreshServer(
    ILogger logger,
    Func<int, ILogger> connectionServerLoggerFactory,
    Func<int, ILogger> connectionAgentLoggerFactory,
    string middlewareAssemblyPath,
    bool useGatewayProxy,
    bool suppressTimeouts)
    : AbstractBrowserRefreshServer(
        logger,
        connectionAgentLoggerFactory,
        connectionServerLoggerFactory,
        middlewareAssemblyPath,
        useGatewayProxy,
        suppressTimeouts)
{
    private readonly DisposalCancellationSource _disposalCancellationSource = new();
    private Task? _webSocketListeningTask;

    public override async ValueTask DisposeAsync()
    {
        _disposalCancellationSource.CancelAndDispose();

        if (_webSocketListeningTask is { } task)
        {
            await task;
        }

        await base.DisposeAsync();
    }

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(WebSocketConfig config, CancellationToken cancellationToken)
    {
        var httpListener = new HttpListener();
        var httpEndpoints = config.GetHttpUrls().ToImmutableArray();

        foreach (var endpoint in httpEndpoints)
        {
            httpListener.Prefixes.Add(endpoint);
        }

        _webSocketListeningTask = ListenAsync(_disposalCancellationSource.Token);

        return new(new WebServerHost(
            httpListener,
            ImmutableArray.CreateRange(httpEndpoints, WebSocketConfig.GetWebSocketUrl),
            httpEndpoints));

        async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                httpListener.Start();

                while (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogDebug("Waiting for a browser connection");

                    // wait for incoming request:
                    var context = await httpListener.GetContextAsync();
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    // check the domain of the Origin header:
                    if (context.Request.Headers["Origin"] is not { } origin ||
                        !Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
                        !config.GetAllowedOriginDomains().Contains(originUri.Host, StringComparer.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 403;
                        context.Response.Close();
                        continue;
                    }

                    try
                    {
                        // Accepting Socket Next request. If the context has a "Sec-WebSocket-Protocol" header it passes back in the AcceptWebSocket
                        var protocol = context.Request.Headers["Sec-WebSocket-Protocol"];
                        var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: protocol).WaitAsync(cancellationToken);

                        _ = OnBrowserConnected(webSocketContext.WebSocket, webSocketContext.SecWebSocketProtocols.FirstOrDefault());
                    }
                    catch (Exception e) when (e is not (OperationCanceledException or ObjectDisposedException))
                    {
                        Logger.LogError("Accepting web socket exception: {Message}", e.Message);

                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                // expected during shutdown
            }
            catch (Exception e)
            {
                Logger.LogError("Unexpected HttpListener exception: {Message}", e.Message);
            }
        }
    }
}

#endif
