// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class ResponseStreamWrapperCompressionTest
    {
        [Fact]
        public async Task WriteAsync_HandlesGzipCompressedHtmlResponse()
        {
            // Arrange
            var htmlContent = "<html><body><h1>Hello world</h1></body></html>";
            var compressedData = CompressWithGzip(htmlContent);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "text/html",
                    StatusCode = 200,
                    Headers = { [HeaderNames.ContentEncoding] = "gzip" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(compressedData);
            await wrapper.FlushAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task WriteAsync_HandlesBrotliCompressedHtmlResponse()
        {
            // Arrange
            var htmlContent = "<html><body><h1>Hello world</h1></body></html>";
            var compressedData = CompressWithBrotli(htmlContent);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "text/html",
                    StatusCode = 200,
                    Headers = { [HeaderNames.ContentEncoding] = "br" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(compressedData);
            await wrapper.FlushAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task WriteAsync_HandlesDeflateCompressedHtmlResponse()
        {
            // Arrange
            var htmlContent = "<html><body><h1>Hello world</h1></body></html>";
            var compressedData = CompressWithDeflate(htmlContent);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "text/html",
                    StatusCode = 200,
                    Headers = { [HeaderNames.ContentEncoding] = "deflate" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(compressedData);
            await wrapper.FlushAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task WriteAsync_DoesNotModifyNonHtmlCompressedResponse()
        {
            // Arrange
            var content = "{ \"test\": \"json\" }";
            var compressedData = CompressWithGzip(content);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "application/json",
                    StatusCode = 200,
                    Headers = { [HeaderNames.ContentEncoding] = "gzip" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(compressedData);
            await wrapper.FlushAsync();

            // Assert
            var result = outputStream.ToArray();
            Assert.Equal(compressedData, result);
            Assert.True(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task WriteAsync_HandlesUnknownCompressionGracefully()
        {
            // Arrange
            var htmlContent = "<html><body><h1>Hello world</h1></body></html>";
            var data = Encoding.UTF8.GetBytes(htmlContent);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "text/html",
                    StatusCode = 200,
                    Headers = { [HeaderNames.ContentEncoding] = "unknown-compression" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(data);
            await wrapper.FlushAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            // Should fall back to writing original data
            Assert.Equal(htmlContent, result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task WriteAsync_PreservesNonCompressedHtmlResponse()
        {
            // Arrange
            var htmlContent = "<html><body><h1>Hello world</h1></body></html>";
            var data = Encoding.UTF8.GetBytes(htmlContent);
            var outputStream = new MemoryStream();
            
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = outputStream,
                    ContentType = "text/html",
                    StatusCode = 200
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(data);
            await wrapper.FlushAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.Null(context.Response.Headers.ContentLength);
        }

        private static byte[] CompressWithGzip(string content)
        {
            var data = Encoding.UTF8.GetBytes(content);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data);
            }
            return output.ToArray();
        }

        private static byte[] CompressWithBrotli(string content)
        {
            var data = Encoding.UTF8.GetBytes(content);
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, CompressionMode.Compress))
            {
                brotli.Write(data);
            }
            return output.ToArray();
        }

        private static byte[] CompressWithDeflate(string content)
        {
            var data = Encoding.UTF8.GetBytes(content);
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionMode.Compress))
            {
                deflate.Write(data);
            }
            return output.ToArray();
        }
    }
}