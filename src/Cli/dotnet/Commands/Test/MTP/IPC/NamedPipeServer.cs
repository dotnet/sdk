// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class NamedPipeServer : NamedPipeBase
{
    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    private readonly Func<NamedPipeServer, IRequest, IResponse> _callback;
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
        Func<NamedPipeServer, IRequest, IResponse> callback,
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
        _loopTask = Task.Factory.StartNew(
            () =>
            {
                try
                {
                    // This is intentionally not running on thread pool and is not async (to avoid getting back to TP unintentionally)
                    // Running this on ThreadPool is problematic as it means we might end up keep TP threads busy doing synchronous
                    // I/O work (via TerminalTestReporter to write to console, or via PipeStream.WriteAsync which appears to block on kernel WriteFile)
                    // which reduces the ability for TP to take more important work like starting new test applications.
                    // So, we intentionally run this on a dedicated thread and run synchronously.
                    // It also goes the other way around. If many TP threads are busy with lock contention in Process.Start,
                    // we want to still be able to process messages received from the MTP test apps.
                    InternalLoop(_cancellationToken);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                {
                    // We are being cancelled, so we don't need to wait anymore
                    return;
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// 4 bytes = message size
    /// ------- Payload -------
    /// 4 bytes = serializer id
    /// x bytes = object buffer.
    /// </summary>
    private void InternalLoop(CancellationToken cancellationToken)
    {
        // This is an indicator when reading from the pipe whether we are at the start of a new message (i.e, we should read 4 bytes as message size)
        // Note that the implementation assumes no overlapping messages in the pipe.
        // The flow goes like:
        // 1. MTP sends a request (and acquires lock).
        // 2. SDK reads the request.
        // 3. SDK sends a response.
        // 4. MTP reads the response (and releases lock).
        // This means that no two requests can be in the pipe at the same time.
        bool isStartOfNewMessage = true;
        int remainingBytesToReadOfWholeMessage = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // If we are at the start of a new message, we need to read at least the message size.
            int currentReadBytes = isStartOfNewMessage
                ? _namedPipeServerStream.ReadAtLeast(_readBuffer, minimumBytes: sizeof(int), throwOnEndOfStream: false)
                : _namedPipeServerStream.Read(_readBuffer);

            if (currentReadBytes == 0 || (isStartOfNewMessage && currentReadBytes < sizeof(int)))
            {
                // The client has disconnected
                return;
            }

            // The local remainingBytesToProcess tracks the remaining bytes of what we have read from the pipe but not yet processed.
            // At the beginning here, it contains everything we have read from the pipe.
            // As we are processing the data in it, we continue to slice it.
            Memory<byte> remainingBytesToProcess = _readBuffer.AsMemory(0, currentReadBytes);

            // If the current read is the start of a new message, we need to read the message size first.
            if (isStartOfNewMessage)
            {
                // We need to read the message size, first 4 bytes
                remainingBytesToReadOfWholeMessage = BitConverter.ToInt32(remainingBytesToProcess.Span);

                // Now that we have read the size, we slice the remainingBytesToProcess.
                remainingBytesToProcess = remainingBytesToProcess.Slice(sizeof(int));

                // Now that we have read the size, we are no longer at the start of a new message.
                // If the current chunk ended up to be the full message, we will set this back to true later.
                isStartOfNewMessage = false;
            }

            // We read the rest of the message.
            // Note that this assumes that no messages are overlapping in the pipe.
            if (remainingBytesToProcess.Length > 0)
            {
                // We need to read the rest of the message
                _messageBuffer.Write(remainingBytesToProcess.Span);
                remainingBytesToReadOfWholeMessage -= remainingBytesToProcess.Length;

                // At this point, we have read everything in the remainingBytesToProcess.
                // Note that while remainingBytesToProcess isn't accessed after this point, we still maintain the
                // invariant that it tracks what we have read from the pipe but not yet processed.
                remainingBytesToProcess = Memory<byte>.Empty;
            }

            if (remainingBytesToReadOfWholeMessage < 0)
            {
                throw new UnreachableException(CliCommandStrings.DotnetTestPipeOverlapping);
            }

            // If we have read all the message, we can deserialize it
            if (remainingBytesToReadOfWholeMessage == 0)
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
                IResponse response = _callback(this, deserializedObject);

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
                if (!BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage))
                {
                    throw new UnreachableException();
                }

                _messageBuffer.Write(bytes);

                // Write the serializer id
                bytes = _sizeOfIntArray;
                if (!BitConverter.TryWriteBytes(bytes, responseNamedPipeSerializer.Id))
                {
                    throw new UnreachableException();
                }

                _messageBuffer.Write(bytes.AsSpan(0, sizeof(int)));

                // Write the message
                _messageBuffer.Write(_serializationBuffer.GetBuffer().AsSpan(0, (int)_serializationBuffer.Position));

                // Send the message
                try
                {
                    _namedPipeServerStream.Write(_messageBuffer.GetBuffer().AsSpan(0, (int)_messageBuffer.Position));
                    _namedPipeServerStream.Flush();
                }
                finally
                {
                    // Reset the buffers
                    _messageBuffer.Position = 0;
                    _serializationBuffer.Position = 0;
                }

                // Reset the control variables
                isStartOfNewMessage = true;
                remainingBytesToReadOfWholeMessage = 0;
            }
        }
    }

    public static string GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return $"testingplatform.pipe.{name.Replace('\\', '.')}";
        }

        // Similar to https://github.com/dotnet/roslyn/blob/99bf83c7bc52fa1ff27cf792db38755d5767c004/src/Compilers/Shared/NamedPipeUtil.cs#L26-L42
        return Path.Combine("/tmp", name);
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
