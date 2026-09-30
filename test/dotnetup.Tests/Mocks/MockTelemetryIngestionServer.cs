// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

internal sealed class MockTelemetryIngestionServer : IDisposable
{
    private const int MaxHeaderBytes = 32 * 1024;
    private const int MaxBodyBytes = 4 * 1024 * 1024;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource _requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _serverTask;

    public MockTelemetryIngestionServer()
    {
        _listener.Server.ExclusiveAddressUse = true;
        _listener.Start(backlog: 1);
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        IngestionEndpoint = $"http://127.0.0.1:{port}/";
        _serverTask = ServeRequestsAsync();
    }

    public string IngestionEndpoint { get; }

    public Task WaitForRequestAsync(TimeSpan timeout) => _requestReceived.Task.WaitAsync(timeout);

    public void Dispose()
    {
        _cancellation.Cancel();
        _listener.Stop();
        _cancellation.Dispose();
    }

    private async Task ServeRequestsAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                if (client.Client.RemoteEndPoint is not IPEndPoint { Address: var address }
                    || !IPAddress.IsLoopback(address))
                {
                    continue;
                }

                await ServeRequestAsync(client);
                _requestReceived.TrySetResult();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_cancellation.IsCancellationRequested)
        {
        }
    }

    private static async Task ServeRequestAsync(TcpClient client)
    {
        using NetworkStream stream = client.GetStream();
        if (await ReadHeadersAsync(stream))
        {
            await ReadChunkedBodyAsync(stream);
        }

        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response);
        await stream.FlushAsync();
        client.Client.Shutdown(SocketShutdown.Send);
    }

    private static async Task<bool> ReadHeadersAsync(NetworkStream stream)
    {
        string requestLine = await ReadLineAsync(stream, 1024);
        if (!requestLine.Equals("POST /v2.1/track HTTP/1.1", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected telemetry request line: '{requestLine}'.");
        }

        bool chunked = false;
        int bytesRead = Encoding.ASCII.GetByteCount(requestLine) + 2;
        while (true)
        {
            string line = await ReadLineAsync(stream, MaxHeaderBytes - bytesRead);
            bytesRead += Encoding.ASCII.GetByteCount(line) + 2;
            if (line.Length == 0)
            {
                return chunked;
            }

            chunked |= line.Equals("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task ReadChunkedBodyAsync(NetworkStream stream)
    {
        int totalBytes = 0;
        while (true)
        {
            string sizeLine = await ReadLineAsync(stream, 128);
            int separator = sizeLine.IndexOf(';');
            string sizeText = separator < 0 ? sizeLine : sizeLine[..separator];
            int size = int.Parse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (size == 0)
            {
                await ReadLineAsync(stream, MaxHeaderBytes);
                return;
            }

            totalBytes = checked(totalBytes + size);
            if (totalBytes > MaxBodyBytes)
            {
                throw new InvalidDataException($"Telemetry request exceeded {MaxBodyBytes} bytes.");
            }

            await ReadExactlyAsync(stream, size);
            await ReadLineAsync(stream, 2);
        }
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream, int maxBytes)
    {
        var bytes = new List<byte>();
        while (true)
        {
            int value = await ReadByteAsync(stream);
            if (value == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            }

            if (bytes.Count >= maxBytes)
            {
                throw new InvalidDataException($"HTTP line exceeded {maxBytes} bytes.");
            }

            bytes.Add((byte)value);
        }
    }

    private static async Task<int> ReadByteAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[1];
        int read = await stream.ReadAsync(buffer);
        return read == 0 ? throw new EndOfStreamException() : buffer[0];
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, int length)
    {
        byte[] buffer = new byte[Math.Min(length, 8192)];
        while (length > 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(length, buffer.Length)));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            length -= read;
        }
    }
}