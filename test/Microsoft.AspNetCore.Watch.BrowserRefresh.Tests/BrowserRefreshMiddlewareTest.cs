// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BrowserRefreshMiddlewareTest
    {
        [Theory]
        [InlineData("DELETE")]
        [InlineData("head")]
        [InlineData("Put")]
        public void IsBrowserDocumentRequest_ReturnsFalse_ForNonGetOrPostRequests(string method)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = method,
                    Headers =
                    {
                        ["Accept"] = "application/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsFalse_IsRequestDoesNotAcceptHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "application/xml",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_ForGetRequestsThatAcceptHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "application/json,text/html;q=0.9",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_ForRequestsThatAcceptAnyHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "application/json,text/*+html;q=0.9",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestDoesNotHaveFetchMetadataRequestHeader()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsEmpty()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = string.Empty,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("document")]
        [InlineData("Document")]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsDocument(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("frame")]
        [InlineData("iframe")]
        [InlineData("serviceworker")]
        public void IsBrowserDocumentRequest_ReturnsFalse_IfRequestFetchMetadataRequestHeaderIsNotDocument(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("DELETE")]
        [InlineData("POST")]
        [InlineData("head")]
        [InlineData("Put")]
        public void IsWebassemblyBootRequest_ReturnsFalse_ForNonGetRequests(string method)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = method,
                    Headers =
                    {
                        ["Accept"] = "application/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWebassemblyBootRequest_ReturnsFalse_IfRequestDoesNotAcceptJson()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "text/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWebassemblyBootRequest_ReturnsTrue_ForGetRequestsThatAcceptJson()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "text/html,application/json;q=0.9",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWebassemblyBootRequest_ReturnsTrue_ForGetRequestsThatAcceptAnyContentType()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "*/*",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("/_framework/blazor.boot.json")]
        [InlineData("/Blazor.boot.json")]
        public void IsWebassemblyBootRequest_ReturnsTrue_ForFileNameRequestsToBlazorBootJson(string path)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = path,
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("/_framework/other.txt")]
        [InlineData("/other.txt")]
        [InlineData("/Blazor.boot.json/other.txt")]
        public void IsWebassemblyBootRequest_ReturnsFalse_ForRequestsToOtherPathsThanBlazorBootJson(string path)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = path,
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWebassemblyBootRequest_ReturnsTrue_IfRequestDoesNotHaveFetchMetadataRequestHeader()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json"
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWebassemblyBootRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsEmpty()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json",
                        ["Sec-Fetch-Dest"] = string.Empty,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("empty")]
        [InlineData("Empty")]
        public void IsWebassemblyBootRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsEmptyValue(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("frame")]
        [InlineData("iframe")]
        [InlineData("serviceworker")]
        [InlineData("document")]
        public void IsWebassemblyBootRequest_ReturnsFalse_IfRequestFetchMetadataRequestHeaderIsEmptyValue(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = HttpMethods.Get,
                    Headers =
                    {
                        ["Accept"] = "application/json",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsWebAssemblyBootRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task InvokeAsync_AttachesHeadersToResponse()
        {
            var stream = new MemoryStream();
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = "GET",
                    Headers = { ["Accept"] = "application/json" },
                },
                Response =
                {
                    Body = stream
                },
            };

            var response = new TestHttpResponseFeature
            {
                Body = stream,
                Headers = new HeaderDictionary()
            };
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpResponseBodyFeature>(response);

            var middleware = new BrowserRefreshMiddleware(async (context) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.StartAsync();
                await context.Response.WriteAsync("{ }");
            }, NullLogger<BrowserRefreshMiddleware>.Instance);

            middleware.Test_SetEnvironment(dotnetModifiableAssemblies: "true", aspnetcoreBrowserTools: "true");

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("DOTNET-MODIFIABLE-ASSEMBLIES"));
            Assert.True(context.Response.Headers.ContainsKey("ASPNETCORE-BROWSER-TOOLS"));
        }

        [Fact]
        public async Task InvokeAsync_DoesNotAttachHeaders_WhenAlreadyAttached()
        {
            var stream = new MemoryStream();
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Path = "/_framework/blazor.boot.json",
                    Method = "GET",
                    Headers = { ["Accept"] = "application/json" },
                },
                Response =
                {
                    Body = stream
                },
            };

            var response = new TestHttpResponseFeature
            {
                Body = stream,
                Headers = new HeaderDictionary()
            };
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpResponseBodyFeature>(response);

            var middleware = new BrowserRefreshMiddleware(async (context) =>
            {

                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("DOTNET-MODIFIABLE-ASSEMBLIES", "true");
                context.Response.Headers.Append("ASPNETCORE-BROWSER-TOOLS", "true");
                await context.Response.StartAsync();
                await context.Response.WriteAsync("{ }");
            }, NullLogger<BrowserRefreshMiddleware>.Instance);

            middleware.Test_SetEnvironment(dotnetModifiableAssemblies: "true", aspnetcoreBrowserTools: "true");

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("DOTNET-MODIFIABLE-ASSEMBLIES"));
            Assert.Equal("true", context.Response.Headers["DOTNET-MODIFIABLE-ASSEMBLIES"]);
            Assert.True(context.Response.Headers.ContainsKey("ASPNETCORE-BROWSER-TOOLS"));
            Assert.Equal("true", context.Response.Headers["ASPNETCORE-BROWSER-TOOLS"]);
        }

        [Fact]
        public async Task InvokeAsync_AddsScriptToThePage()
        {
            // Arrange
            var stream = new MemoryStream();
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers = { ["Accept"] = "text/html" },
                },
                Response =
                {
                    Body = stream
                },
            };

            var middleware = new BrowserRefreshMiddleware(async (context) =>
            {

                context.Response.ContentType = "text/html";

                await context.Response.WriteAsync("<html>");
                await context.Response.WriteAsync("<body>");
                await context.Response.WriteAsync("<h1>");
                await context.Response.WriteAsync("Hello world");
                await context.Response.WriteAsync("</h1>");
                await context.Response.WriteAsync("</body>");
                await context.Response.WriteAsync("</html>");
            }, NullLogger<BrowserRefreshMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var responseContent = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("<html><body><h1>Hello world</h1><script src=\"/_framework/aspnetcore-browser-refresh.js\"></script></body></html>", responseContent);
        }

        private class TestHttpResponseFeature : IHttpResponseFeature, IHttpResponseBodyFeature
        {
            private (Func<object, Task> callback, object state)[] _callbacks = [];
            private bool _hasStarted;

            public int StatusCode { get; set; }
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = new MemoryStream();

            public bool HasStarted => _hasStarted;

            public Stream Stream => Body;

            public PipeWriter Writer => PipeWriter.Create(Body);

            public Task CompleteAsync() => Task.CompletedTask;

            public void DisableBuffering() { }

            public void OnCompleted(Func<object, Task> callback, object state) => throw new NotImplementedException();

            public void OnStarting(Func<object, Task> callback, object state)
            {
                _callbacks = [(callback, state)];
            }

            public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                if(_hasStarted)
                {
                    throw new InvalidOperationException();
                }

                foreach (var (callback, state) in _callbacks)
                {
                    await callback(state);
                }

                await Stream.FlushAsync();

                _hasStarted = true;
            }
        }
    }
}
