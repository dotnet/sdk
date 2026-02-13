// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Named pipe transport for communication between dotnet-watch and the hot reload agent.
/// Used for local processes where named pipes are available.
/// </summary>
internal sealed class NamedPipeClientTransport(ILogger logger) : ClientTransport
{
    private readonly string _namedPipeName = Guid.NewGuid().ToString("N");
    private NamedPipeServerStream? _pipe;

    /// <summary>
    /// The named pipe name, for testing.
    /// </summary>
    internal string NamedPipeName => _namedPipeName;

    public override void ConfigureEnvironment(IDictionary<string, string> env)
    {
        env[AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName] = _namedPipeName;
    }

    public override async Task<string> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
#if NET
        var options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
#else
        var options = PipeOptions.Asynchronous;
#endif
        _pipe = new NamedPipeServerStream(_namedPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options);

        logger.LogDebug("Waiting for application to connect to pipe {NamedPipeName}.", _namedPipeName);

        await _pipe.WaitForConnectionAsync(cancellationToken);

        // When the client connects, the first payload it sends is the initialization payload which includes the apply capabilities.
        var capabilities = (await ClientInitializationResponse.ReadAsync(_pipe, cancellationToken)).Capabilities;

        return capabilities;
    }

    /// <summary>
    /// Returns true if the exception is expected when the pipe is disposed or the process has terminated.
    /// On Unix named pipes can also throw SocketException with ErrorCode 125 (Operation canceled) when disposed.
    /// </summary>
    private static bool IsExpectedPipeException(Exception e, CancellationToken cancellationToken)
    {
        return e is ObjectDisposedException or EndOfStreamException or SocketException { ErrorCode: 125 }
            || cancellationToken.IsCancellationRequested;
    }

    public override async ValueTask WriteAsync(byte type, Func<Stream, CancellationToken, ValueTask>? writePayload, CancellationToken cancellationToken)
    {
        Debug.Assert(_pipe != null);

        await _pipe.WriteAsync(type, cancellationToken);

        if (writePayload != null)
        {
            await writePayload(_pipe, cancellationToken);
        }

        await _pipe.FlushAsync(cancellationToken);
    }

    public override async ValueTask<ClientTransportResponse?> ReadAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_pipe != null);

        try
        {
            var type = (ResponseType)await _pipe.ReadByteAsync(cancellationToken);
            return new ClientTransportResponse(type, _pipe, disposeStream: false);
        }
        catch (Exception e) when (e is not OperationCanceledException && IsExpectedPipeException(e, cancellationToken))
        {
            // Pipe has been disposed or the process has terminated.
            return null;
        }
    }

    public override void Dispose()
    {
        if (_pipe != null)
        {
            logger.LogDebug("Disposing agent communication pipe");

            // Dispose the pipe but do not set it to null, so that any in-progress
            // operations throw the appropriate exception type.
            _pipe.Dispose();
        }
    }
}
