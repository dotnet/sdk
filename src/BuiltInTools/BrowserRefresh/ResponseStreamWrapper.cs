// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Based on https://github.com/RickStrahl/Westwind.AspnetCore.LiveReload/blob/128b5f524e86954e997f2c453e7e5c1dcc3db746/Westwind.AspnetCore.LiveReload/ResponseStreamWrapper.cs

using System.Buffers;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    /// <summary>
    /// Wraps the Response Stream to inject the WebSocket HTML into
    /// an HTML Page.
    /// </summary>
    public class ResponseStreamWrapper : Stream
    {
        private static readonly MediaTypeHeaderValue _textHtmlMediaType = new("text/html");
        private readonly Stream _baseStream;
        private readonly HttpContext _context;
        private readonly ILogger _logger;
        private bool? _isHtmlResponse;
        private bool _isGzipEncoded;
        private BufferingStream? _bufferStream;
        private GZipStream? _gzipStream;
        private byte[]? _decompressBuffer;
        private bool _disposed;

        public ResponseStreamWrapper(HttpContext context, ILogger logger)
        {
            _context = context;
            _baseStream = context.Response.Body;
            _logger = logger;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get; }
        public override long Position { get; set; }
        public bool ScriptInjectionPerformed { get; private set; }

        public bool IsHtmlResponse => _isHtmlResponse ?? false;

        public override void Flush()
        {
            OnWrite();
            if(_isGzipEncoded)
            {
                WriteRemainingBytes();
            }
            _baseStream.Flush();
        }

        public async override Task FlushAsync(CancellationToken cancellationToken)
        {
            OnWrite();
            if (_isGzipEncoded)
            {
                WriteRemainingBytes();
            }

            await _baseStream.FlushAsync(cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            OnWrite();
            if (IsHtmlResponse)
            {
                var data = buffer;
                if (_isGzipEncoded)
                {
                    _bufferStream!.Write(buffer);
                    var read = _gzipStream!.Read(_decompressBuffer);
                    if (read > 0)
                    {
                        data = _decompressBuffer.AsSpan(0, read);
                    }
                }

                // Non-gzip HTML: attempt direct injection directly on provided buffer
                if (!ScriptInjectionPerformed)
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, data);
                    if (ScriptInjectionPerformed)
                    {
                        return;
                    }
                }
            }
            _baseStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            OnWrite();
            _baseStream.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            OnWrite();
            if (IsHtmlResponse)
            {
                var data = buffer.AsSpan(offset, count);
                if (_isGzipEncoded)
                {
                    _bufferStream!.Write(buffer);
                    var read = _gzipStream!.Read(_decompressBuffer);
                    if (read > 0)
                    {
                        data = _decompressBuffer.AsSpan(0, read);
                    }
                }

                if (!ScriptInjectionPerformed)
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, data);
                    if (ScriptInjectionPerformed)
                    {
                        return;
                    }
                }
            }

            _baseStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            OnWrite();
            if (IsHtmlResponse)
            {
                var data = buffer.AsMemory(offset, count);
                if (_isGzipEncoded)
                {
                    await _bufferStream!.WriteAsync(buffer, offset, count, cancellationToken);
                    var read = await _gzipStream!.ReadAsync(_decompressBuffer!, 0, _decompressBuffer!.Length, cancellationToken);
                    if (read > 0)
                    {
                        data = _decompressBuffer.AsMemory(0, read);
                    }
                }
                if (!ScriptInjectionPerformed)
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, data, cancellationToken);
                    if (ScriptInjectionPerformed)
                    {
                        return;
                    }
                }
            }
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            OnWrite();
            if (IsHtmlResponse)
            {
                if(_isGzipEncoded)
                {
                    await _bufferStream!.WriteAsync(buffer, cancellationToken);
                    var read = await _gzipStream!.ReadAsync(_decompressBuffer!, 0, _decompressBuffer!.Length, cancellationToken);
                    if (read > 0)
                    {
                        buffer = _decompressBuffer.AsMemory(0, read);
                    }
                }
                if (!ScriptInjectionPerformed)
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, buffer, cancellationToken);
                    if (ScriptInjectionPerformed)
                    {
                        return;
                    }
                }
            }
            await _baseStream.WriteAsync(buffer, cancellationToken);
        }

        private void OnWrite()
        {
            if (_isHtmlResponse.HasValue)
            {
                return;
            }

            var response = _context.Response;

            _isHtmlResponse =
                (response.StatusCode == StatusCodes.Status200OK || response.StatusCode == StatusCodes.Status500InternalServerError) &&
                MediaTypeHeaderValue.TryParse(response.ContentType, out var mediaType) &&
                mediaType.IsSubsetOf(_textHtmlMediaType) &&
                (!mediaType.Charset.HasValue || mediaType.Charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase));

            if (_isHtmlResponse.Value)
            {
                BrowserRefreshMiddleware.Log.SetupResponseForBrowserRefresh(_logger);

                // Since we're changing the markup content, reset the content-length
                response.Headers.ContentLength = null;

                // Check if the response has gzip Content-Encoding
                if (response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodingValues))
                {
                    var contentEncoding = contentEncodingValues.FirstOrDefault();
                    if (string.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        _isGzipEncoded = true;
                        // Remove the Content-Encoding header since we'll be serving uncompressed content
                        response.Headers.Remove(HeaderNames.ContentEncoding);
                        InitializeBuffers();
                    }
                }
            }
        }

        private void InitializeBuffers()
        {
            _bufferStream = new BufferingStream();
            // Create gzip stream immediately so reads are ready once we flush 8K
            _gzipStream = new GZipStream(_bufferStream, CompressionMode.Decompress, leaveOpen: true);
            _decompressBuffer = ArrayPool<byte>.Shared.Rent(8192);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
             => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
             => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Flush(); // Will not complete pipe for gzip
                if (_isGzipEncoded)
                {
                    WriteRemainingBytes();
                    _gzipStream!.Dispose();
                    _bufferStream!.Dispose();
                    ArrayPool<byte>.Shared.Return(_decompressBuffer!);
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        private void WriteRemainingBytes()
        {
            while (_bufferStream!.HasPendingBytes())
            {
                var read = _gzipStream!.Read(_decompressBuffer!);
                if (read > 0)
                {
                    var data = _decompressBuffer.AsSpan(0, read);
                    if (!ScriptInjectionPerformed)
                    {
                        ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, data);
                        if (ScriptInjectionPerformed)
                        {
                            continue;
                        }
                    }
                    _baseStream.Write(data);
                }
                else
                {
                    break;
                }
            }
        }
    }

    internal class BufferingStream : Stream
    {
        private readonly List<byte[]> _buffers = [ArrayPool<byte>.Shared.Rent(8192)];
        private int _currentReadBufferIndex;
        private int _currentReadPosition;
        private int _currentWritePosition;
        private int _totalBytesRead;
        private int _totalBytesWritten;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => _totalBytesRead; set => throw new NotImplementedException(); }


        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Determine the available bytes
            var available = _totalBytesWritten - _totalBytesRead;

            // Pick either all remaining unread bytes or the remaining buffer content
            var currentReadBuffer = _buffers[_currentReadBufferIndex];
            var remainingInCurrentBuffer = currentReadBuffer.Length - _currentReadPosition;
            var readBuffer = _buffers[_currentReadBufferIndex].AsSpan(_currentReadPosition, Math.Min(available, remainingInCurrentBuffer));

            var bufferSpan = buffer.AsSpan(offset, count);
            var currentReadBytes = 0;
            if (readBuffer.Length > bufferSpan.Length)
            {
                // Copy only what fits in the buffer
                readBuffer.Slice(0, bufferSpan.Length).CopyTo(bufferSpan);
                _currentReadPosition += bufferSpan.Length;
                _totalBytesRead += bufferSpan.Length;
                return bufferSpan.Length;
            }

            do
            {
                // Copy the remaining contents of the current buffer or the remaining unread bytes
                readBuffer.CopyTo(bufferSpan);
                available -= readBuffer.Length;
                currentReadBytes += readBuffer.Length;
                bufferSpan = bufferSpan.Slice(readBuffer.Length);
                _totalBytesRead += readBuffer.Length;
                if (available == 0)
                {
                    return currentReadBytes;
                }
                _currentReadBufferIndex++;
                _currentReadPosition = 0;
                readBuffer = _buffers[_currentReadBufferIndex].AsSpan();
            } while (readBuffer.Length <= bufferSpan.Length);

            return currentReadBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var current = _buffers[^1].AsSpan(_currentWritePosition);
            var bufferSpan = buffer.AsSpan(offset, count);
            if (bufferSpan.Length <= current.Length)
            {
                bufferSpan.CopyTo(current);
                _currentWritePosition += bufferSpan.Length;
                _totalBytesWritten += bufferSpan.Length;
                return;
            }
            else
            {
                // Fill in the remaining space in the current buffer
                bufferSpan.Slice(0, current.Length).CopyTo(current);
                _totalBytesWritten += current.Length;
                // "consume" the copied bytes
                bufferSpan = bufferSpan.Slice(current.Length);

                _currentWritePosition = 0;
                while (bufferSpan.Length > 0)
                {
                    var newBuffer = ArrayPool<byte>.Shared.Rent(8192);
                    _buffers.Add(newBuffer);
                    current = newBuffer;
                    if (bufferSpan.Length <= current.Length)
                    {
                        bufferSpan.CopyTo(current);
                        _currentWritePosition += bufferSpan.Length;
                        _totalBytesWritten += bufferSpan.Length;
                        return;
                    }
                    bufferSpan.Slice(0, current.Length).CopyTo(current);
                    _totalBytesWritten += current.Length;
                    bufferSpan = bufferSpan.Slice(current.Length);
                }
            }
        }

        public override void Flush() { }

        internal bool HasPendingBytes() => _totalBytesWritten < _totalBytesRead;
    }
}
