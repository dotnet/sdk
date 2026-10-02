// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Reads the default binary <c>.resources</c> format (version 2) directly over a caller-owned
///  <see cref="ReadOnlyMemory{T}"/> or a memory-mapped file, without allocating strings or byte
///  arrays.
/// </summary>
/// <remarks>
///  <para>
///   Unlike <see cref="System.Resources.ResourceReader"/>, this type never materializes managed
///   values: name lookups return an index, type, and byte length; value bytes are copied into a
///   caller-supplied <see cref="Span{T}"/> or <see cref="Stream"/>. The name-hash and name-position
///   index tables are read in place from the memory, so construction, lookup, and reads allocate
///   nothing. This mirrors the runtime's <see cref="System.IO.UnmanagedMemoryStream"/> fast path but
///   works over any <see cref="ReadOnlyMemory{T}"/> - a managed array or a memory-mapped view.
///  </para>
///  <para>
///   The reader is immutable over its memory, so lookups are pure and require no locking. Only string
///   resources, the primitive types, <c>byte[]</c>, and <see cref="Stream"/> values are exposed as
///   raw bytes; serialized user types are reported by type code and type name only. No value is ever
///   decoded or deserialized, so the reader triggers no reflection and is trim- and AOT-safe.
///  </para>
///  <para>
///   Values are presented as content only - the on-disk length prefixes of strings, byte arrays, and
///   streams are stripped, so <see cref="ResourceLocation.ByteLength"/> and
///   <see cref="TryGetResourceData(int, Span{byte}, out int)"/> yield exactly the value bytes.
///  </para>
/// </remarks>
internal sealed class RawResourceReader : DisposableBase, IStringResourceReader
{
    // On-disk layout of a default-format (version 2) .resources file. All integers are little-endian
    // and "7-bit int" is a LEB128-style length prefix. The reader validates and indexes this in place
    // over _resources; it never copies or decodes the data.
    //
    //   ResourceManager header
    //     int32   magic (0xBEEFCACE)
    //     int32   ResourceManager header version
    //     int32   byteCountToSkip
    //     byte[]  reader / resource-set type names   (skipped: byteCountToSkip bytes)
    //   ------------------------------------------------------------------------------------
    //   RuntimeResourceSet header
    //     int32   version (== 2)
    //     int32   numResources
    //     int32   numTypes
    //     entry[] type-name table: numTypes x [7-bit len][UTF-8]     (_typeNamesOffset)
    //   ------------------------------------------------------------------------------------
    //     pad     to the next 8-byte boundary ('P','A','D' filler)
    //   ------------------------------------------------------------------------------------
    //   Index
    //     int32[] nameHashes[numResources]     (sorted ascending)    (_nameHashesOffset)
    //     int32[] namePositions[numResources]  (parallel to hashes)  (_namePositionsOffset)
    //     int32   dataSectionOffset
    //   ------------------------------------------------------------------------------------
    //   Name section                                                 (_nameSectionOffset)
    //     per resource, at _nameSectionOffset + namePositions[i]:
    //       [7-bit byteLen][UTF-16LE name][int32 dataPosition]
    //   ------------------------------------------------------------------------------------
    //   Data section                                                 (_dataSectionOffset)
    //     per resource, at _dataSectionOffset + dataPosition:
    //       [7-bit ResourceTypeCode][value], where value is:
    //         String              : [7-bit byteLen][UTF-8 bytes]
    //         ByteArray / Stream  : [int32 byteLen][bytes]
    //         primitives          : fixed-size little-endian
    //         user types (>= 0x40): serialized payload (exposed by type name only)
    //
    // .resources magic number and the only supported RuntimeResourceSet format version.
    private const int MagicNumber = unchecked((int)0xBEEFCACE);
    private const int SupportedVersion = 2;

    private readonly ReadOnlyMemory<byte> _resources;
    private readonly IDisposable? _owned;
    private readonly int _numResources;
    private readonly int _numTypes;

    // Absolute offsets into _resources, all computed once at construction.
    private readonly int _typeNamesOffset;      // start of the type-name string table
    private readonly int _nameHashesOffset;     // int32[_numResources], sorted ascending
    private readonly int _namePositionsOffset;  // int32[_numResources], parallel to the hashes
    private readonly int _nameSectionOffset;    // start of the name section
    private readonly int _dataSectionOffset;    // start of the data section

    /// <summary>
    ///  Initializes a new instance of the <see cref="RawResourceReader"/> class over the given
    ///  <c>.resources</c> content.
    /// </summary>
    /// <param name="resources">
    ///  The complete bytes of a default-format version 2 <c>.resources</c> file. The memory is not
    ///  copied; the caller owns its lifetime and must keep it valid for the life of the reader.
    /// </param>
    /// <exception cref="ArgumentException">The data is not a <c>.resources</c> file (bad magic number).</exception>
    /// <exception cref="NotSupportedException">
    ///  The file is not format version 2 (for example a legacy version 1 file), or the current
    ///  architecture is big-endian.
    /// </exception>
    /// <exception cref="BadImageFormatException">The file header is malformed or truncated.</exception>
    public RawResourceReader(ReadOnlyMemory<byte> resources)
        : this(resources, owned: null)
    {
    }

    private RawResourceReader(ReadOnlyMemory<byte> resources, IDisposable? owned)
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("RawResourceReader is only supported on little-endian architectures.");
        }

        _owned = owned;
        _resources = resources;
        SpanReader<byte> reader = new(resources.Span);

        if (!reader.TryReadInt32LittleEndian(out int magic) || magic != MagicNumber)
        {
            throw new ArgumentException(
                "The data is not a valid .resources file (bad magic number).",
                nameof(resources));
        }

        if (!reader.TryReadInt32LittleEndian(out int resourceManagerHeaderVersion)
            || resourceManagerHeaderVersion < 0
            || !reader.TryReadInt32LittleEndian(out int byteCountToSkip)

            // Skip the rest of the ResourceManager header (reader and set type names). We parse the
            // format structurally and do not validate the declared reader type, so files written for the
            // System.Resources.Extensions reader are read up to their common primitive/string content.
            || !reader.TryAdvance(byteCountToSkip))
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (bad header).");
        }

        if (!reader.TryReadInt32LittleEndian(out int version) || version != SupportedVersion)
        {
            throw new NotSupportedException(
                $"Unsupported .resources format version {version}; only version {SupportedVersion} is supported.");
        }

        if (!reader.TryReadInt32LittleEndian(out _numResources)
            || _numResources < 0
            || !reader.TryReadInt32LittleEndian(out _numTypes)
            || _numTypes < 0)
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (bad resource or type count).");
        }

        // Type-name table: _numTypes length-prefixed UTF-8 strings. Remember the start so user type
        // names can be read on demand; skip past it here without allocating.
        _typeNamesOffset = reader.Position;
        for (int i = 0; i < _numTypes; i++)
        {
            if (!reader.TryRead7BitEncodedInt32(out int typeNameLength)
                || typeNameLength < 0
                || !reader.TryAdvance(typeNameLength))
            {
                ThrowBadImageFormatException("The file is not a valid .resources file (bad type name).");
            }
        }

        // The name-hash array is aligned to 8 bytes (the writer pads with 'P','A','D').
        if (!reader.TryAdvance((8 - (reader.Position & 7)) & 7))
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (bad alignment).");
        }

        _nameHashesOffset = reader.Position;

        // The two int32 index arrays and the data-section offset are fixed size; validate they fit.
        long indexBytes = ((long)_numResources * 8) + 4;
        if (indexBytes > reader.Unread.Length)
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (truncated index).");
        }

        _namePositionsOffset = _nameHashesOffset + (_numResources * 4);
        reader.Position = _namePositionsOffset + (_numResources * 4);

        if (!reader.TryReadInt32LittleEndian(out _dataSectionOffset))
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (bad data section).");
        }

        _nameSectionOffset = reader.Position;

        if (_dataSectionOffset < _nameSectionOffset || _dataSectionOffset > reader.Length)
        {
            ThrowBadImageFormatException("The file is not a valid .resources file (bad data section).");
        }
    }

    /// <summary>
    ///  Creates a reader over memory and transfers ownership of <paramref name="owned"/> to it.
    /// </summary>
    /// <param name="resources">The resource image.</param>
    /// <param name="owned">The object that keeps the image valid and releases it.</param>
    /// <returns>A reader that owns the backing object.</returns>
    internal static RawResourceReader CreateOwned(
        ReadOnlyMemory<byte> resources,
        IDisposable owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        try
        {
            return new(resources, owned);
        }
        catch
        {
            owned.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  Creates a <see cref="RawResourceReader"/> that memory-maps the file at <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The file is mapped, not copied, so no array is allocated for its contents. The returned
    ///   reader owns the mapping and releases it when <see cref="Dispose"/> is called; do not use the
    ///   reader after disposing it.
    ///  </para>
    /// </remarks>
    /// <param name="path">The path of a <c>.resources</c> file.</param>
    /// <returns>A reader that owns the file mapping.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static RawResourceReader CreateFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        MappedMemoryManager resource = MappedMemoryManager.CreateFromFile(path);
        try
        {
            return new RawResourceReader(resource.Memory, resource);
        }
        catch
        {
            ((IDisposable)resource).Dispose();
            throw;
        }
    }

    /// <summary>
    ///  Releases the memory mapping owned by this reader, if any. Disposal is thread-safe and
    ///  idempotent; after it, member access throws <see cref="ObjectDisposedException"/>. A reader
    ///  constructed directly over a <see cref="ReadOnlyMemory{T}"/> owns no mapping to release.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="DisposableBase.Dispose()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _owned?.Dispose();
        }
    }

    /// <summary>
    ///  The number of resources in the file.
    /// </summary>
    public int ResourceCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return _numResources;
        }
    }

    /// <summary>
    ///  Finds a resource by name.
    /// </summary>
    /// <param name="name">The resource name to look up (ordinal, case-sensitive).</param>
    /// <param name="location">On success, the located resource.</param>
    /// <returns><see langword="true"/> if a resource with the given name exists.</returns>
    /// <exception cref="BadImageFormatException">The file is malformed.</exception>
    public bool TryFindResource(ReadOnlySpan<char> name, out ResourceLocation location)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        location = default;
        if (_numResources == 0)
        {
            return false;
        }

        int hash = ResourceNameHash.Compute(name);

        // The header is validated as little-endian at construction, so the sorted int32 hash array can
        // be reinterpreted in place and searched directly - no per-probe reads.
        ReadOnlySpan<byte> resources = _resources.Span;
        ReadOnlySpan<int> hashes = MemoryMarshal.Cast<byte, int>(resources.Slice(_nameHashesOffset, _numResources * 4));

        // Binary search the sorted hash array.
        int lo = 0;
        int hi = _numResources - 1;
        int index = -1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int currentHash = hashes[mid];
            if (currentHash == hash)
            {
                index = mid;
                break;
            }

            if (currentHash < hash)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (index < 0)
        {
            return false;
        }

        // Hashes can collide; expand to the full run of equal hashes and compare names.
        int runStart = index;
        while (runStart > 0 && hashes[runStart - 1] == hash)
        {
            runStart--;
        }

        int runEnd = index;
        while (runEnd < _numResources - 1 && hashes[runEnd + 1] == hash)
        {
            runEnd++;
        }

        SpanReader<byte> reader = new(resources);
        for (int i = runStart; i <= runEnd; i++)
        {
            reader.Position = _nameSectionOffset + GetNamePosition(i);
            if (!reader.TryRead7BitEncodedInt32(out int nameByteLength)
                || nameByteLength < 0
                || (nameByteLength & 1) != 0)
            {
                ThrowBadImageFormatException("A resource name is corrupted.");
            }

            if (nameByteLength != (long)name.Length * 2)
            {
                continue;
            }

            if (!reader.TryRead(nameByteLength, out ReadOnlySpan<byte> nameBytes))
            {
                ThrowBadImageFormatException("A resource name is corrupted.");
            }

            // Names are stored UTF-16LE and the reader requires a little-endian architecture, so the
            // bytes can be reinterpreted as chars and compared directly.
            if (MemoryMarshal.Cast<byte, char>(nameBytes).SequenceEqual(name))
            {
                if (!reader.TryReadInt32LittleEndian(out int dataPosition))
                {
                    ThrowBadImageFormatException("A resource name is corrupted.");
                }

                location = BuildLocation(i, dataPosition);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc cref="TryFindResource(ReadOnlySpan{char}, out ResourceLocation)"/>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool TryFindResource(string name, out ResourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(name);
        return TryFindResource(name.AsSpan(), out location);
    }

    /// <summary>
    ///  Gets the location (type and byte length) of the resource at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">A resource index in the range <c>[0, <see cref="ResourceCount"/>)</c>.</param>
    /// <returns>The location of the resource at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The file is malformed.</exception>
    public ResourceLocation GetLocation(int index)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _numResources);
        return BuildLocation(index, ReadDataPosition(index));
    }

    /// <inheritdoc/>
    ResourceTypeCode IStringResourceReader.GetResourceTypeCode(int index)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _numResources);

        SpanReader<byte> reader = new(_resources.Span)
        {
            Position = GetDataOffset(ReadDataPosition(index))
        };

        if (!reader.TryRead7BitEncodedInt32(out int typeCodeValue) || typeCodeValue < 0)
        {
            ThrowBadImageFormatException("A resource type code is corrupted.");
        }

        return ResourceTypeCodeValidator.Validate(typeCodeValue, _numTypes);
    }

    /// <inheritdoc/>
    string IStringResourceReader.GetResourceName(int index)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _numResources);

        SpanReader<byte> reader = new(_resources.Span)
        {
            Position = _nameSectionOffset + GetNamePosition(index)
        };

        if (!reader.TryRead7BitEncodedInt32(out int byteLength)
            || byteLength < 0
            || (byteLength & 1) != 0)
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        if (!reader.TryRead(byteLength, out ReadOnlySpan<byte> nameBytes))
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        return MemoryMarshal.Cast<byte, char>(nameBytes).ToString();
    }

    /// <inheritdoc/>
    string IStringResourceReader.GetString(int index)
    {
        ResourceLocation location = GetLocation(index);
        return GetString(location);
    }

    /// <inheritdoc/>
    StringResourceLookupKind IStringResourceReader.Lookup(string name, out string? value)
    {
        if (!TryFindResource(name, out ResourceLocation location)
            || location.TypeCode != ResourceTypeCode.String)
        {
            value = null;
            return StringResourceLookupKind.Missing;
        }

        value = GetString(location);
        return StringResourceLookupKind.Found;
    }

    /// <summary>
    ///  Copies the raw value content of the resource at <paramref name="index"/> into
    ///  <paramref name="destination"/>.
    /// </summary>
    /// <param name="index">A resource index in the range <c>[0, <see cref="ResourceCount"/>)</c>.</param>
    /// <param name="destination">The span to copy the value bytes into.</param>
    /// <param name="bytesWritten">On success, the number of bytes written.</param>
    /// <returns>
    ///  <see langword="false"/> if the resource is a user type (no bytes are exposed) or if
    ///  <paramref name="destination"/> is too small; otherwise <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public bool TryGetResourceData(int index, Span<byte> destination, out int bytesWritten)
    {
        ResourceLocation location = GetLocation(index);
        if (location.IsUserType || destination.Length < location.ByteLength)
        {
            bytesWritten = 0;
            return false;
        }

        _resources.Span.Slice(location.ContentOffset, location.ByteLength).CopyTo(destination);
        bytesWritten = location.ByteLength;
        return true;
    }

    /// <summary>
    ///  Copies the raw value content of the resource at <paramref name="index"/> into
    ///  <paramref name="destination"/>.
    /// </summary>
    /// <param name="index">A resource index in the range <c>[0, <see cref="ResourceCount"/>)</c>.</param>
    /// <param name="destination">The stream to write the value bytes to.</param>
    /// <returns><see langword="false"/> if the resource is a user type; otherwise <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public bool TryCopyResourceData(int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ResourceLocation location = GetLocation(index);
        if (location.IsUserType)
        {
            return false;
        }

        ReadOnlyMemory<byte> content = _resources.Slice(location.ContentOffset, location.ByteLength);
        destination.Write(content.Span);
        return true;
    }

    /// <summary>
    ///  Decodes the string at <paramref name="location"/> directly from the indexed resource image.
    /// </summary>
    /// <param name="location">The string resource location returned by this reader.</param>
    /// <returns>The decoded string.</returns>
    internal string GetString(ResourceLocation location)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (location.TypeCode != ResourceTypeCode.String)
        {
            throw new InvalidOperationException("The resource is not a string.");
        }

        return Encoding.UTF8.GetString(
            _resources.Span.Slice(location.ContentOffset, location.ByteLength));
    }

    /// <summary>
    ///  Copies the name of the resource at <paramref name="index"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="index">A resource index in the range <c>[0, <see cref="ResourceCount"/>)</c>.</param>
    /// <param name="destination">The span to copy the name characters into.</param>
    /// <param name="charsWritten">On success, the number of characters written.</param>
    /// <returns>
    ///  <see langword="false"/> if <paramref name="destination"/> is too small; otherwise <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The file is malformed.</exception>
    public bool TryGetResourceName(int index, Span<char> destination, out int charsWritten)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _numResources);

        SpanReader<byte> reader = new(_resources.Span) { Position = _nameSectionOffset + GetNamePosition(index) };
        if (!reader.TryRead7BitEncodedInt32(out int nameByteLength)
            || nameByteLength < 0
            || (nameByteLength & 1) != 0)
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        // Read the name bytes before checking the destination size so a length that runs past the end
        // of the file is rejected as corruption here, rather than returning false and driving the caller
        // to grow its buffer toward the claimed (potentially unbounded) length.
        if (!reader.TryRead(nameByteLength, out ReadOnlySpan<byte> nameBytes))
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        int charCount = nameByteLength / 2;
        if (destination.Length < charCount)
        {
            charsWritten = 0;
            return false;
        }

        // Names are stored UTF-16LE and the reader requires a little-endian architecture, so the bytes
        // can be reinterpreted as chars and copied directly.
        MemoryMarshal.Cast<byte, char>(nameBytes).CopyTo(destination);
        charsWritten = charCount;
        return true;
    }

    /// <summary>
    ///  Copies the declared type name of the user type at <paramref name="index"/> into
    ///  <paramref name="destination"/>.
    /// </summary>
    /// <param name="index">A resource index in the range <c>[0, <see cref="ResourceCount"/>)</c>.</param>
    /// <param name="destination">The span to copy the type name characters into.</param>
    /// <param name="charsWritten">On success, the number of characters written.</param>
    /// <returns>
    ///  <see langword="false"/> if the resource is not a user type or if <paramref name="destination"/>
    ///  is too small; otherwise <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The file is malformed.</exception>
    public bool TryGetUserTypeName(int index, Span<char> destination, out int charsWritten)
    {
        ResourceLocation location = GetLocation(index);
        if (!location.IsUserType)
        {
            charsWritten = 0;
            return false;
        }

        int typeIndex = (int)location.TypeCode - (int)ResourceTypeCode.StartOfUserTypes;
        if (typeIndex < 0 || typeIndex >= _numTypes)
        {
            throw new BadImageFormatException("A user type index is out of range.");
        }

        SpanReader<byte> reader = new(_resources.Span) { Position = _typeNamesOffset };
        for (int i = 0; i < typeIndex; i++)
        {
            if (!reader.TryRead7BitEncodedInt32(out int skipLength)
                || skipLength < 0
                || !reader.TryAdvance(skipLength))
            {
                ThrowBadImageFormatException("A user type name is corrupted.");
            }
        }

        if (!reader.TryRead7BitEncodedInt32(out int typeNameLength) || typeNameLength < 0)
        {
            ThrowBadImageFormatException("A user type name is corrupted.");
        }

        if (!reader.TryRead(typeNameLength, out ReadOnlySpan<byte> typeNameBytes))
        {
            ThrowBadImageFormatException("A user type name is corrupted.");
        }

        return Encoding.UTF8.TryGetChars(typeNameBytes, destination, out charsWritten);
    }

    // Reads the data-section virtual offset stored after the name for a resource index.
    private int ReadDataPosition(int index)
    {
        SpanReader<byte> reader = new(_resources.Span) { Position = _nameSectionOffset + GetNamePosition(index) };
        if (!reader.TryRead7BitEncodedInt32(out int nameByteLength)
            || nameByteLength < 0
            || (nameByteLength & 1) != 0
            || !reader.TryAdvance(nameByteLength))
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        if (!reader.TryReadInt32LittleEndian(out int dataPosition))
        {
            ThrowBadImageFormatException("A resource name is corrupted.");
        }

        return dataPosition;
    }

    // Reads a resource's type code and computes its content offset and length.
    private ResourceLocation BuildLocation(int index, int dataPosition)
    {
        SpanReader<byte> reader = new(_resources.Span) { Position = GetDataOffset(dataPosition) };
        if (!reader.TryRead7BitEncodedInt32(out int typeCodeValue))
        {
            ThrowBadImageFormatException("A resource type code is corrupted.");
        }

        ResourceTypeCode typeCode = ResourceTypeCodeValidator.Validate(typeCodeValue, _numTypes);

        if (typeCodeValue >= (int)ResourceTypeCode.StartOfUserTypes)
        {
            // Serialized user type: report the code and type name only, no value bytes.
            return new ResourceLocation(index, typeCode, byteLength: 0, contentOffset: reader.Position);
        }

        int contentLength;
        switch (typeCode)
        {
            case ResourceTypeCode.Null:
                contentLength = 0;
                break;
            case ResourceTypeCode.Boolean:
            case ResourceTypeCode.Byte:
            case ResourceTypeCode.SByte:
                contentLength = 1;
                break;
            case ResourceTypeCode.Char:
            case ResourceTypeCode.Int16:
            case ResourceTypeCode.UInt16:
                contentLength = 2;
                break;
            case ResourceTypeCode.Int32:
            case ResourceTypeCode.UInt32:
            case ResourceTypeCode.Single:
                contentLength = 4;
                break;
            case ResourceTypeCode.Int64:
            case ResourceTypeCode.UInt64:
            case ResourceTypeCode.Double:
            case ResourceTypeCode.DateTime:
            case ResourceTypeCode.TimeSpan:
                contentLength = 8;
                break;
            case ResourceTypeCode.Decimal:
                contentLength = 16;
                break;
            case ResourceTypeCode.String:
                if (!reader.TryRead7BitEncodedInt32(out contentLength))
                {
                    ThrowBadImageFormatException("A resource value length is out of range.");
                }

                break;
            case ResourceTypeCode.ByteArray:
            case ResourceTypeCode.Stream:
                if (!reader.TryReadInt32LittleEndian(out contentLength))
                {
                    ThrowBadImageFormatException("A resource value length is out of range.");
                }

                break;
            default:
                throw new BadImageFormatException($"Unsupported resource type code 0x{typeCodeValue:X}.");
        }

        int contentOffset = reader.Position;
        if (contentLength < 0 || contentOffset > _resources.Length - contentLength)
        {
            ThrowBadImageFormatException("A resource value length is out of range.");
        }

        return new ResourceLocation(index, typeCode, contentLength, contentOffset);
    }

    private int GetDataOffset(int dataPosition)
    {
        if (dataPosition < 0 || dataPosition >= _resources.Length - _dataSectionOffset)
        {
            throw new BadImageFormatException("A resource data offset is out of range.");
        }

        return _dataSectionOffset + dataPosition;
    }

    private int GetNamePosition(int index)
    {
        SpanReader<byte> reader = new(_resources.Span) { Position = _namePositionsOffset + (index * 4) };
        if (!reader.TryReadInt32LittleEndian(out int position)
            || position < 0
            || position > _dataSectionOffset - _nameSectionOffset)
        {
            ThrowBadImageFormatException("A resource name offset is out of range.");
        }

        return position;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    private static void ThrowBadImageFormatException(string message) => throw new BadImageFormatException(message);
}
