// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Buffers;

namespace Microsoft.DotNet.Tools.Test;

internal abstract class BaseSerializer
{
    protected static string ReadString(Stream stream)
    {
        Span<byte> len = stackalloc byte[4];
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
            Span<byte> len = stackalloc byte[4];
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

    protected static void WriteSize(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        Span<byte> len = stackalloc byte[4];

        if (BitConverter.TryWriteBytes(len, stringutf8TotalBytes))
        {
            stream.Write(len);
        }
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        Span<byte> len = stackalloc byte[4];

        if (BitConverter.TryWriteBytes(len, sizeInBytes))
        {
            stream.Write(len);
        }
    }

    private static int GetSize<T>()
        where T : struct
    {
        int sizeInBytes = 0;

        if (typeof(T) == typeof(int))
        {
            sizeInBytes = sizeof(int);
        }
        else if (typeof(T) == typeof(long))
        {
            sizeInBytes = sizeof(long);
        }
        else if (typeof(T) == typeof(short))
        {
            sizeInBytes = sizeof(short);
        }
        else if (typeof(T) == typeof(bool))
        {
            sizeInBytes = sizeof(bool);
        }

        return sizeInBytes;
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

    protected static void WriteShort(Stream stream, short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
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

    protected static short ReadShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt16(bytes);
    }

    protected static bool ReadBool(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        stream.ReadExactly(bytes);
        return BitConverter.ToBoolean(bytes);
    }

    protected static void WriteField(Stream stream, short id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteSize(stream, value);
        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, short id, bool value)
    {
        WriteShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value);
    }

    protected static void SetPosition(Stream stream, long position)
    {
        stream.Position = position;
    }

    protected static void WriteAtPosition(Stream stream, int value, long position)
    {
        var currentPosition = stream.Position;
        SetPosition(stream, position);
        WriteInt(stream, value);
        SetPosition(stream, currentPosition);
    }
}
