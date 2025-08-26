// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
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
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task Write_HandlesGzipCompressedHtmlResponse()
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
            wrapper.Write(compressedData, 0, compressedData.Length);
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
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
            await wrapper.CompleteAsync();

            var result = outputStream.ToArray();
            Assert.Equal(compressedData, result);
            Assert.True(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task Write_DoesNotModifyNonHtmlCompressedResponse()
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
            wrapper.Write(compressedData, 0, compressedData.Length);
            await wrapper.CompleteAsync();

            var result = outputStream.ToArray();
            Assert.Equal(compressedData, result);
            Assert.True(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task WriteAsync_IgnoresNonGzipCompressionTypes()
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
                    Headers = { [HeaderNames.ContentEncoding] = "br" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            await wrapper.WriteAsync(data);
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            // Should treat as regular data since we only handle gzip
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.True(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
        }

        [Fact]
        public async Task Write_IgnoresNonGzipCompressionTypes()
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
                    Headers = { [HeaderNames.ContentEncoding] = "br" }
                }
            };

            var wrapper = new ResponseStreamWrapper(context, NullLogger.Instance);

            // Act
            wrapper.Write(data, 0, data.Length);
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            // Should treat as regular data since we only handle gzip
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.True(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
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
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task Write_PreservesNonCompressedHtmlResponse()
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
            wrapper.Write(data, 0, data.Length);
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task WriteAsync_GzipHtml_SingleByteChunks()
        {
            // Arrange: small HTML so compressed output is reasonably small; we will feed one byte at a time.
            var htmlContent = "<html><body><h1>Hello world single byte chunks</h1></body></html>";
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

            // Act: write one byte at a time
            foreach (var b in compressedData)
            {
                var single = new byte[] { b };
                await wrapper.WriteAsync(single, 0, 1, CancellationToken.None);
            }
            await wrapper.CompleteAsync();

            // Assert: script injected and content encoding removed
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task Write_GzipHtml_SingleByteChunks()
        {
            // Arrange: small HTML so compressed output is reasonably small; we will feed one byte at a time.
            var htmlContent = "<html><body><h1>Hello world single byte chunks</h1></body></html>";
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

            // Act: write one byte at a time
            foreach (var b in compressedData)
            {
                var single = new byte[] { b };
                wrapper.Write(single, 0, 1);
            }
            await wrapper.CompleteAsync();

            // Assert: script injected and content encoding removed
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task WriteAsync_GzipHtml_LargeChunk32K()
        {
            // Arrange: generate largely incompressible-ish HTML body so compressed data spans >= 32K
            var sb = new StringBuilder();
            sb.Append("<html><body>");
            for (int i = 0; i < 1500; i++) // ~1500 * (36+20) ≈ 84K chars before tags close
            {
                sb.Append(Guid.NewGuid().ToString());
                sb.Append('<'); sb.Append('p'); sb.Append('>');
                sb.Append(i);
                sb.Append("</p>");
            }
            sb.Append("</body></html>");
            var htmlContent = sb.ToString();
            var compressedData = CompressWithGzip(htmlContent);
            Assert.True(compressedData.Length > 0, "Expected non-empty compressed payload");
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

            // Act: write in 32K (32768) byte chunks (last chunk may be smaller)
            const int chunkSize = 32 * 1024;
            int offset = 0;
            while (offset < compressedData.Length)
            {
                var toWrite = Math.Min(chunkSize, compressedData.Length - offset);
                await wrapper.WriteAsync(compressedData, offset, toWrite, CancellationToken.None);
                offset += toWrite;
            }
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.Contains("</body></html>", result); // Ensure full doc present
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
            Assert.Null(context.Response.Headers.ContentLength);
        }

        [Fact]
        public async Task Write_GzipHtml_LargeChunk32K()
        {
            // Arrange: generate largely incompressible-ish HTML body so compressed data spans >= 32K
            var sb = new StringBuilder();
            sb.Append("<html><body>");
            for (int i = 0; i < 1500; i++) // ~1500 * (36+20) ≈ 84K chars before tags close
            {
                sb.Append(Guid.NewGuid().ToString());
                sb.Append('<'); sb.Append('p'); sb.Append('>');
                sb.Append(i);
                sb.Append("</p>");
            }
            sb.Append("</body></html>");
            var htmlContent = sb.ToString();
            var compressedData = CompressWithGzip(htmlContent);
            Assert.True(compressedData.Length > 0, "Expected non-empty compressed payload");
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

            // Act: write in 32K (32768) byte chunks (last chunk may be smaller)
            const int chunkSize = 32 * 1024;
            int offset = 0;
            while (offset < compressedData.Length)
            {
                var toWrite = Math.Min(chunkSize, compressedData.Length - offset);
                wrapper.Write(compressedData, offset, toWrite);
                offset += toWrite;
            }
            await wrapper.CompleteAsync();

            // Assert
            var result = Encoding.UTF8.GetString(outputStream.ToArray());
            Assert.Contains("<script src=\"/_framework/aspnetcore-browser-refresh.js\"></script>", result);
            Assert.Contains("</body></html>", result); // Ensure full doc present
            Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ContentEncoding));
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
    }
}
