// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Buffers;

namespace Microsoft.DotNet.Tools.Test;

internal abstract class BaseSerializer
{
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

    protected static void WriteShort(Stream stream, ushort value)
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

    protected static ushort ReadShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        stream.ReadExactly(bytes);
        return BitConverter.ToUInt16(bytes);
    }

    protected static bool ReadBool(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        stream.ReadExactly(bytes);
        return BitConverter.ToBoolean(bytes);
    }

    protected static void WriteField(Stream stream, ushort id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteStringSize(stream, value);
        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, ushort id, bool value)
    {
        WriteShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value);
    }

    protected static void SetPosition(Stream stream, long position) => stream.Position = position;

    protected static void WriteAtPosition(Stream stream, int value, long position)
    {
        var currentPosition = stream.Position;
        SetPosition(stream, position);
        WriteInt(stream, value);
        SetPosition(stream, currentPosition);
    }

    private static int GetSize<T>()
            where T : struct => typeof(T) == typeof(int)
                ? sizeof(int)
                : typeof(T) == typeof(long)
                ? sizeof(long)
                : typeof(T) == typeof(short) ? sizeof(short) : typeof(T) == typeof(bool) ? sizeof(bool) : 0;
}
