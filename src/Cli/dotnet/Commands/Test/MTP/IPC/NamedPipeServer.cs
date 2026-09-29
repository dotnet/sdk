// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class NamedPipeServer : NamedPipeBase
{
    // macOS allows 104 bytes in sockaddr_un.sun_path, including the NUL terminator.
    internal const int MaxUnixDomainSocketPathLengthInBytes = 103;
    internal const string PipeDirectoryEnvironmentVariable = "TESTINGPLATFORM_PIPE_DIRECTORY";
    internal const int MaximumFrameSize = HttpTestHostGateway.MaximumFrameSize;

    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    private readonly Func<NamedPipeServer, IRequest, Task<IResponse>> _callback;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly CancellationToken _cancellationToken;
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
    private readonly byte[] _sizeOfIntArray = new byte[sizeof(int)];
    private readonly bool _skipUnknownMessages;
    private Task? _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        string pipeName,
        Func<NamedPipeServer, IRequest, Task<IResponse>> callback,
        int maxNumberOfServerInstances,
        CancellationToken cancellationToken,
        bool skipUnknownMessages)
    {
        _namedPipeServerStream = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            inBufferSize: 0,
            outBufferSize: 0);

        _callback = callback;
        _cancellationToken = cancellationToken;
        _skipUnknownMessages = skipUnknownMessages;
    }

    public bool WasConnected { get; private set; }

    public async Task WaitConnectionAsync(CancellationToken cancellationToken)
    {
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken);
        WasConnected = true;
        _loopTask = Task.Run(
            async () =>
            {
                try
                {
                    await InternalLoopAsync(_cancellationToken);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                {
                    // We are being cancelled, so we don't need to wait anymore
                    return;
                }
            }, cancellationToken);
    }

    /// <summary>
    /// 4 bytes = message size
    /// ------- Payload -------
    /// 4 bytes = serializer id
    /// x bytes = object buffer.
    /// </summary>
    private async Task InternalLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IRequest? deserializedObject = await ReadRequestAsync(_namedPipeServerStream, cancellationToken);
            if (deserializedObject is null)
            {
                return;
            }

            IResponse response = await _callback(this, deserializedObject);
            INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());

            ResetBuffer(_serializationBuffer);
            responseNamedPipeSerializer.Serialize(response, _serializationBuffer);

            int sizeOfTheWholeMessage = checked((int)_serializationBuffer.Position + sizeof(int));
            ResetBuffer(_messageBuffer);

            byte[] bytes = _sizeOfIntArray;
            if (!BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage))
            {
                throw new UnreachableException();
            }

            await _messageBuffer.WriteAsync(bytes, cancellationToken);

            if (!BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id))
            {
                throw new UnreachableException();
            }

            await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);
            await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken);

            try
            {
                await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken);
                await _namedPipeServerStream.FlushAsync(cancellationToken);
            }
            finally
            {
                ResetBuffer(_messageBuffer);
                ResetBuffer(_serializationBuffer);
            }
        }
    }

    internal async Task<IRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        ResetBuffer(_messageBuffer);

        try
        {
            int headerBytesRead = 0;
            while (headerBytesRead < sizeof(int))
            {
                int bytesRead = await stream.ReadAsync(
                    _readBuffer.AsMemory(headerBytesRead, sizeof(int) - headerBytesRead),
                    cancellationToken);
                if (bytesRead == 0)
                {
                    return null;
                }

                headerBytesRead += bytesRead;
            }

            int payloadLength = BitConverter.ToInt32(_readBuffer, 0);
            if (payloadLength < sizeof(int) || payloadLength > MaximumFrameSize - sizeof(int))
            {
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The dotnet test pipe frame payload length {0} is invalid; expected {1} to {2} bytes.",
                    payloadLength,
                    sizeof(int),
                    MaximumFrameSize - sizeof(int)));
            }

            int remainingBytes = payloadLength;
            while (remainingBytes > 0)
            {
                int bytesRead = await stream.ReadAsync(
                    _readBuffer.AsMemory(0, Math.Min(_readBuffer.Length, remainingBytes)),
                    cancellationToken);
                if (bytesRead == 0)
                {
                    return null;
                }

                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(0, bytesRead), cancellationToken);
                remainingBytes -= bytesRead;
            }

            _messageBuffer.Position = 0;
            int serializerId = BitConverter.ToInt32(_messageBuffer.GetBuffer(), 0);
            INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(serializerId, _skipUnknownMessages);
            _messageBuffer.Position += sizeof(int);
            return (IRequest)requestNamedPipeSerializer.Deserialize(_messageBuffer);
        }
        finally
        {
            ResetBuffer(_messageBuffer);
        }
    }

    private static void ResetBuffer(MemoryStream buffer)
    {
        buffer.Position = 0;
        buffer.SetLength(0);
    }

    public static string GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return $"testingplatform.pipe.{name.Replace('\\', '.')}";
        }

        (string directory, bool isExplicitOverride) = ResolvePipeDirectory(
            Environment.GetEnvironmentVariable(PipeDirectoryEnvironmentVariable),
            Path.GetTempPath());
        directory = Path.GetFullPath(directory);

        if (isExplicitOverride)
        {
            EnsureDirectoryIsWritable(directory);
        }

        return GetUnixPipePath(name, directory);
    }

    internal static (string Directory, bool IsExplicitOverride) ResolvePipeDirectory(string? overrideDirectory, string? tempPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return (overrideDirectory, true);
        }

        return string.IsNullOrWhiteSpace(tempPath)
            ? ("/tmp", false)
            : (tempPath, false);
    }

    internal static string GetUnixPipePath(string name, string directory)
    {
        string path = Path.Combine(Path.GetFullPath(directory), name);
        EnsurePathLengthWithinLimit(path);
        return path;
    }

    internal static void EnsureDirectoryIsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probePath = Path.Combine(directory, $"testingplatform.probe.{Guid.NewGuid():N}");
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The test pipe directory '{directory}' is not writable. Set {PipeDirectoryEnvironmentVariable} to a writable directory. {ex.Message}",
                ex);
        }
    }

    internal static void EnsurePathLengthWithinLimit(string path)
    {
        int byteLength = Encoding.UTF8.GetByteCount(path);
        if (byteLength > MaxUnixDomainSocketPathLengthInBytes)
        {
            throw new InvalidOperationException(
                $"The test pipe path '{path}' is {byteLength} UTF-8 bytes; maximum is {MaxUnixDomainSocketPathLengthInBytes}. Set {PipeDirectoryEnvironmentVariable} to a shorter path.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (WasConnected)
            {
                // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
                // This is unexpected and we throw an exception.

                // To close gracefully we need to ensure that the client closed the stream line 103.
                if (!_loopTask!.Wait(TimeSpan.FromSeconds(90)))
                {
                    throw new InvalidOperationException(CliCommandStrings.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage);
                }
            }
        }
        finally
        {
            // Ensure we are still disposing the resouces correctly, even if _loopTask completes with
            // an exception, or if the task doesn't complete within the 90 seconds limit.
            _namedPipeServerStream.Dispose();
            _disposed = true;
        }
    }
}
