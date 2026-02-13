// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
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
/// WebSocket server using Kestrel.
/// Can be used via inheritance (override <see cref="HandleRequestAsync"/>) or
/// via composition (pass a request handler delegate to the constructor).
/// </summary>
internal class KestrelWebSocketServer : IDisposable
{
    private readonly Func<HttpContext, Task>? _requestHandler;

    protected IHost? Host { get; private set; }
    public ImmutableArray<string> ServerUrls { get; private set; } = [];
    protected ILogger Logger { get; }

    /// <summary>
    /// Creates a server with a custom request handler (for composition).
    /// </summary>
    public KestrelWebSocketServer(ILogger logger, Func<HttpContext, Task> requestHandler)
    {
        Logger = logger;
        _requestHandler = requestHandler;
    }

    /// <summary>
    /// Creates a server for subclass override (for inheritance).
    /// </summary>
    protected KestrelWebSocketServer(ILogger logger)
    {
        Logger = logger;
    }

    public virtual void Dispose()
    {
        Host?.Dispose();
    }

    private static bool? s_lazyTlsSupported;

    /// <summary>
    /// Checks whether TLS is supported by running <c>dotnet dev-certs https --check --quiet</c>.
    /// </summary>
    public static async ValueTask<bool> IsTlsSupportedAsync(string dotnetPath, bool suppressTimeouts, CancellationToken cancellationToken)
    {
        var result = s_lazyTlsSupported;
        if (result.HasValue)
        {
            return result.Value;
        }

        try
        {
            using var process = Process.Start(dotnetPath, "dev-certs https --check --quiet");
            await process
                .WaitForExitAsync(cancellationToken)
                .WaitAsync(suppressTimeouts ? TimeSpan.MaxValue : TimeSpan.FromSeconds(10), cancellationToken);

            result = process.ExitCode == 0;
        }
        catch
        {
            result = false;
        }

        s_lazyTlsSupported = result;
        return result.Value;
    }

    /// <summary>
    /// Starts the Kestrel WebSocket server.
    /// </summary>
    /// <param name="hostName">Host name to bind to</param>
    /// <param name="port">Port to bind to (0 for auto-assign)</param>
    /// <param name="useTls">Whether to enable HTTPS</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async ValueTask StartServerAsync(string hostName, int port, bool useTls, CancellationToken cancellationToken)
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
            ServerUrls = [.. addresses];
        }

        Logger.LogDebug("WebSocket server started at: {Urls}", string.Join(", ", ServerUrls.Select(ConvertToWebSocketUrl)));
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
    protected virtual Task HandleRequestAsync(HttpContext context)
        => _requestHandler?.Invoke(context) ?? Task.CompletedTask;

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
