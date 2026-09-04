// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// The provider exposes exactly two routes and one credential. The browser generated secret,
/// encrypted with the public key that was pinned into the application build output, is the only
/// thing that lets a caller obtain a socket, and a caller that cannot produce one is rejected
/// before the connection is upgraded so it never gets one at all.
/// </summary>
[TestClass]
public class BrowserToolsEndpointRouterTests : IDisposable
{
    private const string ConnectPath = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath;
    private const string ClearCachePath = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath;

    private readonly SharedSecretProvider _sharedSecretProvider = new();
    private readonly TestBrowserRefreshServer _browserServer;
    private KestrelWebSocketServer? _server;

    public TestContext TestContext { get; set; } = null!;

    public BrowserToolsEndpointRouterTests()
    {
        _browserServer = new TestBrowserRefreshServer((_, _) => { }, _sharedSecretProvider);
    }

    public void Dispose()
    {
        _server?.Dispose();
        _browserServer.Dispose();
        _sharedSecretProvider.Dispose();
    }

    private async ValueTask<Uri> StartRouterAsync()
    {
        var router = new BrowserToolsEndpointRouter(_browserServer);
        _server = await KestrelWebSocketServer.StartServerAsync(
            new WebSocketConfig(port: 0, securePort: null, hostName: null),
            router.HandleAsync,
            TestContext.CancellationToken);

        return new Uri(_server.HttpServerUrls.Single());
    }

    /// <summary>
    /// Encrypts a browser generated secret the way the client does, using the public half of the
    /// supplied key. The result is the raw base64 ciphertext; callers that put it on the wire have to
    /// escape it first because a WebSocket subprotocol is an HTTP token.
    /// </summary>
    private static string EncryptSecret(SharedSecretProvider provider, byte[] secret)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(provider.GetPublicKey()), out _);
        return Convert.ToBase64String(rsa.Encrypt(secret, RSAEncryptionPadding.OaepSHA256));
    }

    /// <summary>
    /// Matches the client, which sends encodeURIComponent(ciphertext) as the subprotocol.
    /// </summary>
    private static string ToSubProtocol(string encryptedSecret)
        => Uri.EscapeDataString(encryptedSecret);

    private async Task<HttpStatusCode> ConnectAsync(Uri baseAddress, params string[] subProtocols)
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;

        foreach (var subProtocol in subProtocols)
        {
            socket.Options.AddSubProtocol(subProtocol);
        }

        var address = new UriBuilder(baseAddress) { Scheme = "ws", Path = ConnectPath }.Uri;

        await Assert.ThrowsExactlyAsync<WebSocketException>(
            () => socket.ConnectAsync(address, TestContext.CancellationToken));

        return socket.HttpStatusCode;
    }

    [TestMethod]
    public async Task Connect_WithoutASubProtocol_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartRouterAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress));
    }

    /// <summary>
    /// Exactly one subprotocol is the credential. Offering several would let a caller probe for an
    /// accepted one, so the request is rejected instead.
    /// </summary>
    [TestMethod]
    public async Task Connect_WithMoreThanOneSubProtocol_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartRouterAsync();
        var valid = ToSubProtocol(EncryptSecret(_sharedSecretProvider, RandomNumberGenerator.GetBytes(32)));

        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, valid, "other-protocol"));
    }

    [TestMethod]
    [DataRow("not-base64!!", DisplayName = "Malformed base64")]
    [DataRow("dG9vLXNob3J0", DisplayName = "Well formed base64 that is not ciphertext")]
    public async Task Connect_WithAMalformedSecret_IsRejectedBeforeUpgrade(string subProtocol)
    {
        var baseAddress = await StartRouterAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, subProtocol));
    }

    /// <summary>
    /// A well formed ciphertext produced for a different key is what a browser holding a stale or
    /// foreign configuration module would present. Only the private key the provider created for
    /// this invocation can decrypt the secret, so it is rejected too.
    /// </summary>
    [TestMethod]
    public async Task Connect_WithASecretEncryptedForAnotherKey_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartRouterAsync();

        using var otherProvider = new SharedSecretProvider();
        var encryptedForOtherKey = ToSubProtocol(EncryptSecret(otherProvider, RandomNumberGenerator.GetBytes(32)));

        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, encryptedForOtherKey));
    }

    /// <summary>
    /// The credential the browser sends must round trip through the provider's private key, which is
    /// what the rejection tests above would otherwise pass vacuously.
    /// </summary>
    [TestMethod]
    public void DecryptSecret_RoundTripsASecretEncryptedWithThePinnedPublicKey()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var encrypted = EncryptSecret(_sharedSecretProvider, secret);

        Assert.AreEqual(Convert.ToBase64String(secret), _sharedSecretProvider.DecryptSecret(encrypted));
    }

    [TestMethod]
    public async Task ClearCache_RespondsWithClearSiteData()
    {
        var baseAddress = await StartRouterAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync(ClearCachePath, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("\"cache\"", response.Headers.GetValues("Clear-Site-Data").Single());
        Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
    }

    /// <summary>
    /// The provider serves no JavaScript and no session or update documents. Anything but the two
    /// routes it owns is a 404, which is what keeps the browser tools client app hosted.
    /// </summary>
    [TestMethod]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/session.json")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/updates/1.json")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/browser-tools.js")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/")]
    public async Task UnknownPaths_RespondWithNotFound(string path)
    {
        var baseAddress = await StartRouterAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync(path, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task NonGetRequests_RespondWithMethodNotAllowed()
    {
        var baseAddress = await StartRouterAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.PostAsync(ClearCachePath, content: null, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
