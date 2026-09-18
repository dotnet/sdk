// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>Reads local version/RID metadata without loading or executing the artifact. This is not authentication.</summary>
internal static class DotnetupVersionMetadataReader
{
    internal const int RecordLength = 256;
    internal const int PayloadLength = 224;

    public static string Format(string version, string runtimeIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        if (version.Concat(runtimeIdentifier).Any(character => character < '!' || character > '~' || character == '|') ||
            version.Length + runtimeIdentifier.Length + 1 >= PayloadLength)
        {
            throw new ArgumentException("Version metadata requires printable ASCII version/RID fields that fit in the record.");
        }

        return version + "|" + runtimeIdentifier;
    }

    private static bool HasMagic(ReadOnlySpan<byte> candidate)
    {
        return candidate.Length >= 16 &&
            BinaryPrimitives.ReadUInt64LittleEndian(candidate) == 0x505554454e544f44 &&
            BinaryPrimitives.ReadUInt64LittleEndian(candidate[8..]) == 0x004345522d52562d;
    }

    public static string Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The metadata stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Position != 0)
        {
            throw new ArgumentException("The metadata stream must be positioned at the start of the artifact.", nameof(stream));
        }

        var buffer = new byte[4096 + RecordLength - 1];
        var retained = 0;
        string? metadata = null;
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

                if (metadata is not null)
                {
                    throw new InvalidDataException("The artifact contains multiple version metadata records.");
                }

                metadata = ReadRecord(candidate);
            }

            if (read == 0)
            {
                break;
            }

            retained = available - scanLength;
            buffer.AsSpan(scanLength, retained).CopyTo(buffer);
        }

        if (metadata is null)
        {
            throw new InvalidDataException("The artifact has no version metadata record.");
        }

        return metadata;
    }

    internal static string ReadRecord(ReadOnlySpan<byte> record)
    {
        if (record.Length < RecordLength || !HasMagic(record))
        {
            throw new InvalidDataException("The version metadata record is missing or truncated.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(record[16..]) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(record[20..]) != 0 ||
            !record.Slice(248, 8).SequenceEqual("END-VER\0"u8))
        {
            throw new InvalidDataException("The version metadata record is malformed or unsupported.");
        }

        var payload = record.Slice(24, PayloadLength);
        var length = payload.IndexOf((byte)0);
        if (length < 3 || payload[length..].ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException("The version metadata payload is missing or has invalid padding.");
        }

        foreach (var character in payload[..length])
        {
            if (character < (byte)'!' || character > (byte)'~')
            {
                throw new InvalidDataException("Version metadata must contain printable ASCII fields.");
            }
        }

        var metadata = System.Text.Encoding.ASCII.GetString(payload[..length]);
        var separator = metadata.IndexOf('|');
        if (separator <= 0 || separator == metadata.Length - 1 || separator != metadata.LastIndexOf('|'))
        {
            throw new InvalidDataException("Version metadata must contain one version and one RID.");
        }

        return metadata;
    }
}