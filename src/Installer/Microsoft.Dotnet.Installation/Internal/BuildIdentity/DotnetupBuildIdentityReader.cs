// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>Reads the versioned identity record without loading or executing the containing artifact.</summary>
internal static class DotnetupBuildIdentityReader
{
    internal const int RecordLength = 96;

    private static bool HasMagic(ReadOnlySpan<byte> candidate)
    {
        return candidate.Length >= 16 &&
            BinaryPrimitives.ReadUInt64LittleEndian(candidate) == 0x505554454e544f44 &&
            BinaryPrimitives.ReadUInt64LittleEndian(candidate[8..]) == 0x004345522d44492d;
    }

    public static string Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The identity stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Position != 0)
        {
            throw new ArgumentException("The identity stream must be positioned at the start of the artifact.", nameof(stream));
        }

        var buffer = new byte[4096 + RecordLength - 1];
        var retained = 0;
        string? identity = null;
        while (true)
        {
            var read = stream.Read(buffer, retained, buffer.Length - retained);
            var available = retained + read;
            var scanLength = read == 0 ? available : Math.Max(0, available - RecordLength + 1);
            for (var offset = 0; offset < scanLength; offset++)
            {
                var candidate = buffer.AsSpan(offset, available - offset);
                if (!HasMagic(candidate))
                {
                    continue;
                }

                if (identity is not null)
                {
                    throw new InvalidDataException("The artifact contains multiple build identity records.");
                }

                identity = ReadRecord(candidate);
            }

            if (read == 0)
            {
                break;
            }

            retained = available - scanLength;
            buffer.AsSpan(scanLength, retained).CopyTo(buffer);
        }

        if (identity is null)
        {
            throw new InvalidDataException("The artifact has no build identity record.");
        }

        return identity;
    }

    internal static string ReadRecord(ReadOnlySpan<byte> record)
    {
        if (record.Length < RecordLength || !HasMagic(record))
        {
            throw new InvalidDataException("The build identity record is missing or truncated.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(record[16..]) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(record[20..]) != 64 ||
            !record.Slice(88, 8).SequenceEqual("END-ID\0\0"u8))
        {
            throw new InvalidDataException("The build identity record is malformed or unsupported.");
        }

        var payload = record.Slice(24, 64);
        foreach (var character in payload)
        {
            if (character is not (>= (byte)'0' and <= (byte)'9') and not (>= (byte)'a' and <= (byte)'f'))
            {
                throw new InvalidDataException("The build identity must contain 64 lowercase hexadecimal characters.");
            }
        }

        return System.Text.Encoding.ASCII.GetString(payload);
    }
}