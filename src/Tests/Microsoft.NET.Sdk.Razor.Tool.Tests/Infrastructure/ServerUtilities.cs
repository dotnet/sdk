﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.NET.Sdk.Razor.Tool;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    internal static class ServerUtilities
    {
        internal static string DefaultClientDirectory { get; } = Path.GetDirectoryName(typeof(ServerUtilities).Assembly.Location);

        internal static ServerPaths CreateBuildPaths(string workingDir, string tempDir)
        {
            return new ServerPaths(
                clientDir: DefaultClientDirectory,
                workingDir: workingDir,
                tempDir: tempDir);
        }

        internal static ServerData CreateServer(
            string pipeName = null,
            CompilerHost compilerHost = null,
            ConnectionHost connectionHost = null,
            Action<object, EventArgs> onListening = null)
        {
            pipeName = pipeName ?? Guid.NewGuid().ToString();
            compilerHost = compilerHost ?? CompilerHost.Create();
            connectionHost = connectionHost ?? ConnectionHost.Create(pipeName);

            var serverStatsSource = new TaskCompletionSource<ServerStats>();
            var serverListenSource = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource();
            var mutexName = MutexName.GetServerMutexName(pipeName);
            var thread = new Thread(_ =>
            {
                var eventBus = new TestableEventBus();
                eventBus.Listening += (sender, e) => { serverListenSource.TrySetResult(true); };
                if (onListening != null)
                {
                    eventBus.Listening += (sender, e) => onListening(sender, e);
                }
                try
                {
                    RunServer(
                        pipeName,
                        connectionHost,
                        compilerHost,
                        cts.Token,
                        eventBus,
                        Timeout.InfiniteTimeSpan);
                }
                finally
                {
                    var serverStats = new ServerStats(connections: eventBus.ConnectionCount, completedConnections: eventBus.CompletedCount);
                    serverStatsSource.SetResult(serverStats);
                }
            });

            thread.Start();

            // The contract of this function is that it will return once the server has started.  Spin here until
            // we can verify the server has started or simply failed to start.
            while (ServerConnection.WasServerMutexOpen(mutexName) != true && thread.IsAlive)
            {
                Thread.Yield();
            }

            return new ServerData(cts, pipeName, serverStatsSource.Task, serverListenSource.Task);
        }

        internal static async Task<ServerResponse> Send(string pipeName, ServerRequest request)
        {
            using (var client = await Client.ConnectAsync(pipeName, timeout: null, cancellationToken: default).ConfigureAwait(false))
            {
                await request.WriteAsync(client.Stream).ConfigureAwait(false);
                return await ServerResponse.ReadAsync(client.Stream).ConfigureAwait(false);
            }
        }

        internal static async Task<int> SendShutdown(string pipeName)
        {
            var response = await Send(pipeName, ServerRequest.CreateShutdown());
            return ((ShutdownServerResponse)response).ServerProcessId;
        }

        internal static int RunServer(
            string pipeName,
            ConnectionHost host,
            CompilerHost compilerHost,
            CancellationToken cancellationToken = default,
            EventBus eventBus = null,
            TimeSpan? keepAlive = null)
        {
            var command = new TestableServerCommand(host, compilerHost, cancellationToken, eventBus, keepAlive);
            var args = new List<string>
            {
                "-p",
                pipeName
            };

            var result = command.Execute(args.ToArray());
            return result;
        }

        private class TestableServerCommand : ServerCommand
        {
            private readonly ConnectionHost _host;
            private readonly CompilerHost _compilerHost;
            private readonly EventBus _eventBus;
            private readonly CancellationToken _cancellationToken;
            private readonly TimeSpan? _keepAlive;

            public TestableServerCommand(
                ConnectionHost host,
                CompilerHost compilerHost,
                CancellationToken ct,
                EventBus eventBus,
                TimeSpan? keepAlive)
                : base(new Application(ct, Mock.Of<ExtensionAssemblyLoader>(), Mock.Of<ExtensionDependencyChecker>(), (path, properties) => Mock.Of<PortableExecutableReference>()))
            {
                _host = host;
                _compilerHost = compilerHost;
                _cancellationToken = ct;
                _eventBus = eventBus;
                _keepAlive = keepAlive;
            }

            protected override void ExecuteServerCore(
                ConnectionHost host,
                CompilerHost compilerHost,
                CancellationToken cancellationToken,
                EventBus eventBus,
                TimeSpan? keepAlive = null)
            {
                base.ExecuteServerCore(
                    _host ?? host,
                    _compilerHost ?? compilerHost,
                    _cancellationToken,
                    _eventBus ?? eventBus,
                    _keepAlive ?? keepAlive);
            }
        }
    }
}
