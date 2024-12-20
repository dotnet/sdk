// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    /// <summary>
    /// Communicates with aspnetcore-browser-refresh.js loaded in the browser.
    /// </summary>
    internal sealed class BrowserRefreshServer : IAsyncDisposable
    {
        private static readonly ReadOnlyMemory<byte> s_reloadMessage = Encoding.UTF8.GetBytes("Reload");
        private static readonly ReadOnlyMemory<byte> s_waitMessage = Encoding.UTF8.GetBytes("Wait");
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly List<BrowserConnection> _activeConnections = [];
        private readonly RSA _rsa;
        private readonly IReporter _reporter;
        private readonly TaskCompletionSource _terminateWebSocket;
        private readonly TaskCompletionSource _browserConnected;
        private readonly string? _environmentHostName;

        // initialized by StartAsync
        private IHost? _refreshServer;
        private string? _serverUrls;

        public readonly EnvironmentOptions Options;

        public BrowserRefreshServer(EnvironmentOptions options, IReporter reporter)
        {
            _rsa = RSA.Create(2048);
            Options = options;
            _reporter = reporter;
            _terminateWebSocket = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _browserConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _environmentHostName = EnvironmentVariables.AutoReloadWSHostName;
        }

        public async ValueTask DisposeAsync()
        {
            _rsa.Dispose();

            BrowserConnection[] connectionsToDispose;
            lock (_activeConnections)
            {
                connectionsToDispose = [.. _activeConnections];
                _activeConnections.Clear();
            }

            foreach (var connection in connectionsToDispose)
            {
                _reporter.Verbose($"Disconnecting from browser #{connection.Id}.");
                await connection.DisposeAsync();
            }

            _refreshServer?.Dispose();

            _terminateWebSocket.TrySetResult();
        }

        public void SetEnvironmentVariables(EnvironmentVariablesBuilder environmentBuilder)
        {
            Debug.Assert(_refreshServer != null);
            Debug.Assert(_serverUrls != null);

            environmentBuilder.SetVariable(EnvironmentVariables.Names.AspNetCoreAutoReloadWSEndPoint, _serverUrls);
            environmentBuilder.SetVariable(EnvironmentVariables.Names.AspNetCoreAutoReloadWSKey, GetServerKey());

            environmentBuilder.DotNetStartupHookDirective.Add(Path.Combine(AppContext.BaseDirectory, "middleware", "Microsoft.AspNetCore.Watch.BrowserRefresh.dll"));
            environmentBuilder.AspNetCoreHostingStartupAssembliesVariable.Add("Microsoft.AspNetCore.Watch.BrowserRefresh");

            if (_reporter.IsVerbose)
            {
                // enable debug logging from middleware:
                environmentBuilder.SetVariable("Logging__LogLevel__Microsoft.AspNetCore.Watch", "Debug");
            }
        }

        public string GetServerKey()
            => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());

        public async ValueTask StartAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(_refreshServer == null);

            var hostName = _environmentHostName ?? "127.0.0.1";

            var supportsTLS = await SupportsTlsAsync();

            _refreshServer = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseKestrel();
                    if (supportsTLS)
                    {
                        builder.UseUrls($"https://{hostName}:0", $"http://{hostName}:0");
                    }
                    else
                    {
                        builder.UseUrls($"http://{hostName}:0");
                    }

                    builder.Configure(app =>
                    {
                        app.UseWebSockets();
                        app.Run(WebSocketRequestAsync);
                    });
                })
                .Build();

            await _refreshServer.StartAsync(cancellationToken);

            var serverUrls = string.Join(',', GetServerUrls(_refreshServer));
            _reporter.Verbose($"Refresh server running at {serverUrls}.");
            _serverUrls = serverUrls;
        }

        private IEnumerable<string> GetServerUrls(IHost server)
        {
            var serverUrls = server.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses;

            Debug.Assert(serverUrls != null);

            if (_environmentHostName is null)
            {
                return serverUrls.Select(s =>
                    s.Replace("http://127.0.0.1", "ws://localhost", StringComparison.Ordinal)
                    .Replace("https://127.0.0.1", "wss://localhost", StringComparison.Ordinal));
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

            string? subProtocol = null;
            string? sharedSecret = null;
            if (context.WebSockets.WebSocketRequestedProtocols.Count == 1)
            {
                subProtocol = context.WebSockets.WebSocketRequestedProtocols[0];
                var subProtocolBytes = Convert.FromBase64String(WebUtility.UrlDecode(subProtocol));
                sharedSecret = Convert.ToBase64String(_rsa.Decrypt(subProtocolBytes, RSAEncryptionPadding.OaepSHA256));
            }

            var clientSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol);
            var connection = new BrowserConnection(clientSocket, sharedSecret, _reporter);

            lock (_activeConnections)
            {
                _activeConnections.Add(connection);
            }

            _browserConnected.TrySetResult();
            await _terminateWebSocket.Task;
        }

        /// <summary>
        /// For testing.
        /// </summary>
        internal void EmulateClientConnected()
        {
            _browserConnected.TrySetResult();
        }

        public async Task WaitForClientConnectionAsync(CancellationToken cancellationToken)
        {
            using var progressCancellationSource = new CancellationTokenSource();

            // It make take a while to connect since the app might need to build first.
            // Indicate progress in the output. Start with 60s and then report progress every 10s.
            var firstReportSeconds = TimeSpan.FromSeconds(60);
            var nextReportSeconds = TimeSpan.FromSeconds(10);

            var reportDelayInSeconds = firstReportSeconds;
            var connectionAttemptReported = false;

            var progressReportingTask = Task.Run(async () =>
            {
                while (!progressCancellationSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(Options.TestFlags != TestFlags.None ? TimeSpan.MaxValue : reportDelayInSeconds, progressCancellationSource.Token);

                    connectionAttemptReported = true;
                    reportDelayInSeconds = nextReportSeconds;
                    _reporter.Output("Connecting to the browser ...");
                }
            }, progressCancellationSource.Token);

            try
            {
                await _browserConnected.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                progressCancellationSource.Cancel();
            }

            if (connectionAttemptReported)
            {
                _reporter.Output("Browser connection established.");
            }
        }

        private IReadOnlyCollection<BrowserConnection> GetOpenBrowserConnections()
        {
            lock (_activeConnections)
            {
                return [.. _activeConnections.Where(b => b.ClientSocket.State == WebSocketState.Open)];
            }
        }

        private async ValueTask DisposeClosedBrowserConnectionsAsync()
        {
            List<BrowserConnection>? lazyConnectionsToDispose = null;

            lock (_activeConnections)
            {
                var j = 0;
                for (var i = 0; i < _activeConnections.Count; i++)
                {
                    var connection = _activeConnections[i];
                    if (connection.ClientSocket.State == WebSocketState.Open)
                    {
                        _activeConnections[j++] = connection;
                    }
                    else
                    {
                        lazyConnectionsToDispose ??= [];
                        lazyConnectionsToDispose.Add(connection);
                    }
                }

                _activeConnections.RemoveRange(j, _activeConnections.Count - j);
            }

            if (lazyConnectionsToDispose != null)
            {
                foreach (var connection in lazyConnectionsToDispose)
                {
                    await connection.DisposeAsync();
                }
            }
        }

        public static ReadOnlyMemory<byte> SerializeJson<TValue>(TValue value)
            => JsonSerializer.SerializeToUtf8Bytes(value, s_jsonSerializerOptions);

        public static TValue DeserializeJson<TValue>(ReadOnlySpan<byte> value)
            => JsonSerializer.Deserialize<TValue>(value, s_jsonSerializerOptions) ?? throw new InvalidDataException("Unexpected null object");

        public ValueTask SendJsonMessageAsync<TValue>(TValue value, CancellationToken cancellationToken)
            => SendAsync(SerializeJson(value), cancellationToken);

        public ValueTask SendReloadMessageAsync(CancellationToken cancellationToken)
            => SendAsync(s_reloadMessage, cancellationToken);

        public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken)
            => SendAsync(s_waitMessage, cancellationToken);

        public ValueTask SendAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
            => SendAndReceiveAsync(request: _ => messageBytes, response: null, cancellationToken);

        public async ValueTask SendAndReceiveAsync<TRequest>(
            Func<string?, TRequest> request,
            Action<ReadOnlySpan<byte>, IReporter>? response,
            CancellationToken cancellationToken)
        {
            var responded = false;

            foreach (var connection in GetOpenBrowserConnections())
            {
                var requestValue = request(connection.SharedSecret);
                var requestBytes = requestValue is ReadOnlyMemory<byte> bytes ? bytes : SerializeJson(requestValue);

                if (!await connection.TrySendMessageAsync(requestBytes, cancellationToken))
                {
                    continue;
                }

                if (response != null && !await connection.TryReceiveMessageAsync(response, cancellationToken))
                {
                    continue;
                }

                responded = true;
            }

            if (!responded)
            {
                _reporter.Verbose($"Failed to receive response from a connected browser.");
            }

            await DisposeClosedBrowserConnectionsAsync();
        }

        private async Task<bool> SupportsTlsAsync()
        {
            try
            {
                using var process = Process.Start(Options.MuxerPath, "dev-certs https --check --quiet");
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public ValueTask RefreshBrowserAsync(CancellationToken cancellationToken)
            => SendJsonMessageAsync(new AspNetCoreHotReloadApplied(), cancellationToken);

        public ValueTask ReportCompilationErrorsInBrowserAsync(ImmutableArray<string> compilationErrors, CancellationToken cancellationToken)
        {
            _reporter.Verbose($"Updating diagnostics in the browser.");
            if (compilationErrors.IsEmpty)
            {
                return SendJsonMessageAsync(new AspNetCoreHotReloadApplied(), cancellationToken);
            }
            else
            {
                return SendJsonMessageAsync(new HotReloadDiagnostics { Diagnostics = compilationErrors }, cancellationToken);
            }
        }

        private readonly struct AspNetCoreHotReloadApplied
        {
            public string Type => "AspNetCoreHotReloadApplied";
        }

        private readonly struct HotReloadDiagnostics
        {
            public string Type => "HotReloadDiagnosticsv1";

            public IEnumerable<string> Diagnostics { get; init; }
        }
    }
}
