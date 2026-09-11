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
            "/_framework/dotnet-browser-tools/clear-cache?name=value%20with%20space")
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
        Assert.AreEqual("/_framework/dotnet-browser-tools/clear-cache", observation.Path);
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

    /// <summary>
    /// The provider rejects a browser that does not present a decryptable encrypted secret before it
    /// upgrades the connection, so the rejection is an ordinary HTTP response. That deliberate status is
    /// part of the authentication contract and has to survive the hop through the application origin,
    /// which is the only origin the browser ever talks to.
    /// </summary>
    [TestMethod]
    [DataRow(StatusCodes.Status400BadRequest)]
    [DataRow(StatusCodes.Status403Forbidden)]
    public async Task ForwardWebSocketAsync_PreservesProviderPreUpgradeStatus(int providerStatusCode)
    {
        await using var provider = await StartApplicationAsync(context =>
        {
            // Mirrors AbstractBrowserRefreshServer.AcceptBrowserConnectionAsync: status only, no upgrade.
            context.Response.StatusCode = providerStatusCode;
            return Task.CompletedTask;
        });

        await using var application = await StartForwarderAsync(GetAddress(provider));

        Assert.AreEqual(
            (HttpStatusCode)providerStatusCode,
            await ConnectAndGetFailureStatusAsync(application, "invalid-encrypted-secret"));
    }

    /// <summary>
    /// A provider that is gone or unreachable never produced a status, so the forwarder must keep
    /// reporting a gateway error instead of inventing an authentication failure.
    /// </summary>
    [TestMethod]
    public async Task ForwardWebSocketAsync_WhenProviderIsUnavailable_ReturnsBadGateway()
    {
        // Start and immediately stop a provider so the address is real but nothing is listening on it.
        var provider = await StartApplicationAsync(_ => Task.CompletedTask);
        var providerAddress = GetAddress(provider);
        await provider.StopAsync(TestContext.CancellationToken);
        await provider.DisposeAsync();

        await using var application = await StartForwarderAsync(providerAddress);

        Assert.AreEqual(
            HttpStatusCode.BadGateway,
            await ConnectAndGetFailureStatusAsync(application, "encrypted-secret"));
    }

    /// <summary>
    /// A provider that switched protocols and then failed handshake validation reports 101, because the
    /// status is recorded before the upgrade response is validated. Relaying it would make the application
    /// claim a protocol switch it never performed, so it has to stay a gateway error.
    /// </summary>
    [TestMethod]
    public async Task ForwardWebSocketAsync_WhenTheUpgradeFailsAfterSwitchingProtocols_ReturnsBadGateway()
    {
        await using var provider = await StartApplicationAsync(async context =>
        {
            // Accepting a subprotocol the client never requested completes the 101 but fails validation.
            using var socket = await context.WebSockets.AcceptWebSocketAsync("a-protocol-the-client-did-not-request");
        });

        await using var application = await StartForwarderAsync(GetAddress(provider));

        Assert.AreEqual(
            HttpStatusCode.BadGateway,
            await ConnectAndGetFailureStatusAsync(application, "encrypted-secret"));
    }

    private async Task<HttpStatusCode> ConnectAndGetFailureStatusAsync(WebApplication application, string? subProtocol)
    {
        using var browserSocket = new ClientWebSocket();
        browserSocket.Options.CollectHttpResponseDetails = true;
        if (subProtocol != null)
        {
            browserSocket.Options.AddSubProtocol(subProtocol);
        }

        var webSocketAddress = new UriBuilder(GetAddress(application))
        {
            Scheme = "ws",
            Path = "/_framework/dotnet-browser-tools/connect",
        }.Uri;

        await Assert.ThrowsExactlyAsync<WebSocketException>(
            () => browserSocket.ConnectAsync(webSocketAddress, TestContext.CancellationToken));

        return browserSocket.HttpStatusCode;
    }

    private static async Task<WebApplication> StartForwarderAsync(Uri providerAddress)
    {
        var forwarder = new BrowserToolsForwarder(providerAddress, NullLogger<BrowserToolsForwarder>.Instance);

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
