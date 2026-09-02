// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

[TestClass]
public class BrowserToolsForwarderTest
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ForwardHttpAsync_PreservesRequestAndResponse()
    {
        var requestObserved = new TaskCompletionSource<HttpRequestObservation>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var provider = await StartApplicationAsync(async context =>
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            requestObserved.SetResult(new(
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString.Value,
                context.Request.Headers["X-Browser-Tools-Test"].ToString(),
                context.Request.ContentType,
                await reader.ReadToEndAsync(TestContext.CancellationToken)));

            context.Response.StatusCode = StatusCodes.Status202Accepted;
            context.Response.ContentType = "application/test";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Clear-Site-Data"] = "\"cache\"";
            context.Response.Headers.ContentLanguage = "en-US";
            await context.Response.WriteAsync("forwarded response", TestContext.CancellationToken);
        });

        await using var application = await StartForwarderAsync(GetAddress(provider));
        using var client = new HttpClient { BaseAddress = GetAddress(application) };
        using var content = new StringContent("request body", Encoding.UTF8, "application/test");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/_framework/dotnet-browser-tools/session.json?name=value%20with%20space")
        {
            Content = content,
        };
        request.Headers.Add("X-Browser-Tools-Test", "preserved");

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.CancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var observation = await requestObserved.Task.WaitAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.AreEqual("application/test", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("en-US", response.Content.Headers.ContentLanguage.Single());
        Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
        Assert.AreEqual("\"cache\"", response.Headers.GetValues("Clear-Site-Data").Single());
        Assert.AreEqual("forwarded response", responseBody);
        Assert.AreEqual(HttpMethods.Post, observation.Method);
        Assert.AreEqual("/_framework/dotnet-browser-tools/session.json", observation.Path);
        Assert.AreEqual("?name=value%20with%20space", observation.QueryString);
        Assert.AreEqual("preserved", observation.TestHeader);
        Assert.AreEqual("application/test; charset=utf-8", observation.ContentType);
        Assert.AreEqual("request body", observation.Body);
    }

    [TestMethod]
    public async Task ForwardWebSocketAsync_PreservesSubprotocolFramesAndClose()
    {
        var providerObserved = new TaskCompletionSource<WebSocketObservation>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var provider = await StartApplicationAsync(async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync("encrypted-secret");
            var buffer = new byte[128];
            var payload = new List<byte>();
            var endOfMessageValues = new List<bool>();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                payload.AddRange(buffer.AsSpan(0, result.Count).ToArray());
                endOfMessageValues.Add(result.EndOfMessage);
            }
            while (!result.EndOfMessage);

            providerObserved.SetResult(new(
                socket.SubProtocol,
                result.MessageType,
                endOfMessageValues,
                Encoding.UTF8.GetString([.. payload])));

            await socket.SendAsync(
                Encoding.UTF8.GetBytes("provider response"),
                WebSocketMessageType.Text,
                endOfMessage: true,
                context.RequestAborted);
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "provider complete",
                context.RequestAborted);
        });

        await using var application = await StartForwarderAsync(GetAddress(provider));
        using var browserSocket = new ClientWebSocket();
        browserSocket.Options.AddSubProtocol("encrypted-secret");
        var webSocketAddress = new UriBuilder(GetAddress(application))
        {
            Scheme = "ws",
            Path = "/_framework/dotnet-browser-tools/connect",
        }.Uri;

        await browserSocket.ConnectAsync(webSocketAddress, TestContext.CancellationToken);
        await browserSocket.SendAsync(
            Encoding.UTF8.GetBytes("browser "),
            WebSocketMessageType.Text,
            endOfMessage: false,
            TestContext.CancellationToken);
        await browserSocket.SendAsync(
            Encoding.UTF8.GetBytes("request"),
            WebSocketMessageType.Text,
            endOfMessage: true,
            TestContext.CancellationToken);

        var responseBuffer = new byte[128];
        var response = await browserSocket.ReceiveAsync(responseBuffer, TestContext.CancellationToken);
        var close = await browserSocket.ReceiveAsync(responseBuffer, TestContext.CancellationToken);
        await browserSocket.CloseOutputAsync(
            close.CloseStatus!.Value,
            close.CloseStatusDescription,
            TestContext.CancellationToken);
        var observation = await providerObserved.Task.WaitAsync(TestContext.CancellationToken);

        Assert.AreEqual("encrypted-secret", browserSocket.SubProtocol);
        Assert.AreEqual("provider response", Encoding.UTF8.GetString(responseBuffer, 0, response.Count));
        Assert.AreEqual(WebSocketMessageType.Close, close.MessageType);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, close.CloseStatus);
        Assert.AreEqual("provider complete", close.CloseStatusDescription);
        Assert.AreEqual("encrypted-secret", observation.SubProtocol);
        Assert.AreEqual(WebSocketMessageType.Text, observation.MessageType);
        Assert.AreSequenceEqual([false, true], observation.EndOfMessageValues);
        Assert.AreEqual("browser request", observation.Payload);
    }

    private static async Task<WebApplication> StartForwarderAsync(Uri providerAddress)
    {
        var forwarder = new BrowserToolsForwarder(
            new BrowserToolsForwarderOptions(providerAddress),
            NullLogger<BrowserToolsForwarder>.Instance);

        return await StartApplicationAsync(async context =>
        {
            if (context.Request.Path.StartsWithSegments(ApplicationPaths.BrowserTools))
            {
                await forwarder.ForwardAsync(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });
    }

    private static async Task<WebApplication> StartApplicationAsync(RequestDelegate requestDelegate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");

        var application = builder.Build();
        application.UseWebSockets();
        application.Run(requestDelegate);
        await application.StartAsync();
        return application;
    }

    private static Uri GetAddress(WebApplication application)
    {
        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses;

        return new Uri(addresses.Single());
    }

    private sealed record HttpRequestObservation(
        string Method,
        string Path,
        string? QueryString,
        string TestHeader,
        string? ContentType,
        string Body);

    private sealed record WebSocketObservation(
        string? SubProtocol,
        WebSocketMessageType MessageType,
        IReadOnlyList<bool> EndOfMessageValues,
        string Payload);
}
