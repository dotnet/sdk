// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal sealed class ScriptInjectingStream : Stream
{
    internal static string InjectedScript { get; } = $"<script src=\"{ApplicationPaths.BrowserRefreshJS}\"></script>";

    private static readonly ReadOnlyMemory<byte> s_bodyTagBytes = "</body>"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> s_injectedScriptBytes = Encoding.UTF8.GetBytes(InjectedScript);

    private readonly Stream _baseStream;
    private readonly byte[] _bodyTagBuffer;

    private int _bodyTagBufferLength;

    public ScriptInjectingStream(Stream baseStream)
    {
        _baseStream = baseStream;
        _bodyTagBuffer = new byte[s_bodyTagBytes.Length];
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length { get; }
    public override long Position { get; set; }
    public bool ScriptInjectionPerformed { get; private set; }

    public override void Flush()
        => _baseStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _baseStream.FlushAsync(cancellationToken);

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!ScriptInjectionPerformed)
        {
            ScriptInjectionPerformed = TryInjectScript(buffer);
        }
        else
        {
            _baseStream.Write(buffer);
        }
    }

    public override void WriteByte(byte value)
    {
        _baseStream.WriteByte(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!ScriptInjectionPerformed)
        {
            ScriptInjectionPerformed = TryInjectScript(buffer.AsSpan(offset, count));
        }
        else
        {
            _baseStream.Write(buffer, offset, count);
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!ScriptInjectionPerformed)
        {
            ScriptInjectionPerformed = await TryInjectScriptAsync(buffer.AsMemory(offset, count), cancellationToken);
        }
        else
        {
            await _baseStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!ScriptInjectionPerformed)
        {
            ScriptInjectionPerformed = await TryInjectScriptAsync(buffer, cancellationToken);
        }
        else
        {
            await _baseStream.WriteAsync(buffer, cancellationToken);
        }
    }

    // Changes to this method should be kept in sync with TryInjectScriptAsync.
    private bool TryInjectScript(ReadOnlySpan<byte> buffer)
    {
        if (_bodyTagBufferLength != 0)
        {
            // We're in the middle of parsing a potential body tag,
            // so we'll start by parsing the rest of the tag and performing
            // script injection if applicable.

            var partialTagLength = FindPartialTagLengthFromStart(_bodyTagBufferLength, buffer);
            if (partialTagLength == -1)
            {
                // This wasn't a closing body tag. Flush what we've buffered so far and reset.
                // We don't return here because we want to continue to process the buffer as if
                // we weren't reading a partial body tag.
                _baseStream.Write(_bodyTagBuffer.AsSpan()[.._bodyTagBufferLength]);
                _bodyTagBufferLength = 0;
            }
            else
            {
                // This may still be a closing body tag. Copy the contents to the temporary buffer.
                buffer[..partialTagLength].CopyTo(_bodyTagBuffer.AsSpan()[_bodyTagBufferLength..]);
                _bodyTagBufferLength += partialTagLength;

                Debug.Assert(_bodyTagBufferLength <= s_bodyTagBytes.Length);

                if (_bodyTagBufferLength == s_bodyTagBytes.Length)
                {
                    // We've just read a full closing body tag, so we flush it to the stream.
                    // Then just write the rest of the stream normally as we've now finished searching
                    // for the script.
                    _baseStream.Write(s_injectedScriptBytes.Span);
                    _baseStream.Write(_bodyTagBuffer);
                    _baseStream.Write(buffer[partialTagLength..]);
                    _bodyTagBufferLength = 0;
                    return true;
                }

                // We're still in the middle of reading the body tag,
                // so there's nothing else to flush to the stream.
                return false;
            }
        }

        // We now know we're not in the middle of processing a body tag.
        Debug.Assert(_bodyTagBufferLength == 0);

        var index = buffer.LastIndexOf(s_bodyTagBytes.Span);
        if (index == -1)
        {
            // We didn't find the full closing body tag, but the end of the buffer
            // might contain the start of a closing body tag.

            var partialBodyTagLength = FindPartialTagLengthFromEnd(buffer);
            if (partialBodyTagLength == -1)
            {
                // We know that the end of the buffer definitely does not
                // represent a closing body tag. We'll just flush the buffer
                // to the base stream.
                _baseStream.Write(buffer);
                return false;
            }
            else
            {
                // We might have found a body tag at the end of the buffer.
                // We'll write the buffer leading up to the start of the body
                // tag candidate and copy the remainder to the temporary buffer.

                _baseStream.Write(buffer[..^partialBodyTagLength]);
                buffer[^partialBodyTagLength..].CopyTo(_bodyTagBuffer);
                _bodyTagBufferLength = partialBodyTagLength;
                return false;
            }
        }

        if (index > 0)
        {
            _baseStream.Write(buffer.Slice(0, index));
            buffer = buffer[index..];
        }

        // Write the injected script
        _baseStream.Write(s_injectedScriptBytes.Span);

        // Write the rest of the buffer/HTML doc
        _baseStream.Write(buffer);
        return true;
    }

    // Changes to this method should be kept in sync with TryInjectScript.
    private async ValueTask<bool> TryInjectScriptAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_bodyTagBufferLength != 0)
        {
            // We're in the middle of parsing a potential body tag,
            // so we'll start by parsing the rest of the tag and performing
            // script injection if applicable.

            var partialTagLength = FindPartialTagLengthFromStart(_bodyTagBufferLength, buffer.Span);
            if (partialTagLength == -1)
            {
                // This wasn't a closing body tag. Flush what we've buffered so far and reset.
                // We don't return here because we want to continue to process the buffer as if
                // we weren't reading a partial body tag.
                await _baseStream.WriteAsync(_bodyTagBuffer.AsMemory()[.._bodyTagBufferLength], cancellationToken);
                _bodyTagBufferLength = 0;
            }
            else
            {
                // This may still be a closing body tag. Copy the contents to the temporary buffer.
                buffer.Span[..partialTagLength].CopyTo(_bodyTagBuffer.AsSpan()[_bodyTagBufferLength..]);
                _bodyTagBufferLength += partialTagLength;

                Debug.Assert(_bodyTagBufferLength <= s_bodyTagBytes.Length);

                if (_bodyTagBufferLength == s_bodyTagBytes.Length)
                {
                    // We've just read a full closing body tag, so we flush it to the stream.
                    // Then just write the rest of the stream normally as we've now finished searching
                    // for the script.
                    await _baseStream.WriteAsync(s_injectedScriptBytes, cancellationToken);
                    await _baseStream.WriteAsync(_bodyTagBuffer, cancellationToken);
                    await _baseStream.WriteAsync(buffer[partialTagLength..], cancellationToken);
                    _bodyTagBufferLength = 0;
                    return true;
                }

                // We're still in the middle of reading the body tag,
                // so there's nothing else to flush to the stream.
                return false;
            }
        }

        // We now know we're not in the middle of processing a body tag.
        Debug.Assert(_bodyTagBufferLength == 0);

        var index = buffer.Span.LastIndexOf(s_bodyTagBytes.Span);
        if (index == -1)
        {
            // We didn't find the full closing body tag, but the end of the buffer
            // might contain the start of a closing body tag.

            var partialBodyTagLength = FindPartialTagLengthFromEnd(buffer.Span);
            if (partialBodyTagLength == -1)
            {
                // We know that the end of the buffer definitely does not
                // represent a closing body tag. We'll just flush the buffer
                // to the base stream.
                await _baseStream.WriteAsync(buffer, cancellationToken);
                return false;
            }
            else
            {
                // We might have found a body tag at the end of the buffer.
                // We'll write the buffer leading up to the start of the body
                // tag candidate and copy the remainder to the temporary buffer.

                await _baseStream.WriteAsync(buffer[..^partialBodyTagLength], cancellationToken);
                buffer[^partialBodyTagLength..].CopyTo(_bodyTagBuffer);
                _bodyTagBufferLength = partialBodyTagLength;
                return false;
            }
        }

        if (index > 0)
        {
            await _baseStream.WriteAsync(buffer.Slice(0, index), cancellationToken);
            buffer = buffer[index..];
        }

        // Write the injected script
        await _baseStream.WriteAsync(s_injectedScriptBytes, cancellationToken);

        // Write the rest of the buffer/HTML doc
        await _baseStream.WriteAsync(buffer, cancellationToken);
        return true;
    }

    private static int FindPartialTagLengthFromStart(int currentBodyTagLength, ReadOnlySpan<byte> buffer)
    {
        var remainingBodyTagBytes = s_bodyTagBytes.Span[currentBodyTagLength..];
        var minLength = Math.Min(buffer.Length, remainingBodyTagBytes.Length);

        return buffer[..minLength].SequenceEqual(remainingBodyTagBytes[..minLength])
            ? minLength
            : -1;
    }

    private static int FindPartialTagLengthFromEnd(ReadOnlySpan<byte> buffer)
    {
        var bufferLength = buffer.Length;
        if (bufferLength == 0)
        {
            return -1;
        }

        var lastByte = buffer[^1];
        var bodyMarkerIndexOfLastByte = BodyTagIndexOf(lastByte);
        if (bodyMarkerIndexOfLastByte == -1)
        {
            return -1;
        }

        var partialTagLength = bodyMarkerIndexOfLastByte + 1;
        if (buffer.Length < partialTagLength)
        {
            return -1;
        }

        return buffer[^partialTagLength..].SequenceEqual(s_bodyTagBytes.Span[..partialTagLength])
            ? partialTagLength
            : -1;

        // We can utilize the fact that each character is unique in "</body>"
        // to perform an efficient index lookup.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int BodyTagIndexOf(byte c)
            => c switch
            {
                (byte)'<' => 0,
                (byte)'/' => 1,
                (byte)'b' => 2,
                (byte)'o' => 3,
                (byte)'d' => 4,
                (byte)'y' => 5,
                (byte)'>' => 6,
                _ => -1,
            };
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (_bodyTagBufferLength > 0)
        {
            _baseStream.Write(_bodyTagBuffer.AsSpan()[.._bodyTagBufferLength]);
            _bodyTagBufferLength = 0;
        }

        Flush();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_bodyTagBufferLength > 0)
        {
            await _baseStream.WriteAsync(_bodyTagBuffer.AsMemory()[.._bodyTagBufferLength]);
            _bodyTagBufferLength = 0;
        }

        await FlushAsync();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
         => throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
         => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
