// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal abstract class Transport(Action<string> log) : IDisposable
{
    public readonly struct RequestStream(Stream? stream, bool disposeOnCompletion) : IDisposable
    {
        public Stream? Stream => stream;

        public void Dispose()
        {
            if (disposeOnCompletion)
            {
                stream?.Dispose();
            }
        }
    }

    public static Transport? TryCreate(Action<string> log, int timeoutMS = 5000)
    {
        var namedPipeName = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName);
        if (!string.IsNullOrEmpty(namedPipeName))
        {
            log($"{AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName}={namedPipeName}");
            return new NamedPipeTransport(namedPipeName, log, timeoutMS);
        }

        var webSocketEndpoint = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint);
        if (!string.IsNullOrEmpty(webSocketEndpoint))
        {
            log($"{AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint}={webSocketEndpoint}");
            return new WebSocketTransport(webSocketEndpoint, log, timeoutMS);
        }

        return null;
    }

    protected void Log(string message)
        => log(message);

    public abstract void Dispose();
    public abstract string DisplayName { get; }
    public abstract ValueTask SendAsync(IResponse response, CancellationToken cancellationToken);
    public abstract ValueTask<RequestStream> ReceiveAsync(CancellationToken cancellationToken);
}
