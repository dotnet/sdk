// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP
#nullable enable
using System.Buffers;
#endif

namespace Microsoft.DotNet.Tools.Test;

internal abstract class BaseSerializer
{
#if NETCOREAPP
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
        byte[] bytes = ArrayPool<byte>.Shared.Rent(size);
        try
        {
#if NET7_0_OR_GREATER
            stream.ReadExactly(bytes, 0, size);
#else
            _ = stream.Read(bytes, 0, size);
#endif
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

#else
    protected static string ReadString(Stream stream)
    {
        byte[] len = new byte[sizeof(int)];
        stream.Read(len, 0, len.Length);
        int length = BitConverter.ToInt32(len, 0);
        byte[] bytes = new byte[length];
        stream.Read(bytes, 0, bytes.Length);
        return Encoding.UTF8.GetString(bytes);
    }

    protected static string ReadStringValue(Stream stream, int size)
    {
        byte[] bytes = new byte[size];
        _ = stream.Read(bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(bytes);
    }

    protected static void WriteString(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] len = BitConverter.GetBytes(bytes.Length);
        stream.Write(len, 0, len.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteStringValue(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteStringSize(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] len = BitConverter.GetBytes(bytes.Length);
        stream.Write(len, 0, len.Length);
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        byte[] len = BitConverter.GetBytes(sizeInBytes);
        stream.Write(len, 0, len.Length);
    }

    protected static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static int ReadInt(Stream stream)
    {
        byte[] bytes = new byte[sizeof(int)];
        stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToInt32(bytes, 0);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteShort(Stream stream, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static long ReadLong(Stream stream)
    {
        byte[] bytes = new byte[sizeof(long)];
        stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToInt64(bytes, 0);
    }

    protected static ushort ReadShort(Stream stream)
    {
        byte[] bytes = new byte[sizeof(ushort)];
        stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToUInt16(bytes, 0);
    }

    protected static void WriteBool(Stream stream, bool value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static bool ReadBool(Stream stream)
    {
        byte[] bytes = new byte[sizeof(bool)];
        stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToBoolean(bytes, 0);
    }
#endif

    protected static byte ReadByte(Stream stream) => (byte)stream.ReadByte();

    protected static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

    protected static void WriteField(Stream stream, ushort id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteStringSize(stream, value);
        WriteStringValue(stream, value);
    }

    protected static void WriteField(Stream stream, ushort id, long? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteSize<long>(stream);
        WriteLong(stream, value.Value);
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

        WriteShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, byte? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
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

    private static int GetSize<T>() => typeof(T) switch
    {
        Type type when type == typeof(int) => sizeof(int),
        Type type when type == typeof(long) => sizeof(long),
        Type type when type == typeof(short) => sizeof(short),
        Type type when type == typeof(bool) => sizeof(bool),
        Type type when type == typeof(byte) => sizeof(byte),
        _ => 0,
    };

    public static bool IsNullOrEmpty<T>(T[]? list) => list is null || list.Length == 0;
}
