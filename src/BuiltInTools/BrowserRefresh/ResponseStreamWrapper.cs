// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Based on https://github.com/RickStrahl/Westwind.AspnetCore.LiveReload/blob/128b5f524e86954e997f2c453e7e5c1dcc3db746/Westwind.AspnetCore.LiveReload/ResponseStreamWrapper.cs

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
        private string? _contentEncoding;
        private MemoryStream? _compressedBuffer;
        private bool _compressionHandled;

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
            
            // Handle any pending compressed data
            if (_compressedBuffer != null && !_compressionHandled && _compressedBuffer.Length > 0)
            {
                var success = TryDecompressAndInject();
                _compressionHandled = true;
            }
            
            _baseStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            OnWrite();
            
            // Handle any pending compressed data
            if (_compressedBuffer != null && !_compressionHandled && _compressedBuffer.Length > 0)
            {
                var success = TryDecompressAndInject();
                _compressionHandled = true;
            }
            
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            OnWrite();
            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                if (!string.IsNullOrEmpty(_contentEncoding))
                {
                    // Convert span to array for compression handling
                    var tempBuffer = buffer.ToArray();
                    HandleCompressedWrite(tempBuffer, 0, tempBuffer.Length);
                }
                else
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, buffer);
                }
            }
            else
            {
                _baseStream.Write(buffer);
            }
        }

        public override void WriteByte(byte value)
        {
            OnWrite();
            _baseStream.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            OnWrite();

            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                if (!string.IsNullOrEmpty(_contentEncoding))
                {
                    HandleCompressedWrite(buffer, offset, count);
                }
                else
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, buffer.AsSpan(offset, count));
                }
            }
            else
            {
                _baseStream.Write(buffer, offset, count);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            OnWrite();

            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                if (!string.IsNullOrEmpty(_contentEncoding))
                {
                    await HandleCompressedWriteAsync(buffer, offset, count, cancellationToken);
                }
                else
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, buffer.AsMemory(offset, count), cancellationToken);
                }
            }
            else
            {
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            OnWrite();

            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                if (!string.IsNullOrEmpty(_contentEncoding))
                {
                    // Convert memory to array for compression handling
                    var tempBuffer = buffer.ToArray();
                    await HandleCompressedWriteAsync(tempBuffer, 0, tempBuffer.Length, cancellationToken);
                }
                else
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, buffer, cancellationToken);
                }
            }
            else
            {
                await _baseStream.WriteAsync(buffer, cancellationToken);
            }
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

                // Check if the response has a Content-Encoding header
                if (response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodingValues))
                {
                    _contentEncoding = contentEncodingValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(_contentEncoding))
                    {
                        // Remove the Content-Encoding header since we'll be serving uncompressed content
                        response.Headers.Remove(HeaderNames.ContentEncoding);
                        
                        // Initialize buffer for collecting compressed content
                        _compressedBuffer = new MemoryStream();
                    }
                }
            }
        }

        private void HandleCompressedWrite(byte[] buffer, int offset, int count)
        {
            if (_compressedBuffer == null)
            {
                // Fallback: write directly to base stream
                _baseStream.Write(buffer, offset, count);
                return;
            }

            if (_compressionHandled)
            {
                // Already processed compression, write directly to base stream
                _baseStream.Write(buffer, offset, count);
                return;
            }

            // Append compressed data to buffer
            _compressedBuffer.Write(buffer, offset, count);
            
            // Don't try to decompress on every write - wait for flush/dispose
        }

        private async Task HandleCompressedWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_compressedBuffer == null)
            {
                // Fallback: write directly to base stream
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
                return;
            }

            if (_compressionHandled)
            {
                // Already processed compression, write directly to base stream
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
                return;
            }

            // Append compressed data to buffer
            await _compressedBuffer.WriteAsync(buffer, offset, count, cancellationToken);
            
            // Don't try to decompress on every write - wait for flush/dispose
        }

        private bool TryDecompressAndInject()
        {
            if (_compressedBuffer == null || string.IsNullOrEmpty(_contentEncoding))
                return false;

            try
            {
                var compressedData = _compressedBuffer.ToArray();
                using var compressedStream = new MemoryStream(compressedData);
                using var decompressedStream = new MemoryStream();
                
                // Create decompression stream
                using var decompressionStream = _contentEncoding.ToLowerInvariant() switch
                {
                    "gzip" => new GZipStream(compressedStream, CompressionMode.Decompress) as Stream,
                    "br" or "brotli" => new BrotliStream(compressedStream, CompressionMode.Decompress) as Stream,
                    "deflate" => new DeflateStream(compressedStream, CompressionMode.Decompress) as Stream,
                    _ => null
                };

                if (decompressionStream == null)
                {
                    // Unknown compression, write original data
                    _baseStream.Write(compressedData);
                    return true;
                }

                // Decompress data
                decompressionStream.CopyTo(decompressedStream);
                var decompressedData = decompressedStream.ToArray();

                // Try to inject script into decompressed content
                ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, decompressedData);
                return true;
            }
            catch
            {
                // If decompression fails, write original compressed data
                var compressedData = _compressedBuffer.ToArray();
                _baseStream.Write(compressedData);
                return true;
            }
        }

        private async Task<bool> TryDecompressAndInjectAsync(CancellationToken cancellationToken)
        {
            if (_compressedBuffer == null || string.IsNullOrEmpty(_contentEncoding))
                return false;

            try
            {
                var compressedData = _compressedBuffer.ToArray();
                using var compressedStream = new MemoryStream(compressedData);
                using var decompressedStream = new MemoryStream();
                
                // Create decompression stream
                using var decompressionStream = _contentEncoding.ToLowerInvariant() switch
                {
                    "gzip" => new GZipStream(compressedStream, CompressionMode.Decompress) as Stream,
                    "br" or "brotli" => new BrotliStream(compressedStream, CompressionMode.Decompress) as Stream,
                    "deflate" => new DeflateStream(compressedStream, CompressionMode.Decompress) as Stream,
                    _ => null
                };

                if (decompressionStream == null)
                {
                    // Unknown compression, write original data
                    await _baseStream.WriteAsync(compressedData, cancellationToken);
                    return true;
                }

                // Decompress data
                await decompressionStream.CopyToAsync(decompressedStream, cancellationToken);
                var decompressedData = decompressedStream.ToArray();

                // Try to inject script into decompressed content
                ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, decompressedData, cancellationToken);
                return true;
            }
            catch
            {
                // If decompression fails, write original compressed data
                var compressedData = _compressedBuffer.ToArray();
                await _baseStream.WriteAsync(compressedData, cancellationToken);
                return true;
            }
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
            if (disposing)
            {
                // Handle any remaining compressed data
                if (_compressedBuffer != null && !_compressionHandled && _compressedBuffer.Length > 0)
                {
                    var success = TryDecompressAndInject();
                    _compressionHandled = true;
                }
                _compressedBuffer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
