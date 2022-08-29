// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Based on https://github.com/RickStrahl/Westwind.AspnetCore.LiveReload/blob/128b5f524e86954e997f2c453e7e5c1dcc3db746/Westwind.AspnetCore.LiveReload/ResponseStreamWrapper.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private static readonly MediaTypeHeaderValue _textHtmlMediaType = new MediaTypeHeaderValue("text/html");
        private readonly MemoryStream _internalStream;
        private readonly Stream _baseStream;
        private readonly HttpContext _context;
        private readonly ILogger _logger;
        private bool? _isHtmlResponse;

        public ResponseStreamWrapper(HttpContext context, ILogger logger)
        {
            _context = context;
            _internalStream = new MemoryStream();
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
            OnFlush();

            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                ScriptInjectionPerformed = WebSocketScriptInjection.TryInjectLiveReloadScript(_baseStream, _internalStream.GetBuffer());

                if (!ScriptInjectionPerformed)
                {
                    // The script was not injected, so copy the internal buffer directly to the base stream.
                    _internalStream.CopyTo(_baseStream);
                }
            }
            else
            {
                // Either script injection was already performed or we're not dealing with an HTML response.
                _internalStream.CopyTo(_baseStream);
            }

            _internalStream.SetLength(0);
            _baseStream.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            OnFlush();

            if (IsHtmlResponse && !ScriptInjectionPerformed)
            {
                ScriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(_baseStream, _internalStream.GetBuffer());

                if (!ScriptInjectionPerformed)
                {
                    // The script was not injected, so copy the internal buffer directly to the base stream.
                    await _internalStream.CopyToAsync(_baseStream);
                }
            }
            else
            {
                // Either script injection was already performed or we're not dealing with an HTML response.
                await _internalStream.CopyToAsync(_baseStream);
            }

            _internalStream.SetLength(0);
            await _baseStream.FlushAsync(cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
            => _internalStream.Write(buffer);

        public override void WriteByte(byte value)
            => _internalStream.WriteByte(value);

        public override void Write(byte[] buffer, int offset, int count)
            => _internalStream.Write(buffer, offset, count);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await _internalStream.WriteAsync(buffer, offset, count, cancellationToken);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => await _internalStream.WriteAsync(buffer, cancellationToken);

        private void OnFlush()
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
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Flush();
            _internalStream.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await FlushAsync();
            await _internalStream.DisposeAsync();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
             => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
             => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
