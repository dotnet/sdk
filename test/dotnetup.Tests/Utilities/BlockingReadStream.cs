// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Test stream that optionally emits a prefix and then waits until the read is canceled.
/// Assumes callers exercise asynchronous reads only.
/// </summary>
internal sealed class BlockingReadStream : Stream
{
    private readonly byte[] _prefix;
    private int _offset;

    public BlockingReadStream(byte[]? prefix = null)
    {
        _prefix = prefix ?? [];
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset < _prefix.Length)
        {
            int bytesToCopy = Math.Min(buffer.Length, _prefix.Length - _offset);
            _prefix.AsMemory(_offset, bytesToCopy).CopyTo(buffer);
            _offset += bytesToCopy;
            return bytesToCopy;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}