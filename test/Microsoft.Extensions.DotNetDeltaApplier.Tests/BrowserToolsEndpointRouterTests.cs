// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.WebSockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class BrowserToolsEndpointRouterTests
{
    [TestMethod]
    public async Task ClearCache_Get_ReturnsNoContentAndClearSiteData()
    {
        using var server = new TestBrowserRefreshServer();
        var router = new BrowserToolsEndpointRouter(server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath);

        AssertEmptyResponse(context, body, StatusCodes.Status204NoContent);
        Assert.AreEqual("\"cache\"", context.Response.Headers["Clear-Site-Data"].ToString());
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("PUT")]
    [DataRow("DELETE")]
    [DataRow("HEAD")]
    public async Task KnownEndpoint_NonGetMethod_ReturnsMethodNotAllowed(string method)
    {
        using var server = new TestBrowserRefreshServer();
        var router = new BrowserToolsEndpointRouter(server);

        var (context, body) = await InvokeAsync(
            router,
            method,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath);

        AssertEmptyResponse(context, body, StatusCodes.Status405MethodNotAllowed);
    }

    /// <summary>
    /// The provider must never serve executable JavaScript: the browser tools client and its
    /// configuration are part of the application build output. Serving them from the provider
    /// would make authenticating the provider with the build pinned public key meaningless.
    /// </summary>
    [TestMethod]
    [DataRow("/browser-tools-bootstrap.js")]
    [DataRow("/browser-tools-client.js")]
    [DataRow("/session.json")]
    [DataRow("/updates/44444444-4444-4444-4444-444444444444.json")]
    [DataRow("/unknown")]
    public async Task RemovedOrUnknownGet_ReturnsNotFound(string route)
    {
        using var server = new TestBrowserRefreshServer();
        var router = new BrowserToolsEndpointRouter(server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + route);

        AssertEmptyResponse(context, body, StatusCodes.Status404NotFound);
    }

    [TestMethod]
    public async Task Connect_NonWebSocketRequest_ReturnsBadRequest()
    {
        using var server = new TestBrowserRefreshServer();
        var router = new BrowserToolsEndpointRouter(server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath);

        AssertEmptyResponse(context, body, StatusCodes.Status400BadRequest);
    }

    [TestMethod]
    public async Task Connect_WebSocketWithoutSharedSecret_IsRejectedBeforeAcceptance()
    {
        using var server = new TestBrowserRefreshServer();
        var context = await ConnectAsync(server, subProtocol: null);

        AssertResponse(context, StatusCodes.Status400BadRequest);
    }

    [TestMethod]
    [DataRow("not-base-64!")]
    [DataRow("AAAA")]
    public async Task Connect_WebSocketWithMalformedSharedSecret_IsRejectedBeforeAcceptance(string subProtocol)
    {
        using var server = new TestBrowserRefreshServer();
        var context = await ConnectAsync(server, subProtocol);

        AssertResponse(context, StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// A secret encrypted with a key pair other than the one pinned into the application build
    /// output must not authenticate: this is what prevents a rogue provider or a stale browser tab
    /// from talking to this provider.
    /// </summary>
    [TestMethod]
    public async Task Connect_WebSocketWithForeignKey_IsRejectedBeforeAcceptance()
    {
        using var server = new TestBrowserRefreshServer();
        using var foreignKey = new SharedSecretProvider();

        var context = await ConnectAsync(server, EncryptSecret(foreignKey));

        AssertResponse(context, StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// The sub-protocol is URL encoded on the wire because base64 contains '+' and '/'.
    /// A secret encrypted with the server's own key must reach acceptance.
    /// </summary>
    [TestMethod]
    public async Task Connect_WebSocketWithMatchingKey_ReachesAcceptance()
    {
        using var server = new TestBrowserRefreshServer();
        var feature = new TestWebSocketFeature();

        // The test feature refuses to complete the upgrade, so reaching it is the observable signal
        // that decryption succeeded and the request was authenticated.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ConnectAsync(server, Uri.EscapeDataString(EncryptSecret(server.Key)), feature));

        Assert.IsTrue(feature.AcceptAttempted);
    }

    private static string EncryptSecret(SharedSecretProvider key)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.GetPublicKey()), out _);
        return Convert.ToBase64String(rsa.Encrypt(RandomNumberGenerator.GetBytes(32), RSAEncryptionPadding.OaepSHA256));
    }

    private static async Task<DefaultHttpContext> ConnectAsync(
        TestBrowserRefreshServer server,
        string? subProtocol,
        TestWebSocketFeature? feature = null)
    {
        var router = new BrowserToolsEndpointRouter(server);
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpWebSocketFeature>(feature ?? new TestWebSocketFeature());
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath;
        context.Response.Body = new MemoryStream();

        if (subProtocol != null)
        {
            context.Request.Headers.SecWebSocketProtocol = subProtocol;
        }

        await router.HandleAsync(context);
        return context;
    }

    private static async Task<(DefaultHttpContext Context, byte[] Body)> InvokeAsync(
        BrowserToolsEndpointRouter router,
        string method,
        string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await router.HandleAsync(context);

        context.Response.Body.Position = 0;
        return (context, ((MemoryStream)context.Response.Body).ToArray());
    }

    private static void AssertEmptyResponse(DefaultHttpContext context, byte[] body, int expectedStatusCode)
    {
        AssertResponse(context, expectedStatusCode);
        Assert.IsEmpty(body);
    }

    private static void AssertResponse(DefaultHttpContext context, int expectedStatusCode)
    {
        Assert.AreEqual(expectedStatusCode, context.Response.StatusCode);
        Assert.AreEqual("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private sealed class TestWebSocketFeature : IHttpWebSocketFeature
    {
        public bool AcceptAttempted { get; private set; }

        public bool IsWebSocketRequest => true;

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            AcceptAttempted = true;
            throw new InvalidOperationException("Acceptance is not supported by the test feature.");
        }
    }
}
