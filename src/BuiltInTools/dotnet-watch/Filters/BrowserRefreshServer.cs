// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
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
        private readonly List<(WebSocket clientSocket, string sharedSecret)> _clientSockets = new();
        private readonly RSA _rsa;
        private readonly DotNetWatchOptions _options;
        private readonly IReporter _reporter;
        private readonly string _muxerPath;
        private readonly TaskCompletionSource _terminateWebSocket;
        private readonly TaskCompletionSource _clientConnected;
        private IHost _refreshServer;

        public BrowserRefreshServer(DotNetWatchOptions options, IReporter reporter, string muxerPath)
        {
            _rsa = RSA.Create(2048);
            _options = options;
            _reporter = reporter;
            _muxerPath = muxerPath;
            _terminateWebSocket = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _clientConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string ServerKey => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());

        private Uri GetAsUri(string uri, string environmentVariableKey)
        {
            try
            {
                return new Uri(uri);
            }
            catch (Exception ex)
            {
                _reporter.Error($"Environment variable {environmentVariableKey}={uri} not a valid Uri.");
                _reporter.Verbose($"New Uri exception: {ex}");
            }

            return null;
        }

        private IEnumerable<Uri> GetAsUris(string[] uris, string environmentVariableKey)
        {
            return uris.Select(uri => GetAsUri(uri, environmentVariableKey));
        }

        private void SuggestEnvironmentVariableFormat(string envEndpoints)
        {
            _reporter.Verbose($"This is not accepted: '{envEndpoints}'.");
            _reporter.Verbose("Try something like: 'https://localhost:43399,http://localhost:8099'.");
        }

        public async ValueTask<IEnumerable<string>> StartAsync(CancellationToken cancellationToken)
        {
            const string wsEndpointsKey = "DOTNET_WATCH_AUTO_RELOAD_WS_ENDPOINT";
            const string wsHostnameKey = "DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME";
            Uri endpointSecure = null;
            Uri endpoint = null;
            var envEndpoints = Environment.GetEnvironmentVariable(wsEndpointsKey);
            var messagePrefix = () => $"{wsEndpointsKey} = '{envEndpoints}'";

            if (!string.IsNullOrWhiteSpace(envEndpoints))
            {
                var success = false;
                _reporter.Verbose($"{messagePrefix}. Attepmting to set WebSockets endpoints.");

                if (envEndpoints.Contains(','))
                {
                    var tokens = envEndpoints.Split(',');

                    if (tokens.Length != 2)
                    {
                        _reporter.Error($"Environment variable {wsEndpointsKey} cannot contain more than two endpoints.");
                        SuggestEnvironmentVariableFormat(envEndpoints);
                    }
                    else
                    {
                        var uris = GetAsUris(tokens, wsEndpointsKey);
                        endpointSecure = uris.SingleOrDefault(uri => uri.Scheme == "https");
                        endpoint = uris.SingleOrDefault(uri => uri.Scheme == "http");

                        if (endpointSecure == null || endpoint == null)
                        {
                            _reporter.Error($"When prodviding two {wsEndpointsKey} endpoints, one must be secure (https) and the other insecure (http).");
                            SuggestEnvironmentVariableFormat(envEndpoints);
                            endpointSecure = null;
                            endpoint = null;
                        }
                        else
                        {
                            success = true;
                        }
                    }
                }
                else
                {
                    endpoint = GetAsUri(envEndpoints, wsEndpointsKey);

                    if (endpoint.Scheme != "http")
                    {
                        _reporter.Error($"When prodviding only one {wsEndpointsKey} endpoint, it must be insecure (http).");
                    }
                    else
                    {
                        success = true;
                    }
                }

                if (success)
                {
                    _reporter.Verbose($"Applying {messagePrefix} succeeded.");
                }
                else
                {
                    _reporter.Error($"Setting {messagePrefix} failed.");
                }
            }

            // Only attempt to use the DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME environment variable
            // when the DOTNET_WATCH_AUTO_RELOAD_WS_ENDPOINT environment variable is not specified.
            if (endpointSecure == null && endpoint == null)
            {
                var envHostName = Environment.GetEnvironmentVariable(wsHostnameKey);
                var hostName = envHostName ?? "127.0.0.1";
                endpointSecure = GetAsUri($"https://{hostName}:0", wsHostnameKey);
                endpoint = GetAsUri($"http://{hostName}:0", wsHostnameKey);
            }

            var supportsTLS = await SupportsTLS();

            _refreshServer = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseKestrel();
                    if (supportsTLS)
                    {
                        builder.UseUrls(endpointSecure.ToString(), endpoint.ToString());
                    }
                    else
                    {
                        builder.UseUrls(endpoint.ToString());
                    }

                    builder.Configure(app =>
                    {
                        app.UseWebSockets();
                        app.Run(WebSocketRequest);
                    });
                })
                .Build();

            await _refreshServer.StartAsync(cancellationToken);

            var serverUrls = _refreshServer.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses;

            // if (envHostName is null)
            // {
            //     return serverUrls.Select(s =>
            //         s.Replace("http://127.0.0.1", "ws://localhost", StringComparison.Ordinal)
            //         .Replace("https://127.0.0.1", "wss://localhost", StringComparison.Ordinal));
            // }

            return new[]
            {
                serverUrls
                    .First()
                    .Replace("127.0.0.1", "localhost", StringComparison.Ordinal)
                    .Replace("https://", "wss://", StringComparison.Ordinal)
                    .Replace("http://", "ws://", StringComparison.Ordinal)
             };
        }

        private async Task WebSocketRequest(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var subProtocol = (string)null;
            string sharedSecret = null;
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

        public async Task WaitForClientConnectionAsync(CancellationToken cancellationToken)
        {
            using var progressCancellationSource = new CancellationTokenSource();

            var progressReportingTask = Task.Run(async () =>
            {
                while (!progressCancellationSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(_options.TestFlags != TestFlags.None ? TimeSpan.MaxValue : TimeSpan.FromSeconds(5), progressCancellationSource.Token);
                    _reporter.Warn("Connecting to the browser is taking longer than expected ...");
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
        }

        public ValueTask SendJsonSerlialized<TValue>(TValue value, CancellationToken cancellationToken = default)
        {
            var jsonSerialized = JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);
            return SendMessage(jsonSerialized, cancellationToken);
        }

        public async ValueTask SendJsonWithSecret<TValue>(Func<string, TValue> valueFactory, CancellationToken cancellationToken = default)
        {
            try
            {
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
                }
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
                for (var i = 0; i < _clientSockets.Count; i++)
                {
                    var (clientSocket, _) = _clientSockets[i];
                    if (clientSocket.State is not WebSocketState.Open)
                    {
                        continue;
                    }
                    await clientSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
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

            if (_refreshServer != null)
            {
                _refreshServer.Dispose();
            }

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
                using var process = Process.Start(_muxerPath, "dev-certs https --check --quiet");
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
