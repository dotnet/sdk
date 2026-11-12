// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.NET.TestFramework;

internal sealed class ToolSearchTestServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _serverTask;
    private volatile bool _stopping;

    public ToolSearchTestServer()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        _listener.Start();
        _serverTask = ServeAsync();
    }

    public Uri BaseUri { get; }

    public void WriteConfig(string path)
    {
        new XDocument(
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new[] { "public", "corp" }.Select(name =>
                        new XElement("add", new XAttribute("key", name),
                            new XAttribute("value", new Uri(BaseUri, $"{name}/index.json").AbsoluteUri),
                            new XAttribute("allowInsecureConnections", "true")))),
                new XElement("packageSourceMapping",
                    new XElement("clear"),
                    new XElement("packageSource", new XAttribute("key", "public"),
                        new XElement("package", new XAttribute("pattern", "*"))),
                    new XElement("packageSource", new XAttribute("key", "corp"),
                        new XElement("package", new XAttribute("pattern", "Contoso.*"))),
                    new XElement("packageSource", new XAttribute("key", "disabled"),
                        new XElement("package", new XAttribute("pattern", "Contoso.Disabled.*"))))))
            .Save(path);
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true;
        _listener.Stop();
        _listener.Close();
        await _serverTask.ConfigureAwait(false);
    }

    private async Task ServeAsync()
    {
        try
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                string path = context.Request.Url!.AbsolutePath;
                string source = path.Split('/')[1];
                string response;
                if (path.EndsWith("index.json", StringComparison.Ordinal))
                {
                    response = $$"""
                        {"version":"3.0.0","resources":[
                          {"@id":"{{new Uri(BaseUri, $"{source}/query").AbsoluteUri}}","@type":"SearchQueryService/3.5.0"}
                        ]}
                        """;
                }
                else
                {
                    string data = source == "public"
                        ? string.Join(',', Package("Other.Tool", "1.0.0"), Package("Contoso.Tool", "9.0.0"),
                            Package("Contoso.Private.Tool", "9.0.0"), Package("Contoso.Disabled.Tool", "9.0.0"))
                        : string.Join(',', Package("Contoso.Tool", "1.0.0"), Package("Contoso.Disabled.Tool", "9.0.0"));
                    response = $$"""{"totalHits":4,"data":[{{data}}]}""";
                }

                byte[] content = Encoding.UTF8.GetBytes(response);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = content.Length;
                await context.Response.OutputStream.WriteAsync(content).ConfigureAwait(false);
                context.Response.Close();
            }
        }
        catch (HttpListenerException) when (_stopping)
        {
        }
        catch (ObjectDisposedException) when (_stopping)
        {
        }
    }

    private static string Package(string id, string version) => $$"""
        {"id":"{{id}}","version":"{{version}}","description":"A test tool",
         "authors":["Test"],"totalDownloads":10,"packageTypes":[{"name":"DotnetTool"}],
         "versions":[{"version":"{{version}}","downloads":10}]}
        """;
}
