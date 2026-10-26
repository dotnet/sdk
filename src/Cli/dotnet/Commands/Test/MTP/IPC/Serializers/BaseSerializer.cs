// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal abstract class BaseSerializer
{
    private const int MaximumCollectionLength = 1_000_000;

    protected static string ReadString(Stream stream)
    {
        Span<byte> len = stackalloc byte[sizeof(int)];
        stream.ReadExactly(len);
        int stringLen = BitConverter.ToInt32(len);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringLen);
        try
        {
            stream.ReadExactly(bytes, 0, stringLen);
            return Encoding.UTF8.GetString(bytes, 0, stringLen);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static string ReadStringValue(Stream stream, int size)
    {
        ValidatePayloadSize(stream, size);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            stream.ReadExactly(bytes, 0, size);
            return Encoding.UTF8.GetString(bytes, 0, size);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteString(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringutf8TotalBytes);
        try
        {
            Span<byte> len = stackalloc byte[sizeof(int)];
            BitConverter.TryWriteBytes(len, stringutf8TotalBytes);
            stream.Write(len);

            Encoding.UTF8.GetBytes(str, bytes);
            stream.Write(bytes, 0, stringutf8TotalBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteStringValue(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringutf8TotalBytes);
        try
        {
            Encoding.UTF8.GetBytes(str, bytes);
            stream.Write(bytes, 0, stringutf8TotalBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteStringSize(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        Span<byte> len = stackalloc byte[sizeof(int)];
        if (BitConverter.TryWriteBytes(len, stringutf8TotalBytes))
        {
            stream.Write(len);
        }
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        Span<byte> len = stackalloc byte[sizeof(int)];

        if (BitConverter.TryWriteBytes(len, sizeInBytes))
        {
            stream.Write(len);
        }
    }

    protected static void WriteInt(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, value);

        stream.Write(bytes);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BitConverter.TryWriteBytes(bytes, value);

        stream.Write(bytes);
    }

    protected static void WriteUShort(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(bytes, value);

        stream.Write(bytes);
    }

    protected static void WriteBool(Stream stream, bool value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        BitConverter.TryWriteBytes(bytes, value);

        stream.Write(bytes);
    }

    protected static int ReadInt(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt32(bytes);
    }

    protected static long ReadLong(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt64(bytes);
    }

    protected static ushort ReadUShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        stream.ReadExactly(bytes);
        return BitConverter.ToUInt16(bytes);
    }

    protected static bool ReadBool(Stream stream)
        => ReadByte(stream) != 0;

    protected static byte ReadByte(Stream stream)
        => stream.ReadByte() is int value and not -1
            ? (byte)value
            : throw new EndOfStreamException();

    protected static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

    protected static void WriteField(Stream stream, ushort id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteStringSize(stream, value);
        WriteStringValue(stream, value);
    }

    protected static void WriteField(Stream stream, ushort id, long? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<long>(stream);
        WriteLong(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, int? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<int>(stream);
        WriteInt(stream, value.Value);
    }

    protected static void WriteField(Stream stream, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, byte? value)
    {
        if (value is null)
        {
            return;
        }

        WriteByte(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, bool? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, byte? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<byte>(stream);
        WriteByte(stream, value.Value);
    }

    protected static void SetPosition(Stream stream, long position) => stream.Position = position;

    protected static void WriteAtPosition(Stream stream, int value, long position)
    {
        long currentPosition = stream.Position;
        SetPosition(stream, position);
        WriteInt(stream, value);
        SetPosition(stream, currentPosition);
    }

    protected static void ReadFields(Stream stream, Func<ushort, int, bool> tryReadField)
    {
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);
            if (fieldSize < 0)
            {
                throw new InvalidDataException($"Field {fieldId} has a negative size.");
            }

            long fieldEnd = checked(stream.Position + fieldSize);
            if (fieldEnd > stream.Length)
            {
                throw new EndOfStreamException($"Field {fieldId} extends beyond the end of the stream.");
            }

            if (!tryReadField(fieldId, fieldSize))
            {
                SetPosition(stream, fieldEnd);
                continue;
            }

            if (stream.Position > fieldEnd)
            {
                throw new InvalidDataException($"Field {fieldId} consumed more data than its declared size.");
            }

            SetPosition(stream, fieldEnd);
        }
    }

    protected static T ReadFieldPayload<T>(
        Stream stream,
        int fieldSize,
        Func<Stream, T> readPayload)
    {
        ValidatePayloadSize(stream, fieldSize);
        using var payloadStream = new BoundedReadStream(stream, fieldSize);
        return readPayload(payloadStream);
    }

    protected static int ReadCollectionLength(Stream stream, int minimumBytesPerItem)
    {
        int length = ReadInt(stream);
        long remainingBytes = stream.Length - stream.Position;
        if (length < 0 ||
            length > MaximumCollectionLength ||
            minimumBytesPerItem <= 0 ||
            length > remainingBytes / minimumBytesPerItem)
        {
            throw new InvalidDataException($"Collection length {length} is invalid for the remaining payload.");
        }

        return length;
    }

    private static void ValidatePayloadSize(Stream stream, int size)
    {
        if (size < 0 || size > stream.Length - stream.Position)
        {
            throw new InvalidDataException($"Payload size {size} is invalid for the remaining stream.");
        }
    }

    private sealed class BoundedReadStream(Stream stream, long length) : Stream
    {
        private readonly long _start = stream.Position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; } = length;

        public override long Position
        {
            get => stream.Position - _start;
            set
            {
                if (value < 0 || value > Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                stream.Position = _start + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = (int)Math.Min(count, Length - Position);
            return bytesToRead == 0 ? 0 : stream.Read(buffer, offset, bytesToRead);
        }

        public override int Read(Span<byte> buffer)
        {
            int bytesToRead = (int)Math.Min(buffer.Length, Length - Position);
            return bytesToRead == 0 ? 0 : stream.Read(buffer[..bytesToRead]);
        }

        public override int ReadByte()
            => Position == Length ? -1 : stream.ReadByte();

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = position;
            return Position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    protected static void WriteListPayload<T>(Stream stream, ushort fieldId, T[]? list, Action<Stream, T> writeItem)
    {
        if (list is null || list.Length == 0)
        {
            return;
        }

        WriteUShort(stream, fieldId);
        WriteInt(stream, 0);

        long before = stream.Position;
        WriteInt(stream, list.Length);
        foreach (T item in list)
        {
            writeItem(stream, item);
        }

        WriteAtPosition(stream, checked((int)(stream.Position - before)), before - sizeof(int));
    }

    private const ushort ExecutionScopedExecutionIdFieldId = 1;
    private const ushort ExecutionScopedInstanceIdFieldId = 2;

    protected static bool TryReadExecutionScopedField(Stream stream, ushort fieldId, int fieldSize, ref string? executionId, ref string? instanceId)
    {
        switch (fieldId)
        {
            case ExecutionScopedExecutionIdFieldId:
                executionId = ReadStringValue(stream, fieldSize);
                return true;

            case ExecutionScopedInstanceIdFieldId:
                instanceId = ReadStringValue(stream, fieldSize);
                return true;

            default:
                return false;
        }
    }

    protected static void WriteExecutionScopedHeader(Stream stream, string? executionId, string? instanceId, ushort payloadFieldCount)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, (ushort)((executionId is null ? 0 : 1) + (instanceId is null ? 0 : 1) + payloadFieldCount));

        WriteField(stream, ExecutionScopedExecutionIdFieldId, executionId);
        WriteField(stream, ExecutionScopedInstanceIdFieldId, instanceId);
    }

    private static int GetSize<T>() => typeof(T) switch
    {
        Type type when type == typeof(int) => sizeof(int),
        Type type when type == typeof(long) => sizeof(long),
        Type type when type == typeof(short) => sizeof(short),
        Type type when type == typeof(ushort) => sizeof(ushort),
        Type type when type == typeof(bool) => sizeof(bool),
        Type type when type == typeof(byte) => sizeof(byte),
        _ => throw new InvalidOperationException($"Unsupported primitive type '{typeof(T)}'."),
    };

    public static bool IsNullOrEmpty<T>(T[]? list) => list is null || list.Length == 0;
}
