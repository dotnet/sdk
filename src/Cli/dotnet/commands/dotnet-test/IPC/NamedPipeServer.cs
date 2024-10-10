// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System.Buffers;
using System.Globalization;
using System.IO.Pipes;

namespace Microsoft.DotNet.Tools.Test;

internal sealed class NamedPipeServer : NamedPipeBase, IServer
{
    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly CancellationToken _cancellationToken;
    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];
    private readonly bool _skipUnknownMessages;
    private Task _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        CancellationToken cancellationToken)
        : this(GetPipeName(name), callback, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        CancellationToken cancellationToken)
        : this(pipeNameDescription, callback, maxNumberOfServerInstances: 1, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        int maxNumberOfServerInstances,
        CancellationToken cancellationToken)
    {
        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        _callback = callback;
        _cancellationToken = cancellationToken;
    }

    public NamedPipeServer(
    PipeNameDescription pipeNameDescription,
    Func<IRequest, Task<IResponse>> callback,
    int maxNumberOfServerInstances,
    CancellationToken cancellationToken,
    bool skipUnknownMessages)
    {
        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
                catch (Exception ex)
                {
                    Environment.FailFast($"[NamedPipeServer] Unhandled exception:{Environment.NewLine}{ex}", ex);
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
#if NET
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken);
#else
            int currentReadBytes = await _namedPipeServerStream.ReadAsync(_readBuffer, currentReadIndex, _readBuffer.Length, cancellationToken);
#endif
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
                currentMessageSize = BitConverter.ToInt32(_readBuffer, 0);
                missingBytesToReadOfCurrentChunk = currentReadBytes - sizeof(int);
                missingBytesToReadOfWholeMessage = currentMessageSize;
                currentReadIndex = sizeof(int);
            }

            if (missingBytesToReadOfCurrentChunk > 0)
            {
                // We need to read the rest of the message
#if NET
                await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken);
#else
                await _messageBuffer.WriteAsync(_readBuffer, currentReadIndex, missingBytesToReadOfCurrentChunk, cancellationToken);
#endif
                missingBytesToReadOfWholeMessage -= missingBytesToReadOfCurrentChunk;
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
                IResponse response = await _callback(deserializedObject);

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
#if NET
                byte[] bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
                try
                {
                    BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage);

                    await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(sizeOfTheWholeMessage), 0, sizeof(int), cancellationToken);
#endif

                // Write the serializer id
#if NET
                bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
                try
                {
                    BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id);

                    await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
#else
                await _messageBuffer.WriteAsync(BitConverter.GetBytes(responseNamedPipeSerializer.Id), 0, sizeof(int), cancellationToken);
#endif

                // Write the message
#if NET
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken);
#else
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Position, cancellationToken);
#endif

                // Send the message
                try
                {
#if NET
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken);
#else
                    await _namedPipeServerStream.WriteAsync(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, cancellationToken);
#endif
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

        string directoryId = Path.Combine(Path.GetTempPath(), name);
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

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.

            // To close gracefully we need to ensure that the client closed the stream line 103.
            if (!_loopTask.Wait(TimeSpan.FromSeconds(90)))
            {
                throw new InvalidOperationException("InternalLoopAsyncDidNotExitSuccessfullyErrorMessage");
            }
        }

        _namedPipeServerStream.Dispose();
        PipeName.Dispose();

        _disposed = true;
    }

#if NET
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.

            try
            {
                // To close gracefully we need to ensure that the client closed the stream line 103.
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(90));
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("InternalLoopAsyncDidNotExitSuccessfullyErrorMessage");
            }
        }

        _namedPipeServerStream.Dispose();
        PipeName.Dispose();

        _disposed = true;
    }
#endif
}
