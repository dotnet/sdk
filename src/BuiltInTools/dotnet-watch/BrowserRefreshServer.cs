// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
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
    public class BrowserRefreshServer : IAsyncDisposable
    {
        private readonly byte[] ReloadMessage = Encoding.UTF8.GetBytes("Reload");
        private readonly byte[] WaitMessage = Encoding.UTF8.GetBytes("Wait");
        private readonly IReporter _reporter;
        private readonly TaskCompletionSource _taskCompletionSource;
        private IHost _refreshServer;
        private readonly ConcurrentDictionary<string, WebSocket> _connectedClients = new();

        public BrowserRefreshServer(IReporter reporter)
        {
            _reporter = reporter;
            _taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async ValueTask<string> StartAsync(CancellationToken cancellationToken)
        {
            var hostName = Environment.GetEnvironmentVariable("DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME") ?? "127.0.0.1";

            _refreshServer = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseKestrel();
                    builder.UseUrls($"http://{hostName}:0");

                    builder.Configure(app =>
                    {
                        app.UseWebSockets();
                        app.Run(WebSocketRequest);
                    });
                })
                .Build();

            await _refreshServer.StartAsync(cancellationToken);

            var serverUrl = _refreshServer.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses
                .First();

            return serverUrl.Replace("http://", "ws://");
        }

        private async Task WebSocketRequest(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }
            //new client add them to the list of clients
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _connectedClients.TryAdd(Guid.NewGuid().ToString(), webSocket);
            await _taskCompletionSource.Task;
        }

        public async ValueTask SendMessage(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < _connectedClients.Count; i++)
            {
                string clientId = _connectedClients.Keys.ElementAt(i);
                WebSocket _webSocket = _connectedClients.Where(x => x.Key.Equals(clientId)).Select(x => x.Value).FirstOrDefault();
                if (_webSocket == null || _webSocket.CloseStatus.HasValue)
                {
                    // Remove the client
                    _connectedClients.TryRemove(clientId, out _);
                    continue;
                }

                try
                {
                    await _webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
                catch (Exception ex)
                {
                    _reporter.Verbose($"Refresh server error: {ex}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            for (int i = 0; i < _connectedClients.Count; i++)
            {
                string clientId = _connectedClients.Keys.ElementAt(i);
                WebSocket _webSocket = _connectedClients.Where(x => x.Key.Equals(clientId)).Select(x => x.Value).FirstOrDefault();
                if (_webSocket != null)
                {
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default);
                    _webSocket.Dispose();
                    _connectedClients.TryRemove(clientId, out _);
                }

                if (_refreshServer != null)
                {
                    _refreshServer.Dispose();
                }
            }

            _taskCompletionSource.TrySetResult();
        }

        public ValueTask ReloadAsync(CancellationToken cancellationToken) => SendMessage(ReloadMessage, cancellationToken);

        public ValueTask SendWaitMessageAsync(CancellationToken cancellationToken) => SendMessage(WaitMessage, cancellationToken);
    }
}
