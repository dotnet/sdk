// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Base class for WebSocket servers using Kestrel.
/// Provides common infrastructure for mobile hot reload.
/// </summary>
internal abstract class KestrelWebSocketServer : IDisposable
{
    protected IHost? Host { get; private set; }
    protected ImmutableArray<string> ServerUrls { get; private set; } = [];
    protected ILogger Logger { get; }

    protected KestrelWebSocketServer(ILogger logger)
    {
        Logger = logger;
    }

    public virtual void Dispose()
    {
        Host?.Dispose();
    }

    /// <summary>
    /// Starts the Kestrel WebSocket server.
    /// </summary>
    /// <param name="hostName">Host name to bind to (default: localhost)</param>
    /// <param name="port">Port to bind to (0 for auto-assign)</param>
    /// <param name="useTls">Whether to enable HTTPS</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async ValueTask StartServerAsync(string hostName = "localhost", int port = 0, bool useTls = false, CancellationToken cancellationToken = default)
    {
        if (Host != null)
        {
            throw new InvalidOperationException("Server already started");
        }

        Host = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseKestrel();

                if (useTls)
                {
                    builder.UseUrls($"https://{hostName}:{port}", $"http://{hostName}:{port}");
                }
                else
                {
                    builder.UseUrls($"http://{hostName}:{port}");
                }

                builder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Run(HandleRequestAsync);
                });
            })
            .Build();

        await Host.StartAsync(cancellationToken);

        // URLs are only available after the server has started.
        var addresses = Host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;

        if (addresses != null)
        {
            ServerUrls = [.. addresses.Select(ConvertToWebSocketUrl)];
        }

        Logger.LogDebug("WebSocket server started at: {Urls}", string.Join(", ", ServerUrls));
    }

    /// <summary>
    /// Converts an HTTP(S) URL to a WebSocket URL.
    /// </summary>
    protected virtual string ConvertToWebSocketUrl(string httpUrl)
        => httpUrl
            .Replace("http://", "ws://", StringComparison.Ordinal)
            .Replace("https://", "wss://", StringComparison.Ordinal);

    /// <summary>
    /// Handles incoming HTTP requests. Override to implement WebSocket logic.
    /// </summary>
    protected abstract Task HandleRequestAsync(HttpContext context);

    /// <summary>
    /// Helper to accept a WebSocket connection.
    /// </summary>
    protected async ValueTask<WebSocket?> AcceptWebSocketAsync(HttpContext context, string? subProtocol = null)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return null;
        }

        return await context.WebSockets.AcceptWebSocketAsync(subProtocol);
    }
}

#endif
