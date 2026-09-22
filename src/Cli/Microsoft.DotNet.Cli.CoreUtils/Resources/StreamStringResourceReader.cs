// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Reads an indexed version-2 resource table directly from an owned seekable stream.
/// </summary>
internal sealed class StreamStringResourceReader : IStringResourceReader
{
    private const int MagicNumber = unchecked((int)0xBEEFCACE);
    private const int SupportedVersion = 2;

    private readonly BinaryReader _reader;
    private readonly long _resourceOffset;
    private readonly int[] _nameHashes;
    private readonly int[] _namePositions;
    private readonly int _typeCount;
    private readonly long _nameSectionOffset;
    private readonly long _dataSectionOffset;

    /// <summary>
    ///  Initializes a reader over the current position of <paramref name="stream"/> and takes
    ///  ownership of the stream.
    /// </summary>
    /// <param name="stream">The readable, seekable resource stream.</param>
    internal StreamStringResourceReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        BinaryReader? reader = null;

        try
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("The resource stream must be readable.", nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("The resource stream must be seekable.");
            }

            reader = new(stream, Encoding.UTF8);
            _reader = reader;
            _resourceOffset = stream.Position;

            if (_reader.ReadInt32() != MagicNumber)
            {
                throw new ArgumentException("The stream is not a valid .resources file.", nameof(stream));
            }

            int resourceManagerHeaderVersion = _reader.ReadInt32();
            int byteCountToSkip = _reader.ReadInt32();
            if (resourceManagerHeaderVersion < 0 || byteCountToSkip < 0)
            {
                throw new BadImageFormatException("The resource header is invalid.");
            }

            stream.Seek(byteCountToSkip, SeekOrigin.Current);
            int version = _reader.ReadInt32();
            if (version != SupportedVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported .resources format version {version}; only version {SupportedVersion} is supported.");
            }

            ResourceCount = _reader.ReadInt32();
            _typeCount = _reader.ReadInt32();
            if (ResourceCount < 0 || _typeCount < 0)
            {
                throw new BadImageFormatException("The resource or type count is invalid.");
            }

            for (int i = 0; i < _typeCount; i++)
            {
                SkipLengthPrefixedBytes();
            }

            long relativePosition = stream.Position - _resourceOffset;
            stream.Seek((8 - (relativePosition & 7)) & 7, SeekOrigin.Current);

            long indexByteLength = ((long)ResourceCount * 2 + 1) * sizeof(int);
            if (indexByteLength > stream.Length - stream.Position)
            {
                throw new BadImageFormatException("The resource index is truncated.");
            }

            _nameHashes = new int[ResourceCount];
            for (int i = 0; i < _nameHashes.Length; i++)
            {
                _nameHashes[i] = _reader.ReadInt32();
            }

            _namePositions = new int[ResourceCount];
            for (int i = 0; i < _namePositions.Length; i++)
            {
                int position = _reader.ReadInt32();
                if (position < 0)
                {
                    throw new BadImageFormatException("A resource name offset is invalid.");
                }

                _namePositions[i] = position;
            }

            int dataSectionOffset = _reader.ReadInt32();
            if (dataSectionOffset < 0)
            {
                throw new BadImageFormatException("The resource data section offset is invalid.");
            }

            _nameSectionOffset = stream.Position;
            _dataSectionOffset = checked(_resourceOffset + dataSectionOffset);
            if (_dataSectionOffset < _nameSectionOffset || _dataSectionOffset > stream.Length)
            {
                throw new BadImageFormatException("The resource data section is invalid.");
            }

            long nameSectionLength = _dataSectionOffset - _nameSectionOffset;
            for (int i = 0; i < _namePositions.Length; i++)
            {
                if (_namePositions[i] >= nameSectionLength)
                {
                    throw new BadImageFormatException("A resource name offset is invalid.");
                }
            }
        }
        catch (EndOfStreamException exception)
        {
            DisposeFailedConstruction(reader, stream);
            throw new BadImageFormatException("The resource stream is truncated.", exception);
        }
        catch
        {
            DisposeFailedConstruction(reader, stream);
            throw;
        }
    }

    /// <inheritdoc/>
    public int ResourceCount { get; }

    private static void DisposeFailedConstruction(BinaryReader? reader, Stream stream)
    {
        if (reader is null)
        {
            stream.Dispose();
        }
        else
        {
            reader.Dispose();
        }
    }

    /// <inheritdoc/>
    public ResourceTypeCode GetResourceTypeCode(int index)
    {
        int dataPosition = ReadDataPosition(index);
        _reader.BaseStream.Position = checked(_dataSectionOffset + dataPosition);
        return ReadResourceTypeCode();
    }

    /// <inheritdoc/>
    public string GetResourceName(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ResourceCount);
        _reader.BaseStream.Position = checked(_nameSectionOffset + _namePositions[index]);
        int byteLength = Read7BitEncodedInt32();
        if (byteLength < 0 || (byteLength & 1) != 0)
        {
            throw new BadImageFormatException("A resource name is invalid.");
        }

        if (byteLength > _reader.BaseStream.Length - _reader.BaseStream.Position)
        {
            throw new BadImageFormatException("A resource name is truncated.");
        }

        byte[] bytes = _reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength)
        {
            throw new BadImageFormatException("A resource name is truncated.");
        }

        return Encoding.Unicode.GetString(bytes);
    }

    /// <inheritdoc/>
    public string GetString(int index)
    {
        int dataPosition = ReadDataPosition(index);
        _reader.BaseStream.Position = checked(_dataSectionOffset + dataPosition);
        if (ReadResourceTypeCode() != ResourceTypeCode.String)
        {
            throw new InvalidOperationException("The resource is not a string.");
        }

        return _reader.ReadString();
    }

    /// <inheritdoc/>
    public StringResourceLookupKind Lookup(string name, out string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        int hash = ResourceNameHash.Compute(name);
        int index = Array.BinarySearch(_nameHashes, hash);
        if (index < 0)
        {
            value = null;
            return StringResourceLookupKind.Missing;
        }

        int first = index;
        while (first > 0 && _nameHashes[first - 1] == hash)
        {
            first--;
        }

        int last = index;
        while (last < ResourceCount - 1 && _nameHashes[last + 1] == hash)
        {
            last++;
        }

        for (int i = first; i <= last; i++)
        {
            _reader.BaseStream.Position = checked(_nameSectionOffset + _namePositions[i]);
            int byteLength = Read7BitEncodedInt32();
            if (byteLength < 0 || (byteLength & 1) != 0)
            {
                throw new BadImageFormatException("A resource name is invalid.");
            }

            if (byteLength != checked(name.Length * sizeof(char)) || !NameEquals(name))
            {
                continue;
            }

            int dataPosition = _reader.ReadInt32();
            if (dataPosition < 0 || dataPosition >= _reader.BaseStream.Length - _dataSectionOffset)
            {
                throw new BadImageFormatException("A resource data offset is invalid.");
            }

            _reader.BaseStream.Position = _dataSectionOffset + dataPosition;
            ResourceTypeCode typeCode = ReadResourceTypeCode();
            if (typeCode != ResourceTypeCode.String)
            {
                value = null;
                return StringResourceLookupKind.Missing;
            }

            value = _reader.ReadString();
            return StringResourceLookupKind.Found;
        }

        value = null;
        return StringResourceLookupKind.Missing;
    }

    /// <inheritdoc/>
    public void Dispose() => _reader.Dispose();

    private int ReadDataPosition(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ResourceCount);
        _reader.BaseStream.Position = checked(_nameSectionOffset + _namePositions[index]);
        SkipLengthPrefixedBytes();
        int dataPosition = _reader.ReadInt32();
        if (dataPosition < 0 || dataPosition >= _reader.BaseStream.Length - _dataSectionOffset)
        {
            throw new BadImageFormatException("A resource data offset is invalid.");
        }

        return dataPosition;
    }

    private ResourceTypeCode ReadResourceTypeCode()
    {
        int value = Read7BitEncodedInt32();
        return value < 0
            ? throw new BadImageFormatException("A resource type code is invalid.")
            : ResourceTypeCodeValidator.Validate(value, _typeCount);
    }

    private bool NameEquals(string name)
    {
        for (int i = 0; i < name.Length; i++)
        {
            if (_reader.ReadUInt16() != name[i])
            {
                return false;
            }
        }

        return true;
    }

    private void SkipLengthPrefixedBytes()
    {
        int length = Read7BitEncodedInt32();
        if (length < 0)
        {
            throw new BadImageFormatException("A length-prefixed value is invalid.");
        }

        _reader.BaseStream.Seek(length, SeekOrigin.Current);
    }

    private int Read7BitEncodedInt32()
    {
        uint result = 0;
        const int MaxBytesWithoutOverflow = 4;

        for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
        {
            byte value = _reader.ReadByte();
            result |= (uint)(value & 0x7F) << shift;
            if (value <= 0x7F)
            {
                return (int)result;
            }
        }

        byte last = _reader.ReadByte();
        if (last > 0x0F)
        {
            throw new BadImageFormatException("A 7-bit encoded integer is invalid.");
        }

        result |= (uint)last << (MaxBytesWithoutOverflow * 7);
        return (int)result;
    }

}
