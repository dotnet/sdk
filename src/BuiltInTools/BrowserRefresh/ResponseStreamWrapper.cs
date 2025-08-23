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
        private bool _isGzipEncoded;
        private MemoryStream? _gzipInputBuffer;
        private MemoryStream? _decompressedBuffer;

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
            if (_isGzipEncoded && _gzipInputBuffer != null)
            {
                ProcessGzipData(isFlush: true);
            }
            _baseStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            OnWrite();
            if (_isGzipEncoded && _gzipInputBuffer != null)
            {
                ProcessGzipData(isFlush: true);
            }
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            OnWrite();
            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                if (_isGzipEncoded)
                {
                    HandleGzipWrite(buffer.ToArray(), 0, buffer.Length);
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
                if (_isGzipEncoded)
                {
                    HandleGzipWrite(buffer, offset, count);
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
                if (_isGzipEncoded)
                {
                    await HandleGzipWriteAsync(buffer, offset, count, cancellationToken);
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
                if (_isGzipEncoded)
                {
                    var tempBuffer = buffer.ToArray();
                    await HandleGzipWriteAsync(tempBuffer, 0, tempBuffer.Length, cancellationToken);
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

                // Check if the response has gzip Content-Encoding
                if (response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodingValues))
                {
                    var contentEncoding = contentEncodingValues.FirstOrDefault();
                    if (string.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        _isGzipEncoded = true;
                        // Remove the Content-Encoding header since we'll be serving uncompressed content
                        response.Headers.Remove(HeaderNames.ContentEncoding);
                        
                        // Initialize streams for gzip processing
                        _gzipInputBuffer = new MemoryStream();
                        _decompressedBuffer = new MemoryStream();
                    }
                }
            }
        }

        private void HandleGzipWrite(byte[] buffer, int offset, int count)
        {
            if (_gzipInputBuffer == null)
            {
                // Fallback: write directly to base stream
                _baseStream.Write(buffer, offset, count);
                return;
            }

            // Add compressed data to input buffer
            _gzipInputBuffer.Write(buffer, offset, count);
            
            // Try to process gzip data on every write
            TryProcessGzipData();
        }

        private async Task HandleGzipWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_gzipInputBuffer == null)
            {
                // Fallback: write directly to base stream
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
                return;
            }

            // Add compressed data to input buffer
            await _gzipInputBuffer.WriteAsync(buffer, offset, count, cancellationToken);
            
            // Try to process gzip data on every write
            TryProcessGzipData();
        }

        private void TryProcessGzipData()
        {
            if (_gzipInputBuffer == null || _decompressedBuffer == null || _gzipInputBuffer.Length == 0)
                return;

            try
            {
                // Position at start for reading
                _gzipInputBuffer.Position = 0;

                // Try to decompress the data we have so far
                using var gzipStream = new GZipStream(_gzipInputBuffer, CompressionMode.Decompress, leaveOpen: true);
                
                var buffer = new byte[4096];
                int totalBytesRead = 0;
                int bytesRead;
                while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    _decompressedBuffer.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }

                // If we successfully decompressed data and have the complete stream
                if (totalBytesRead > 0)
                {
                    var decompressedData = _decompressedBuffer.ToArray();
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, decompressedData);
                    
                    // Clear the buffers since we've successfully processed the data
                    _decompressedBuffer.SetLength(0);
                    _gzipInputBuffer.SetLength(0);
                }
            }
            catch
            {
                // Decompression failed, likely because we don't have complete data yet
                // This is expected for partial writes - just continue
            }
        }

        private void ProcessGzipData(bool isFlush)
        {
            if (_gzipInputBuffer == null || _decompressedBuffer == null)
                return;

            try
            {
                // Position at start for reading
                _gzipInputBuffer.Position = 0;

                // Try to decompress the data we have so far
                using var gzipStream = new GZipStream(_gzipInputBuffer, CompressionMode.Decompress, leaveOpen: true);
                
                var buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    _decompressedBuffer.Write(buffer, 0, bytesRead);
                }

                // If we have decompressed data, try script injection
                if (_decompressedBuffer.Length > 0)
                {
                    var decompressedData = _decompressedBuffer.ToArray();
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, decompressedData);
                    
                    // Clear the buffers since we've processed the data
                    _decompressedBuffer.SetLength(0);
                    _gzipInputBuffer.SetLength(0);
                }
            }
            catch
            {
                // If decompression fails and this is a flush, write original data
                if (isFlush && _gzipInputBuffer.Length > 0)
                {
                    _gzipInputBuffer.Position = 0;
                    _gzipInputBuffer.CopyTo(_baseStream);
                    _gzipInputBuffer.SetLength(0);
                }
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
                // Handle any remaining gzip data
                if (_isGzipEncoded && _gzipInputBuffer != null && _gzipInputBuffer.Length > 0)
                {
                    ProcessGzipData(isFlush: true);
                }
                
                _gzipInputBuffer?.Dispose();
                _decompressedBuffer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
