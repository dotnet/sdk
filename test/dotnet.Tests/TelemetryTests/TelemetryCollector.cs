// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.DotNet.Tests.TelemetryTests;

internal sealed class TelemetryCollector : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<CollectedEvent> _events = [];
    private readonly object _eventsLock = new();
    private readonly TcpListener _listener;
    private readonly Task _listenerTask;

    public TelemetryCollector()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Endpoint = new Uri($"http://127.0.0.1:{port}");
        _listenerTask = ListenAsync(_cancellation.Token);
    }

    public Uri Endpoint { get; }

    public IReadOnlyList<CollectedEvent> GetEvents()
    {
        lock (_eventsLock)
        {
            // OTLP delivery is at-least-once. Treat retries of the same event ID as one
            // logical telemetry event while preserving independently emitted events.
            return
            [
                .. _events
                    .GroupBy(
                        e => (e.Name, EventId: e.Attributes.GetValueOrDefault("event id")),
                        e => e)
                    .Select(group => group.First())
            ];
        }
    }

    public async Task<IReadOnlyList<CollectedEvent>> WaitForEventsAsync(
        Func<IReadOnlyList<CollectedEvent>, bool> condition,
        TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            IReadOnlyList<CollectedEvent> events = GetEvents();
            if (condition(events))
            {
                return events;
            }

            await Task.Delay(100);
        }

        IReadOnlyList<CollectedEvent> collectedEvents = GetEvents();
        throw new TimeoutException(
            $"The telemetry condition was not met. Collected events: {string.Join(", ", collectedEvents.Select(e => e.Name))}");
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        _listener.Stop();

        try
        {
            await _listenerTask;
        }
        catch (OperationCanceledException)
        {
        }

        _cancellation.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            using (client)
            {
                await HandleRequestAsync(client, cancellationToken);
            }
        }
    }

    private async Task HandleRequestAsync(TcpClient client, CancellationToken cancellationToken)
    {
        NetworkStream stream = client.GetStream();
        (string path, int contentLength) = await ReadHeadersAsync(stream, cancellationToken);
        byte[] body = new byte[contentLength];
        await ReadExactlyAsync(stream, body, cancellationToken);

        if (path == "/v1/traces")
        {
            IReadOnlyList<CollectedEvent> events = OtlpTraceParser.Parse(body);
            lock (_eventsLock)
            {
                _events.AddRange(events);
            }
        }

        const string response = "HTTP/1.1 200 OK\r\nContent-Type: application/x-protobuf\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
    }

    private static async Task<(string Path, int ContentLength)> ReadHeadersAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        List<byte> bytes = [];
        byte[] buffer = new byte[1];

        while (bytes.Count < 64 * 1024)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The OTLP request ended before its headers were complete.");
            }

            bytes.Add(buffer[0]);
            int count = bytes.Count;
            if (count >= 4
                && bytes[count - 4] == '\r'
                && bytes[count - 3] == '\n'
                && bytes[count - 2] == '\r'
                && bytes[count - 1] == '\n')
            {
                string headers = Encoding.ASCII.GetString([.. bytes]);
                string[] lines = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                string path = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
                string? contentLengthHeader = lines.FirstOrDefault(
                    line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));

                if (contentLengthHeader is null
                    || !int.TryParse(
                        contentLengthHeader.AsSpan("Content-Length:".Length).Trim(),
                        CultureInfo.InvariantCulture,
                        out int contentLength))
                {
                    throw new InvalidDataException("The OTLP request did not contain a valid Content-Length header.");
                }

                return (path, contentLength);
            }
        }

        throw new InvalidDataException("The OTLP request headers exceeded 64 KiB.");
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The OTLP request ended before its body was complete.");
            }

            offset += read;
        }
    }

    private static class OtlpTraceParser
    {
        public static IReadOnlyList<CollectedEvent> Parse(ReadOnlySpan<byte> payload)
        {
            List<CollectedEvent> events = [];
            var reader = new ProtobufReader(payload);
            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1 && wireType == 2)
                {
                    ParseResourceSpans(reader.ReadLengthDelimited(), events);
                }
                else
                {
                    reader.Skip(wireType);
                }
            }

            return events;
        }

        private static void ParseResourceSpans(ReadOnlySpan<byte> payload, List<CollectedEvent> events)
        {
            var reader = new ProtobufReader(payload);
            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 2 && wireType == 2)
                {
                    ParseScopeSpans(reader.ReadLengthDelimited(), events);
                }
                else
                {
                    reader.Skip(wireType);
                }
            }
        }

        private static void ParseScopeSpans(ReadOnlySpan<byte> payload, List<CollectedEvent> events)
        {
            var reader = new ProtobufReader(payload);
            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 2 && wireType == 2)
                {
                    ParseSpan(reader.ReadLengthDelimited(), events);
                }
                else
                {
                    reader.Skip(wireType);
                }
            }
        }

        private static void ParseSpan(ReadOnlySpan<byte> payload, List<CollectedEvent> events)
        {
            var reader = new ProtobufReader(payload);
            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 11 && wireType == 2)
                {
                    events.Add(ParseEvent(reader.ReadLengthDelimited()));
                }
                else
                {
                    reader.Skip(wireType);
                }
            }
        }

        private static CollectedEvent ParseEvent(ReadOnlySpan<byte> payload)
        {
            string? name = null;
            Dictionary<string, string?> attributes = [];
            var reader = new ProtobufReader(payload);

            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 2 when wireType == 2:
                        name = Encoding.UTF8.GetString(reader.ReadLengthDelimited());
                        break;
                    case 3 when wireType == 2:
                        ParseKeyValue(reader.ReadLengthDelimited(), attributes);
                        break;
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            return new CollectedEvent(
                name ?? throw new InvalidDataException("An OTLP span event did not contain a name."),
                attributes);
        }

        private static void ParseKeyValue(
            ReadOnlySpan<byte> payload,
            Dictionary<string, string?> attributes)
        {
            string? key = null;
            string? value = null;
            var reader = new ProtobufReader(payload);

            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1 when wireType == 2:
                        key = Encoding.UTF8.GetString(reader.ReadLengthDelimited());
                        break;
                    case 2 when wireType == 2:
                        value = ParseAnyValue(reader.ReadLengthDelimited());
                        break;
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            if (key is not null)
            {
                attributes[key] = value;
            }
        }

        private static string? ParseAnyValue(ReadOnlySpan<byte> payload)
        {
            var reader = new ProtobufReader(payload);
            while (reader.TryReadField(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1 when wireType == 2:
                        return Encoding.UTF8.GetString(reader.ReadLengthDelimited());
                    case 2 when wireType == 0:
                        return (reader.ReadVarint() != 0).ToString(CultureInfo.InvariantCulture);
                    case 3 when wireType == 0:
                        return unchecked((long)reader.ReadVarint()).ToString(CultureInfo.InvariantCulture);
                    case 4 when wireType == 1:
                        return BitConverter.Int64BitsToDouble(
                            unchecked((long)reader.ReadFixed64())).ToString(CultureInfo.InvariantCulture);
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            return null;
        }
    }

    private ref struct ProtobufReader
    {
        private readonly ReadOnlySpan<byte> _payload;
        private int _offset;

        public ProtobufReader(ReadOnlySpan<byte> payload)
        {
            _payload = payload;
        }

        public bool TryReadField(out int fieldNumber, out int wireType)
        {
            if (_offset == _payload.Length)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            ulong tag = ReadVarint();
            fieldNumber = checked((int)(tag >> 3));
            wireType = (int)(tag & 7);
            return true;
        }

        public ulong ReadVarint()
        {
            ulong value = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                EnsureAvailable(1);
                byte current = _payload[_offset++];
                value |= (ulong)(current & 0x7f) << shift;
                if ((current & 0x80) == 0)
                {
                    return value;
                }
            }

            throw new InvalidDataException("The OTLP payload contained an invalid varint.");
        }

        public ulong ReadFixed64()
        {
            EnsureAvailable(sizeof(ulong));
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_payload[_offset..]);
            _offset += sizeof(ulong);
            return value;
        }

        public ReadOnlySpan<byte> ReadLengthDelimited()
        {
            int length = checked((int)ReadVarint());
            EnsureAvailable(length);
            ReadOnlySpan<byte> value = _payload.Slice(_offset, length);
            _offset += length;
            return value;
        }

        public void Skip(int wireType)
        {
            switch (wireType)
            {
                case 0:
                    ReadVarint();
                    break;
                case 1:
                    EnsureAvailable(8);
                    _offset += 8;
                    break;
                case 2:
                    _ = ReadLengthDelimited();
                    break;
                case 5:
                    EnsureAvailable(4);
                    _offset += 4;
                    break;
                default:
                    throw new InvalidDataException($"The OTLP payload contained unsupported wire type {wireType}.");
            }
        }

        private void EnsureAvailable(int length)
        {
            if (length < 0 || _offset > _payload.Length - length)
            {
                throw new InvalidDataException("The OTLP payload ended unexpectedly.");
            }
        }
    }
}

internal sealed record CollectedEvent(
    string Name,
    IReadOnlyDictionary<string, string?> Attributes);
