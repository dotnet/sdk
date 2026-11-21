// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    [TestClass]
    public class WorkloadPackageSourceValidatorTests
    {
        public TestContext TestContext { get; set; } = null!;

        private const string V2ServiceDocument =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <service xml:base="https://www.nuget.org/api/v2" xmlns="http://www.w3.org/2007/app" xmlns:atom="http://www.w3.org/2005/Atom">
              <workspace><atom:title type="text">Default</atom:title><collection href="Packages"><atom:title type="text">Packages</atom:title></collection></workspace>
            </service>
            """;

        private const string HtmlPage = "<!DOCTYPE html><html><head><title>Not a feed</title></head><body></body></html>";

        [TestMethod]
        [DataRow(V2ServiceDocument, true)]
        [DataRow("<service xmlns=\"http://www.w3.org/2007/app\" />", true)]
        [DataRow(HtmlPage, false)]
        [DataRow("<html><body>Hello</body></html>", false)]
        [DataRow("{\"version\": \"3.0.0\", \"resources\": []}", false)]
        [DataRow("", false)]
        public void ItRecognizesODataServiceDocuments(string content, bool expected)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            WorkloadPackageSourceValidator.IsODataServiceDocument(stream).Should().Be(expected);
        }

        [TestMethod]
        public void ItTreatsAMissingResponseBodyAsNotAServiceDocument()
        {
            WorkloadPackageSourceValidator.IsODataServiceDocument(null).Should().BeFalse();
        }

        [TestMethod]
        public async Task ItAcceptsAV2Feed()
        {
            using var server = new TestHttpServer(HttpStatusCode.OK, "application/xml", V2ServiceDocument);

            await WorkloadPackageSourceValidator.ValidateAsync([server.Url], TestContext.CancellationToken);
        }

        [TestMethod]
        public async Task ItRejectsAWebPageThatIsNotAV2Feed()
        {
            using var server = new TestHttpServer(HttpStatusCode.OK, "text/html", HtmlPage);

            var exception = await Assert.ThrowsExactlyAsync<GracefulException>(() => WorkloadPackageSourceValidator.ValidateAsync([server.Url], TestContext.CancellationToken));

            exception.Message.Should().Be(string.Format(CliCommandStrings.WorkloadPackageSourceIsNotANuGetFeed, server.Url));
            exception.IsUserError.Should().BeTrue();
        }

        [TestMethod]
        public async Task ItRejectsAV3SourceThatIsNotAServiceIndex()
        {
            using var server = new TestHttpServer(HttpStatusCode.OK, "text/html", HtmlPage);
            var source = server.Url + "index.json";

            var exception = await Assert.ThrowsExactlyAsync<GracefulException>(() => WorkloadPackageSourceValidator.ValidateAsync([source], TestContext.CancellationToken));

            exception.Message.Should().StartWith(string.Format(CliCommandStrings.WorkloadPackageSourceCouldNotBeLoaded, source, string.Empty).TrimEnd());
        }

        [TestMethod]
        public async Task ItRejectsASourceThatReturnsAnError()
        {
            using var server = new TestHttpServer(HttpStatusCode.NotFound, "text/html", HtmlPage);

            var exception = await Assert.ThrowsExactlyAsync<GracefulException>(() => WorkloadPackageSourceValidator.ValidateAsync([server.Url], TestContext.CancellationToken));

            exception.Message.Should().StartWith(string.Format(CliCommandStrings.WorkloadPackageSourceCouldNotBeLoaded, server.Url, string.Empty).TrimEnd());
        }

        [TestMethod]
        public async Task ItDoesNotValidateLocalFolderSources()
        {
            var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            await WorkloadPackageSourceValidator.ValidateAsync([folder, " ", null!], TestContext.CancellationToken);
        }

        /// <summary>
        ///  Minimal loopback HTTP server that returns the same response for every request.
        /// </summary>
        private sealed class TestHttpServer : IDisposable
        {
            private readonly HttpListener _listener = new();
            private readonly Task _serveTask;

            public TestHttpServer(HttpStatusCode statusCode, string contentType, string body)
            {
                Url = $"http://localhost:{GetFreePort()}/";
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _serveTask = Task.Run(async () =>
                {
                    var bytes = Encoding.UTF8.GetBytes(body);
                    while (_listener.IsListening)
                    {
                        HttpListenerContext context;
                        try
                        {
                            context = await _listener.GetContextAsync();
                        }
                        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                        {
                            return;
                        }

                        context.Response.StatusCode = (int)statusCode;
                        context.Response.ContentType = contentType;
                        await context.Response.OutputStream.WriteAsync(bytes);
                        context.Response.Close();
                    }
                });
            }

            public string Url { get; }

            public void Dispose()
            {
                _listener.Stop();
                _listener.Close();
                _serveTask.Wait(TimeSpan.FromSeconds(5));
            }

            private static int GetFreePort()
            {
                using var socket = new TcpListener(IPAddress.Loopback, 0);
                socket.Start();
                return ((IPEndPoint)socket.LocalEndpoint).Port;
            }
        }
    }
}
