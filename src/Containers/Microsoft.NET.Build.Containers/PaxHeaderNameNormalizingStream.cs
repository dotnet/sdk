// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A write-through stream that rewrites the name of pax extended headers so that a tar archive does
/// not depend on the process that produced it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Formats.Tar.TarWriter"/> names the extended header entry that precedes an entry
/// <c>./PaxHeaders.&lt;process id&gt;/.</c>. POSIX suggests including the process id so that two
/// concurrent extractions cannot collide over the same temporary name, but it means the bytes of the
/// archive differ between two runs that write identical content. That alone is enough to change a
/// container layer's digest, so the same source published twice yields two different images.
/// </para>
/// <para>
/// The name is not meaningful to an extractor: the path an extended header applies to is carried in
/// its <c>path</c> record, not in its entry name, and the entry always applies to the entry that
/// immediately follows it. Replacing the name with a constant is therefore safe and keeps the archive
/// a valid pax archive.
/// </para>
/// </remarks>
internal sealed class PaxHeaderNameNormalizingStream : Stream
{
    private const int BlockSize = 512;
    private const int NameOffset = 0;
    private const int NameLength = 100;
    private const int SizeOffset = 124;
    private const int SizeLength = 12;
    private const int ChecksumOffset = 148;
    private const int ChecksumLength = 8;
    private const int TypeFlagOffset = 156;
    private const int MagicOffset = 257;
    private const byte ExtendedHeaderTypeFlag = (byte)'x';
    private const byte GlobalExtendedHeaderTypeFlag = (byte)'g';

    /// <summary>The name written in place of the process-dependent one.</summary>
    private static ReadOnlySpan<byte> NormalizedName => "./PaxHeaders/."u8;

    private static ReadOnlySpan<byte> UstarMagic => "ustar"u8;

    private readonly Stream _inner;
    private readonly bool _leaveOpen;
    private readonly byte[] _block = new byte[BlockSize];

    private int _blockBytes;
    private long _dataBlocksRemaining;
    private bool _disposed;

    public PaxHeaderNameNormalizingStream(Stream inner, bool leaveOpen = false)
    {
        _inner = inner;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        // The stream is reassembled into 512 byte blocks because a caller may write across block
        // boundaries, and a header can only be recognized once its whole block is available.
        while (!buffer.IsEmpty)
        {
            int take = Math.Min(BlockSize - _blockBytes, buffer.Length);
            buffer[..take].CopyTo(_block.AsSpan(_blockBytes));
            _blockBytes += take;
            buffer = buffer[take..];

            if (_blockBytes == BlockSize)
            {
                ProcessBlock();
                _inner.Write(_block, 0, BlockSize);
                _blockBytes = 0;
            }
        }
    }

    public override void WriteByte(byte value) => Write(new ReadOnlySpan<byte>(in value));

    private void ProcessBlock()
    {
        // Only a header block can be rewritten, so the data blocks that follow one are skipped. This
        // matters because file content can contain anything, including bytes that look like a header.
        if (_dataBlocksRemaining > 0)
        {
            _dataBlocksRemaining--;
            return;
        }

        Span<byte> block = _block;

        // An all-zero block is end-of-archive padding rather than a header.
        if (!block.ContainsAnyExcept((byte)0))
        {
            return;
        }

        if (!block.Slice(MagicOffset, UstarMagic.Length).SequenceEqual(UstarMagic))
        {
            return;
        }

        _dataBlocksRemaining = ParseDataBlockCount(block);

        byte typeFlag = block[TypeFlagOffset];
        if (typeFlag is not (ExtendedHeaderTypeFlag or GlobalExtendedHeaderTypeFlag))
        {
            return;
        }

        Span<byte> name = block.Slice(NameOffset, NameLength);
        name.Clear();
        NormalizedName.CopyTo(name);

        WriteChecksum(block);
    }

    /// <summary>Reads the octal size field and converts it to a count of trailing data blocks.</summary>
    private static long ParseDataBlockCount(ReadOnlySpan<byte> block)
    {
        long size = 0;
        foreach (byte b in block.Slice(SizeOffset, SizeLength))
        {
            if (b is (byte)' ' or 0)
            {
                // The field is terminated by a space or NUL; anything after it is padding.
                break;
            }

            if (b is < (byte)'0' or > (byte)'7')
            {
                // Not a value this stream understands (for example a base-256 encoded size). Treating
                // it as zero would risk rewriting file content, so the rest of the archive is left alone.
                return long.MaxValue;
            }

            size = (size * 8) + (b - '0');
        }

        return (size + BlockSize - 1) / BlockSize;
    }

    /// <summary>Recomputes the header checksum, which the name change invalidates.</summary>
    private static void WriteChecksum(Span<byte> block)
    {
        Span<byte> checksumField = block.Slice(ChecksumOffset, ChecksumLength);

        // The checksum is defined as the sum of the header bytes with the checksum field read as spaces.
        checksumField.Fill((byte)' ');

        int checksum = 0;
        foreach (byte b in block)
        {
            checksum += b;
        }

        // Six octal digits, a NUL and a space, as written by the runtime.
        for (int i = 5; i >= 0; i--)
        {
            checksumField[i] = (byte)('0' + (checksum & 7));
            checksum >>= 3;
        }

        checksumField[6] = 0;
        checksumField[7] = (byte)' ';
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            // A well-formed archive is a whole number of blocks, but a trailing partial block is
            // forwarded rather than dropped so this stream never loses data it was given.
            if (_blockBytes > 0)
            {
                _inner.Write(_block, 0, _blockBytes);
                _blockBytes = 0;
            }

            if (!_leaveOpen)
            {
                _inner.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
