// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Microsoft.TemplateEngine.Edge.UnitTests.Mocks
{
    internal sealed class MockNuGetFeed : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly Task _serverTask;
        private readonly string _metadataId;

        internal MockNuGetFeed(string metadataId)
        {
            _metadataId = metadataId;
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseAddress = $"http://127.0.0.1:{port}/";
            _serverTask = ServeAsync();
        }

        internal string BaseAddress { get; }

        internal ConcurrentQueue<string> Requests { get; } = new ConcurrentQueue<string>();

        internal byte[] PackageContent { get; } = Encoding.UTF8.GetBytes("downloaded package");

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.GetAwaiter().GetResult();
            }
            finally
            {
                _cancellation.Dispose();
            }
        }

        private async Task ServeAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using CancellationTokenRegistration registration = _cancellation.Token.Register(client.Close);
                    using NetworkStream stream = client.GetStream();
                    using StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                    string? requestLine = await reader.ReadLineAsync();
                    if (requestLine is null)
                    {
                        continue;
                    }

                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
                    {
                    }

                    string[] requestParts = requestLine.Split(' ');
                    if (requestParts.Length != 3
                        || requestParts[0] != "GET"
                        || !Uri.TryCreate(new Uri(BaseAddress), requestParts[1], out Uri? requestUri))
                    {
                        WriteResponse(stream, HttpStatusCode.BadRequest, Encoding.UTF8.GetBytes("Invalid HTTP request."), "text/plain");
                        continue;
                    }

                    string path = requestUri.AbsolutePath;
                    Requests.Enqueue(path);
                    if (path.StartsWith("/flatcontainer/", StringComparison.Ordinal)
                        && path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteResponse(stream, HttpStatusCode.OK, PackageContent, "application/octet-stream");
                    }
                    else if (GetResponse(path) is string response)
                    {
                        WriteResponse(stream, HttpStatusCode.OK, Encoding.UTF8.GetBytes(response), "application/json");
                    }
                    else
                    {
                        WriteResponse(stream, HttpStatusCode.NotFound, Encoding.UTF8.GetBytes("Unknown feed endpoint."), "text/plain");
                    }
                }
            }
            catch (SocketException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (IOException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        private static void WriteResponse(NetworkStream stream, HttpStatusCode status, byte[] content, string contentType)
        {
            byte[] headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {status}\r\nContent-Type: {contentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");
            stream.Write(headers, 0, headers.Length);
            stream.Write(content, 0, content.Length);
        }

        private string? GetResponse(string path)
        {
            if (path.StartsWith("/flatcontainer/", StringComparison.Ordinal)
                && path.EndsWith("/index.json", StringComparison.Ordinal))
            {
                return """{"versions": ["1.0.0"]}""";
            }
            string id = JsonSerializer.Serialize(_metadataId);
            if (path.StartsWith("/registration/", StringComparison.Ordinal))
            {
                return $$"""
                    {
                      "@id": "{{BaseAddress}}registration/templates/index.json",
                      "count": 1,
                      "items": [{
                        "@id": "{{BaseAddress}}registration/templates/page.json",
                        "count": 1,
                        "lower": "1.0.0",
                        "upper": "1.0.0",
                        "items": [{
                          "@id": "{{BaseAddress}}registration/templates/1.0.0.json",
                          "catalogEntry": {
                            "@id": "{{BaseAddress}}catalog/package.json",
                            "id": {{id}},
                            "version": "1.0.0",
                            "authors": "Test",
                            "description": "Test package",
                            "listed": true
                          },
                          "packageContent": "{{BaseAddress}}flatcontainer/templates/1.0.0/templates.1.0.0.nupkg"
                        }]
                      }]
                    }
                    """;
            }
            if (path == "/query")
            {
                return $$"""
                    {"totalHits": 1, "data": [{"id": "Templates", "version": "1.0.0", "owners": ["Test"]}]}
                    """;
            }
            if (path != "/index.json")
            {
                return null;
            }
            return $$"""
                {
                  "version": "3.0.0",
                  "resources": [
                    {"@id": "{{BaseAddress}}registration/", "@type": "RegistrationsBaseUrl/3.6.0"},
                    {"@id": "{{BaseAddress}}query", "@type": "SearchQueryService/3.4.0"},
                    {"@id": "{{BaseAddress}}flatcontainer/", "@type": "PackageBaseAddress/3.0.0"}
                  ]
                }
                """;
        }
    }
}
