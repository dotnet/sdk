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
        private MemoryStream? _compressedBuffer;

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
            _baseStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            OnWrite();
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            OnWrite();
            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                var processedBuffer = HandleGzipIfNeeded(buffer.ToArray());
                if (processedBuffer.Length > 0)
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, processedBuffer);
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
                var data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);
                var processedBuffer = HandleGzipIfNeeded(data);
                if (processedBuffer.Length > 0)
                {
                    ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, processedBuffer.AsSpan());
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
                var data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);
                var processedBuffer = await HandleGzipIfNeededAsync(data, cancellationToken);
                if (processedBuffer.Length > 0)
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, processedBuffer.AsMemory(), cancellationToken);
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
                var processedBuffer = await HandleGzipIfNeededAsync(buffer.ToArray(), cancellationToken);
                if (processedBuffer.Length > 0)
                {
                    ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, processedBuffer, cancellationToken);
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
                        _compressedBuffer = new MemoryStream();
                    }
                }
            }
        }

        private byte[] HandleGzipIfNeeded(byte[] buffer)
        {
            if (!_isGzipEncoded)
            {
                return buffer;
            }

            return ProcessCompressedData(buffer);
        }

        private async Task<byte[]> HandleGzipIfNeededAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (!_isGzipEncoded)
            {
                return buffer;
            }

            return await ProcessCompressedDataAsync(buffer, cancellationToken);
        }

        private byte[] ProcessCompressedData(byte[] data)
        {
            if (_compressedBuffer == null)
            {
                return data;
            }

            // Write incoming compressed data to the buffer
            _compressedBuffer.Write(data);

            // Try to decompress all accumulated data
            return TryDecompressAllData();
        }

        private async Task<byte[]> ProcessCompressedDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_compressedBuffer == null)
            {
                return data;
            }

            // Write incoming compressed data to the buffer
            await _compressedBuffer.WriteAsync(data, cancellationToken);

            // Try to decompress all accumulated data
            return TryDecompressAllData();
        }

        private byte[] TryDecompressAllData()
        {
            if (_compressedBuffer == null || _compressedBuffer.Length == 0)
                return Array.Empty<byte>();

            try
            {
                // Create a new MemoryStream with the compressed data for reading
                var compressedData = _compressedBuffer.ToArray();
                using var compressedStream = new MemoryStream(compressedData);
                using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();
                
                gzipStream.CopyTo(decompressedStream);
                var result = decompressedStream.ToArray();
                
                // Clear the compressed buffer since we've successfully processed the data
                _compressedBuffer.SetLength(0);
                
                return result;
            }
            catch
            {
                // Decompression failed, likely because we don't have complete data yet
                // Return empty array to indicate no data is ready for processing yet
                return Array.Empty<byte>();
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
                _compressedBuffer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
