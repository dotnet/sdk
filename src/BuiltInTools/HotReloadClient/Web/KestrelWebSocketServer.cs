// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
/// Sealed WebSocket server using Kestrel.
/// Uses a request handler delegate for all WebSocket handling.
/// </summary>
internal sealed class KestrelWebSocketServer : IDisposable
{
    private readonly RequestDelegate _requestHandler;
    private readonly ILogger _logger;

    private IHost? _host;
    public ImmutableArray<string> ServerUrls { get; private set; } = [];

    public KestrelWebSocketServer(ILogger logger, RequestDelegate requestHandler)
    {
        _logger = logger;
        _requestHandler = requestHandler;
    }

    public void Dispose()
    {
        _host?.Dispose();
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
    /// <param name="port">HTTP port to bind to (0 for auto-assign)</param>
    /// <param name="securePort">HTTPS port to bind to in addition to HTTP port. Null to skip HTTPS.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async ValueTask StartServerAsync(string hostName, int port, int? securePort, CancellationToken cancellationToken)
    {
        if (_host != null)
        {
            throw new InvalidOperationException("Server already started");
        }

        _host = new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseKestrel();

                if (securePort.HasValue)
                {
                    builder.UseUrls($"http://{hostName}:{port}", $"https://{hostName}:{securePort.Value}");
                }
                else
                {
                    builder.UseUrls($"http://{hostName}:{port}");
                }

                builder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Run(_requestHandler);
                });
            })
            .Build();

        await _host.StartAsync(cancellationToken);

        // URLs are only available after the server has started.
        var addresses = _host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;

        if (addresses != null)
        {
            ServerUrls = [.. addresses];
        }

        _logger.LogDebug("WebSocket server started at: {Urls}", string.Join(", ", ServerUrls.Select(url => GetWebSocketUrl(url))));
    }

    /// <summary>
    /// Converts an HTTP(S) URL to a WebSocket URL.
    /// When <paramref name="hostName"/> is not specified, also replaces 127.0.0.1 with localhost.
    /// </summary>
    internal static string GetWebSocketUrl(string httpUrl, string? hostName = null)
    {
        if (hostName is null)
        {
            return httpUrl
                .Replace("http://127.0.0.1", "ws://localhost", StringComparison.Ordinal)
                .Replace("https://127.0.0.1", "wss://localhost", StringComparison.Ordinal);
        }

        return httpUrl
            .Replace("https://", "wss://", StringComparison.Ordinal)
            .Replace("http://", "ws://", StringComparison.Ordinal);
    }
}

#endif
