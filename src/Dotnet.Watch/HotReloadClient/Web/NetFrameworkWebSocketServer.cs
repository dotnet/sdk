// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class NetFrameworkWebSocketServer : IAsyncDisposable
{
    public ImmutableArray<string> ServerUrls { get; }
    public ImmutableArray<string> HttpServerUrls { get; }

    private readonly DisposalCancellationSource _disposalCancellationSource;
    private readonly Task _listeningTask;

    private NetFrameworkWebSocketServer(
        ImmutableArray<string> serverUrls,
        ImmutableArray<string> httpServerUrls,
        DisposalCancellationSource cancellationSource,
        Task listeningTask)
    {
        ServerUrls = serverUrls;
        HttpServerUrls = httpServerUrls;

        _listeningTask = listeningTask;
        _disposalCancellationSource = cancellationSource;
    }

    public async ValueTask DisposeAsync()
    {
        _disposalCancellationSource.CancelAndDispose();

        if (_listeningTask is { } task)
        {
            await task;
        }
    }

    public static NetFrameworkWebSocketServer StartServer(
        WebSocketConfig config,
        Func<WebSocketConfig, HttpContext, Task> requestHandler,
        ILogger logger)
    {
        if (config.Port == 0 || config.SecurePort == 0)
        {
            throw new NotSupportedException($"{nameof(NetFrameworkWebSocketServer)} does not support auto-assigned ports.");
        }

        var httpServerUrls = config.GetHttpUrls().ToImmutableArray();
        var serverUrls = ImmutableArray.CreateRange(httpServerUrls, WebSocketConfig.GetWebSocketUrl);

        var httpListener = new HttpListener();

        foreach (var endpoint in httpServerUrls)
        {
            // HttpListener requires prefixes to end with '/'.
            httpListener.Prefixes.Add(endpoint.EndsWith("/") ? endpoint : endpoint + "/");
        }

        var disposalCancellationSource = new DisposalCancellationSource();
        var listeningTask = ListenAsync(disposalCancellationSource.Token);

        return new NetFrameworkWebSocketServer(serverUrls, httpServerUrls, disposalCancellationSource, listeningTask);

        async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                httpListener.Start();

                while (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug("Waiting for a browser connection");

                    var context = await httpListener.GetContextAsync();
                    try
                    {
                        await requestHandler(config, new HttpContext(context, cancellationToken));
                    }
                    finally
                    {
                        try
                        {
                            context.Response.Close();
                        }
                        catch (ObjectDisposedException)
                        {
                            // AcceptWebSocketAsync may already have disposed the response.
                        }
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                // expected during shutdown
            }
            catch (Exception e)
            {
                logger.LogError("Unexpected exception: {Message}", e.Message);
            }
        }
    }

}

#endif
