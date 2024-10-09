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
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    /// <summary>
    /// Communicates with aspnetcore-browser-refresh.js loaded in the browser.
    /// </summary>
    internal sealed class BrowserRefreshServer : IAsyncDisposable
    {
        private readonly struct BrowserConnection(WebSocket clientSocket, string? sharedSecret, int id) : IAsyncDisposable
        {
            public WebSocket ClientSocket { get; } = clientSocket;
            public string? SharedSecret { get; } = sharedSecret;
            public int Id { get; } = id;

            internal async Task SendMessageAsync(ReadOnlyMemory<byte> messageBytes, IReporter reporter, CancellationToken cancellationToken)
            {
                try
                {
                    await ClientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    reporter.Verbose($"Browser connection #{Id} has been terminated.");
                }
                catch (Exception e)
                {
                    reporter.Verbose($"Failed to send message to browser #{Id}: {e.Message}");
                }
            }

            public async ValueTask DisposeAsync()
            {
                await ClientSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, default);
                ClientSocket.Dispose();
            }
        }

        private readonly byte[] ReloadMessage = Encoding.UTF8.GetBytes("Reload");
        private readonly byte[] WaitMessage = Encoding.UTF8.GetBytes("Wait");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly List<BrowserConnection> _activeConnections = [];
        private readonly RSA _rsa;

        private readonly IReporter _reporter;
        private readonly TaskCompletionSource _terminateWebSocket;
        private readonly TaskCompletionSource _clientConnected;
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
            _clientConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _environmentHostName = EnvironmentVariables.AutoReloadWSHostName;
        }

        public async ValueTask DisposeAsync()
        {
            _rsa.Dispose();

            foreach (var connection in await GetOpenBrowserConnectionsAsync())
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
        }

        public string GetServerKey()
            => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());

        public async ValueTask StartAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(_refreshServer == null);

            var hostName = _environmentHostName ?? "127.0.0.1";

            var supportsTLS = await SupportsTLS();

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
                        app.Run(WebSocketRequest);
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

        private async Task WebSocketRequest(HttpContext context)
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

            int connectionId;
            lock (_activeConnections)
            {
                connectionId = _activeConnections.Count;
                _activeConnections.Add(new BrowserConnection(clientSocket, sharedSecret, connectionId));
            }

            _reporter.Verbose($"Browser #{connectionId} connected to referesh server web socket.");

            _clientConnected.TrySetResult();
            await _terminateWebSocket.Task;
        }

        /// <summary>
        /// For testing.
        /// </summary>
        internal void EmulateClientConnected()
        {
            _clientConnected.TrySetResult();
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
                await _clientConnected.Task.WaitAsync(cancellationToken);
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

        private async ValueTask<IReadOnlyCollection<BrowserConnection>> GetOpenBrowserConnectionsAsync()
        {
            var openConnections = new List<BrowserConnection>();
            List<BrowserConnection>? lazyConnectionsToDispose = null;

            lock (_activeConnections)
            {
                openConnections.AddRange(_activeConnections.Where(b => b.ClientSocket.State == WebSocketState.Open));

                if (openConnections.Count < _activeConnections.Count)
                {
                    foreach (var connection in _activeConnections)
                    {
                        if (connection.ClientSocket.State != WebSocketState.Open)
                        {
                            lazyConnectionsToDispose ??= [];
                            lazyConnectionsToDispose.Add(connection);
                        }
                    }

                    _activeConnections.Clear();
                    _activeConnections.AddRange(openConnections);
                }
            }

            if (lazyConnectionsToDispose != null)
            {
                foreach (var connection in lazyConnectionsToDispose)
                {
                    await connection.DisposeAsync();
                    _reporter.Verbose($"Browser #{connection.Id} disconnected.");
                }
            }

            return openConnections;
        }

        public ValueTask SendJsonMessage<TValue>(TValue value, CancellationToken cancellationToken)
        {
            var jsonSerialized = JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);
            return Send(jsonSerialized, cancellationToken);
        }

        public async ValueTask SendJsonMessageWithSecret<TValue>(Func<string?, TValue> messageFactory, CancellationToken cancellationToken)
        {
            foreach (var connection in await GetOpenBrowserConnectionsAsync())
            {
                var message = messageFactory(connection.SharedSecret);
                var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, _jsonSerializerOptions);
                await connection.SendMessageAsync(messageBytes, _reporter, cancellationToken);
            }
        }

        public async ValueTask Send(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
        {
            foreach (var connection in await GetOpenBrowserConnectionsAsync())
            {
                await connection.SendMessageAsync(messageBytes, _reporter, cancellationToken);
            }
        }

        public async ValueTask<TValue> SendAndReceive<TValue>(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
        {
            foreach (var connection in await GetOpenBrowserConnectionsAsync())
            {
                await connection.SendMessageAsync(messageBytes, _reporter, cancellationToken);

                try
                {
                    var result = await connection.ReceiveMessageAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        continue;
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _reporter.Verbose($"Refresh server error: {ex}");
                }
            }
        }

        public ValueTask ReloadAsync(CancellationToken cancellationToken) => Send(ReloadMessage, cancellationToken);

        public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken) => Send(WaitMessage, cancellationToken);

        private async Task<bool> SupportsTLS()
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
            => SendJsonMessage(new AspNetCoreHotReloadApplied(), cancellationToken);

        public ValueTask ReportCompilationErrorsInBrowserAsync(ImmutableArray<string> compilationErrors, CancellationToken cancellationToken)
        {
            _reporter.Verbose($"Updating diagnostics in the browser.");
            if (compilationErrors.IsEmpty)
            {
                return SendJsonMessage(new AspNetCoreHotReloadApplied(), cancellationToken);
            }
            else
            {
                return SendJsonMessage(new HotReloadDiagnostics { Diagnostics = compilationErrors }, cancellationToken);
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
