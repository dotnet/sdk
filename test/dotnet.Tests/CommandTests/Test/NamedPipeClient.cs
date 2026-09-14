// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace dotnet.Tests.CommandTests.Test;

using System.Buffers;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class NamedPipeClient : NamedPipeBase
{
    private readonly NamedPipeClientStream _namedPipeClientStream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly MemoryStream _serializationBuffer = new();
    private readonly MemoryStream _messageBuffer = new();
    private readonly byte[] _readBuffer = new byte[250000];

    private bool _disposed;

    public NamedPipeClient(string name)
    {
        _namedPipeClientStream = new(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        PipeName = name;
    }

    public string PipeName { get; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
        => await _namedPipeClientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

    public async Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
       where TRequest : IRequest
       where TResponse : IResponse
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(typeof(TRequest));

            // Ask to serialize the body
            _serializationBuffer.Position = 0;
            requestNamedPipeSerializer.Serialize(request, _serializationBuffer);

            // Write the message size
            _messageBuffer.Position = 0;

            // The length of the message is the size of the message plus one byte to store the serializer id
            // Space for the message
            int sizeOfTheWholeMessage = (int)_serializationBuffer.Position;

            // Space for the serializer id
            sizeOfTheWholeMessage += sizeof(int);

            // Write the message size
            byte[] bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                BitConverter.TryWriteBytes(bytes, sizeOfTheWholeMessage);
                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }

            // Write the serializer id
            bytes = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                BitConverter.TryWriteBytes(bytes, requestNamedPipeSerializer.Id);
                await _messageBuffer.WriteAsync(bytes.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }

            try
            {
                // Write the message
                await _messageBuffer.WriteAsync(_serializationBuffer.GetBuffer().AsMemory(0, (int)_serializationBuffer.Position), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Reset the serialization buffer
                _serializationBuffer.Position = 0;
            }

            // Send the message
            try
            {
                await _namedPipeClientStream.WriteAsync(_messageBuffer.GetBuffer().AsMemory(0, (int)_messageBuffer.Position), cancellationToken).ConfigureAwait(false);
                await _namedPipeClientStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _namedPipeClientStream.WaitForPipeDrain();
                }
            }
            finally
            {
                // Reset the buffers
                _messageBuffer.Position = 0;
                _serializationBuffer.Position = 0;
            }

            // Read the response
            int currentMessageSize = 0;
            int missingBytesToReadOfWholeMessage = 0;
            while (true)
            {
                int currentReadIndex = 0;
                int currentReadBytes = await _namedPipeClientStream.ReadAsync(_readBuffer.AsMemory(currentReadIndex, _readBuffer.Length), cancellationToken).ConfigureAwait(false);

                if (currentReadBytes == 0)
                {
                    // We are reading a message response.
                    // If we cannot get a response, there is no way we can recover and continue executing.
                    // This can happen if the other processes gets killed or crashes while while it's sending the response.
                    // This is especially important for 'dotnet test', where the user can simply kill the dotnet.exe process themselves.
                    // In that case, we want the MTP process to also die.
                    Environment.FailFast("[NamedPipeClient] Connection lost with the other side.");
                }

                // Reset the current chunk size
                int missingBytesToReadOfCurrentChunk = currentReadBytes;

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
                    await _messageBuffer.WriteAsync(_readBuffer.AsMemory(currentReadIndex, missingBytesToReadOfCurrentChunk), cancellationToken).ConfigureAwait(false);
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
                    _messageBuffer.Position += sizeof(int); // Skip the serializer id
                    INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(serializerId);

                    // Deserialize the message
                    try
                    {
                        return (TResponse)responseNamedPipeSerializer.Deserialize(_messageBuffer);
                    }
                    finally
                    {
                        // Reset the message buffer
                        _messageBuffer.Position = 0;
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _serializationBuffer.Dispose();
        _messageBuffer.Dispose();
        _namedPipeClientStream.Dispose();
        _disposed = true;
    }
}
