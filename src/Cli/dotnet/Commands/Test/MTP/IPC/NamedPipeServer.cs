// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class NamedPipeServer : NamedPipeBase
{
    private readonly Func<NamedPipeServer, IRequest, Task<IResponse>> _callback;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly CancellationToken _cancellationToken;
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
    private readonly byte[] _sizeOfIntArray = new byte[sizeof(int)];
    private readonly bool _skipUnknownMessages;
    private Task _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<NamedPipeServer, IRequest, Task<IResponse>> callback,
        int maxNumberOfServerInstances,
        CancellationToken cancellationToken,
        bool skipUnknownMessages)
    {
        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _callback = callback;
        _cancellationToken = cancellationToken;
        _skipUnknownMessages = skipUnknownMessages;
    }

    public PipeNameDescription PipeName { get; private set; }

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
        int currentMessageSize = 0;
        int missingBytesToReadOfWholeMessage = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            int missingBytesToReadOfCurrentChunk = 0;
            int currentReadIndex = 0;
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken);
            if (currentReadBytes == 0)
            {
                // The client has disconnected
                return;
            }

            // Reset the current chunk size
            missingBytesToReadOfCurrentChunk = currentReadBytes;

            // If currentRequestSize is 0, we need to read the message size
            if (currentMessageSize == 0)
            {
                // We need to read the message size, first 4 bytes
                if (currentReadBytes < sizeof(int))
                {
                    throw new UnreachableException(CliCommandStrings.DotnetTestPipeIncompleteSize);
                }

                currentMessageSize = BitConverter.ToInt32(_readBuffer, 0);
                missingBytesToReadOfCurrentChunk = currentReadBytes - sizeof(int);
                missingBytesToReadOfWholeMessage = currentMessageSize;
                currentReadIndex = sizeof(int);
            }

            if (missingBytesToReadOfCurrentChunk > 0)
            {
                // We need to read the rest of the message
                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken);
                missingBytesToReadOfWholeMessage -= missingBytesToReadOfCurrentChunk;
            }

            if (missingBytesToReadOfWholeMessage < 0)
            {
                throw new UnreachableException(CliCommandStrings.DotnetTestPipeOverlapping);
            }

            // If we have read all the message, we can deserialize it
            if (missingBytesToReadOfWholeMessage == 0)
            {
                // Deserialize the message
                _messageBuffer.Position = 0;

                // Get the serializer id
                int serializerId = BitConverter.ToInt32(_messageBuffer.GetBuffer(), 0);

                // Get the serializer
                INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(serializerId, _skipUnknownMessages);

                // Deserialize the message
                _messageBuffer.Position += sizeof(int); // Skip the serializer id
                var deserializedObject = (IRequest)requestNamedPipeSerializer.Deserialize(_messageBuffer);

                // Call the callback
                IResponse response = await _callback(this, deserializedObject);

                // Write the message size
                _messageBuffer.Position = 0;

                // Get the response serializer
                INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());

                // Serialize the response
                responseNamedPipeSerializer.Serialize(response, _serializationBuffer);

                // The length of the message is the size of the message plus one byte to store the serializer id
                // Space for the message
                int sizeOfTheWholeMessage = (int)_serializationBuffer.Position;

                // Space for the serializer id
                sizeOfTheWholeMessage += sizeof(int);

                // Write the message size
                byte[] bytes = _sizeOfIntArray;
                BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage);
                await _messageBuffer.WriteAsync(bytes, cancellationToken);

                // Write the serializer id
                bytes = _sizeOfIntArray;
                BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id);

                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);

                // Write the message
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken);

                // Send the message
                try
                {
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken);
                    await _namedPipeServerStream.FlushAsync(cancellationToken);
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        _namedPipeServerStream.WaitForPipeDrain();
                    }
                }
                finally
                {
                    // Reset the buffers
                    _messageBuffer.Position = 0;
                    _serializationBuffer.Position = 0;
                }

                // Reset the control variables
                currentMessageSize = 0;
                missingBytesToReadOfWholeMessage = 0;
            }
        }
    }

    public static PipeNameDescription GetPipeName(string name)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}", false);
        }

        string directoryId = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
        Directory.CreateDirectory(directoryId);
        return new PipeNameDescription(
            !Directory.Exists(directoryId)
                ? throw new DirectoryNotFoundException(string.Format(
                    CultureInfo.InvariantCulture,
                    $"Directory: {directoryId} doesn't exist.",
                    directoryId))
                : Path.Combine(directoryId, ".p"), true);
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
                if (!_loopTask.Wait(TimeSpan.FromSeconds(90)))
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
            PipeName.Dispose();

            _disposed = true;
        }
    }
}
