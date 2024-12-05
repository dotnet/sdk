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
        private readonly byte[] ReloadMessage = Encoding.UTF8.GetBytes("Reload");
        private readonly byte[] WaitMessage = Encoding.UTF8.GetBytes("Wait");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly List<(WebSocket clientSocket, string? sharedSecret)> _clientSockets = new();
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
            _clientSockets.Add((clientSocket, sharedSecret));
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

        public ValueTask SendJsonSerlialized<TValue>(TValue value, CancellationToken cancellationToken = default)
        {
            var jsonSerialized = JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);
            return SendMessage(jsonSerialized, cancellationToken);
        }

        public async ValueTask SendJsonWithSecret<TValue>(Func<string?, TValue> valueFactory, CancellationToken cancellationToken = default)
        {
            try
            {
                bool messageSent = false;

                for (var i = 0; i < _clientSockets.Count; i++)
                {
                    var (clientSocket, secret) = _clientSockets[i];
                    if (clientSocket.State is not WebSocketState.Open)
                    {
                        continue;
                    }

                    var value = valueFactory(secret);
                    var messageBytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);

                    await clientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                    messageSent = true;
                }

                _reporter.Verbose(messageSent ? "Browser message sent." : "Unable to send message to browser, no socket is open.");
            }
            catch (TaskCanceledException)
            {
                _reporter.Verbose("WebSocket connection has been terminated.");
            }
            catch (Exception ex)
            {
                _reporter.Verbose($"Refresh server error: {ex}");
            }
        }

        public async ValueTask SendMessage(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
        {
            try
            {
                bool messageSent = false;

                for (var i = 0; i < _clientSockets.Count; i++)
                {
                    var (clientSocket, _) = _clientSockets[i];
                    if (clientSocket.State is not WebSocketState.Open)
                    {
                        continue;
                    }

                    await clientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                    messageSent = true;
                }

                _reporter.Verbose(messageSent ? "Browser message sent." : "Unable to send message to browser, no socket is open.");
            }
            catch (TaskCanceledException)
            {
                _reporter.Verbose("WebSocket connection has been terminated.");
            }
            catch (Exception ex)
            {
                _reporter.Verbose($"Refresh server error: {ex}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            _rsa.Dispose();

            for (var i = 0; i < _clientSockets.Count; i++)
            {
                var (clientSocket, _) = _clientSockets[i];
                await clientSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, null, default);
                clientSocket.Dispose();
            }

            _refreshServer?.Dispose();

            _terminateWebSocket.TrySetResult();
        }

        public async ValueTask<ValueWebSocketReceiveResult?> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            for (int i = 0; i < _clientSockets.Count; i++)
            {
                var (clientSocket, _) = _clientSockets[i];

                if (clientSocket.State != WebSocketState.Open)
                {
                    continue;
                }

                try
                {
                    var result = await clientSocket.ReceiveAsync(buffer, cancellationToken);

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

            return default;
        }

        public ValueTask ReloadAsync(CancellationToken cancellationToken) => SendMessage(ReloadMessage, cancellationToken);

        public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken) => SendMessage(WaitMessage, cancellationToken);

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
            => SendJsonSerlialized(new AspNetCoreHotReloadApplied(), cancellationToken);

        public ValueTask ReportCompilationErrorsInBrowserAsync(ImmutableArray<string> compilationErrors, CancellationToken cancellationToken)
        {
            _reporter.Verbose($"Updating diagnostics in the browser.");
            if (compilationErrors.IsEmpty)
            {
                return SendJsonSerlialized(new AspNetCoreHotReloadApplied(), cancellationToken);
            }
            else
            {
                return SendJsonSerlialized(new HotReloadDiagnostics { Diagnostics = compilationErrors }, cancellationToken);
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
