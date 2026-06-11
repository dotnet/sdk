// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    [TestClass]
    public class BrowserScriptMiddlewareTest
    {
        private readonly RequestDelegate _next = (context) => Task.CompletedTask;
        private readonly ILogger<BrowserScriptMiddleware> _logger;

        public BrowserScriptMiddlewareTest()
        {
            var loggerFactory = LoggerFactory.Create(_ => { });
            _logger = loggerFactory.CreateLogger<BrowserScriptMiddleware>();
        }

        [TestMethod]
        public async Task InvokeAsync_ReturnsScript()
        {
            var context = new DefaultHttpContext();
            var stream = new MemoryStream();
            context.Response.Body = stream;
            var middleware = new BrowserScriptMiddleware(
                _next,
                new PathString("/script.js"),
                BrowserScriptMiddleware.GetWebSocketClientJavaScript("some-host", "test-key"),
                _logger);

            await middleware.InvokeAsync(context);

            stream.Position = 0;
            var script = Encoding.UTF8.GetString(stream.ToArray());
            StringAssert.Contains(script, "// dotnet-watch browser reload script");
            StringAssert.Contains(script, "'some-host'");
            StringAssert.Contains(script, "'test-key'");
        }

        [TestMethod]
        public async Task InvokeAsync_ConfiguresHeaders()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new BrowserScriptMiddleware(
                _next,
                new PathString("/script.js"),
                BrowserScriptMiddleware.GetWebSocketClientJavaScript("some-host", "test-key"),
                _logger);

            await middleware.InvokeAsync(context);

            var response = context.Response;
            var headers = response.Headers.OrderBy(h => h.Key).ToArray();
            Assert.HasCount(3, headers);
            Assert.AreEqual("Cache-Control", headers[0].Key);
            Assert.AreEqual("no-store", headers[0].Value.ToString());
            Assert.AreEqual("Content-Length", headers[1].Key);
            Assert.AreNotEqual(0, headers[1].Value.Count);
            Assert.AreEqual("Content-Type", headers[2].Key);
            Assert.AreEqual("application/javascript; charset=utf-8", headers[2].Value.ToString());
        }
    }
}
