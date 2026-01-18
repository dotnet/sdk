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

    private static readonly string? s_namedPipeName = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName);
    private static readonly string? s_httpEndpoint = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadHttpEndpoint);

    public static Transport? TryCreate(Action<string> log, int timeoutMS = 5000)
        => !string.IsNullOrEmpty(s_namedPipeName)
            ? new NamedPipeTransport(s_namedPipeName, log, timeoutMS)
            : !string.IsNullOrEmpty(s_httpEndpoint)
            ? new HttpTransport(s_httpEndpoint, log, timeoutMS)
            : null;

    protected void Log(string message)
        => log(message);

    public abstract void Dispose();
    public abstract string DisplayName { get; }
    public abstract ValueTask SendAsync(IResponse response, CancellationToken cancellationToken);
    public abstract ValueTask<RequestStream> ReceiveAsync(CancellationToken cancellationToken);
}
