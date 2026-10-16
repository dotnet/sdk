// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// The provider exposes exactly three routes and one credential. The browser generated secret,
/// encrypted with the public key that was pinned into the application build output, is the only
/// thing that lets a caller obtain a socket, and a caller that cannot produce one is rejected
/// before the connection is upgraded so it never gets one at all.
/// </summary>
[TestClass]
public class BrowserToolsEndpointRouterTests : IAsyncDisposable
{
    private const string ConnectPath = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath;
    private const string ClearCachePath = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath;
    private const string HotReloadSettingsPath = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.HotReloadSettingsPath;

    private readonly RSAParameters _sessionKeyParameters;
    private readonly SharedSecretProvider _sharedSecretProvider;
    private readonly TestBrowserRefreshServer _browserServer;
    private int _sessionKeyLoads;
    private IAsyncDisposable? _server;

    public TestContext TestContext { get; set; } = null!;

    public BrowserToolsEndpointRouterTests()
    {
        using var rsa = RSA.Create(2048);
        _sessionKeyParameters = rsa.ExportParameters(includePrivateParameters: true);
        _sharedSecretProvider = new SharedSecretProvider(_sessionKeyParameters);
        _browserServer = new TestBrowserRefreshServer(
            middlewareAssemblyPath: Path.GetTempPath(),
            useGatewayProxy: true,
            sessionKeyFactory: () =>
            {
                _sessionKeyLoads++;
                return new SharedSecretProvider(_sessionKeyParameters);
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_server != null)
        {
            await _server.DisposeAsync();
        }

        await _browserServer.DisposeAsync();
        _sharedSecretProvider.Dispose();
    }

    private async ValueTask<Uri> StartServerAsync()
    {
#if NET
        var config = new WebSocketConfig(port: 0, securePort: null, hostName: null, additionalAllowedOrigins: []);
        var server = await KestrelWebSocketServer.StartServerAsync(
            config,
            async context => await _browserServer.HandleRequestAsync(config, context),
            TestContext.CancellationToken);

        _server = server;

        return new Uri(server.HttpServerUrls.Single());
#else
        // HttpListener does not support auto-assigned ports, so allocate a free one.
        var config = new WebSocketConfig(port: GetFreePort(),  securePort: null, hostName: null, additionalAllowedOrigins: []);

        var server = NetFrameworkWebSocketServer.StartServer(
            config,
            async (config, context) => await _browserServer.HandleRequestAsync(config, context),
            NullLogger.Instance);

        _server = server;

        return new Uri(server.HttpServerUrls.Single());

        static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
#endif
    }

    private static byte[] GetRandomBytes(int count)
    {
#if NET
        return RandomNumberGenerator.GetBytes(count);
#else
        var bytes = new byte[count];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
#endif
    }

    private static string EncryptSecret(SharedSecretProvider provider, byte[] secret)
        => CryptoExtensions.GetEncryptedSecret(provider.GetPublicKey(), provider.ExportPublicKeyParameters(), secret);

    /// <summary>
    /// Matches the client, which sends encodeURIComponent(ciphertext) as the subprotocol.
    /// </summary>
    private static string ToSubProtocol(string encryptedSecret)
        => Uri.EscapeDataString(encryptedSecret);

    private async Task<HttpStatusCode> ConnectAsync(Uri baseAddress, params string[] subProtocols)
    {
        using var socket = new ClientWebSocket();
#if NET
        socket.Options.CollectHttpResponseDetails = true;
#endif
        foreach (var subProtocol in subProtocols)
        {
            socket.Options.AddSubProtocol(subProtocol);
        }

        socket.Options.SetRequestHeader("Origin", "http://localhost");

        var address = new UriBuilder(baseAddress) { Scheme = "ws", Path = ConnectPath }.Uri;

        await Assert.ThrowsExactlyAsync<WebSocketException>(
            () => socket.ConnectAsync(address, TestContext.CancellationToken));

#if NET
        return socket.HttpStatusCode;
#else
        return HttpStatusCode.BadRequest;
#endif
    }

    [TestMethod]
    public async Task Connect_WithoutASubProtocol_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartServerAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress));
    }

    /// <summary>
    /// Exactly one subprotocol is the credential. Offering several would let a caller probe for an
    /// accepted one, so the request is rejected instead.
    /// </summary>
    [TestMethod]
    public async Task Connect_WithMoreThanOneSubProtocol_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartServerAsync();
        var valid = ToSubProtocol(EncryptSecret(_sharedSecretProvider, GetRandomBytes(32)));

        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, valid, "other-protocol"));
    }

    [TestMethod]
    [DataRow("not-base64!!", DisplayName = "Malformed base64")]
    [DataRow("dG9vLXNob3J0", DisplayName = "Well formed base64 that is not ciphertext")]
    public async Task Connect_WithAMalformedSecret_IsRejectedBeforeUpgrade(string subProtocol)
    {
        var baseAddress = await StartServerAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, subProtocol));
        Assert.AreEqual(1, _sessionKeyLoads);
    }

    [TestMethod]
    public async Task Connect_LoadsTheCurrentSessionKeyForEachRequest()
    {
        var baseAddress = await StartServerAsync();

        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, "not-base64!!"));
        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, "not-base64!!"));
        Assert.AreEqual(2, _sessionKeyLoads);
    }

    /// <summary>
    /// A well formed ciphertext produced for a different key is what a browser holding a stale or
    /// foreign configuration module would present. Only the private key the provider created for
    /// this invocation can decrypt the secret, so it is rejected too.
    /// </summary>
    [TestMethod]
    public async Task Connect_WithASecretEncryptedForAnotherKey_IsRejectedBeforeUpgrade()
    {
        var baseAddress = await StartServerAsync();

        using var otherProvider = new SharedSecretProvider();
        var encryptedForOtherKey = ToSubProtocol(EncryptSecret(otherProvider, GetRandomBytes(32)));

        Assert.AreEqual(HttpStatusCode.BadRequest, await ConnectAsync(baseAddress, encryptedForOtherKey));
    }

    /// <summary>
    /// The credential the browser sends must round trip through the provider's private key, which is
    /// what the rejection tests above would otherwise pass vacuously.
    /// </summary>
    [TestMethod]
    public void DecryptSecret_RoundTripsASecretEncryptedWithThePinnedPublicKey()
    {
        var secret = GetRandomBytes(32);
        var encrypted = EncryptSecret(_sharedSecretProvider, secret);

        Assert.AreEqual(Convert.ToBase64String(secret), _sharedSecretProvider.DecryptSecret(encrypted));
    }

    [TestMethod]
    public async Task ClearCache_RespondsWithClearSiteData()
    {
        var baseAddress = await StartServerAsync();

        var response = await TestHttpClient.GetAsync(baseAddress, ClearCachePath, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("\"cache\"", response.GetHeader("Clear-Site-Data"));
        Assert.AreEqual("no-store", response.GetHeader("Cache-Control"));
    }

    [TestMethod]
    public async Task HotReloadSettings_ReportsProviderAvailability()
    {
        var baseAddress = await StartServerAsync();

        var response = await TestHttpClient.GetAsync(baseAddress, HotReloadSettingsPath, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.MediaType);
        Assert.AreEqual("no-store", response.GetHeader("Cache-Control"));
        Assert.AreEqual("{ \"hotReload\": true }", response.Body);
        Assert.AreEqual(0, _sessionKeyLoads);
    }

    /// <summary>
    /// The provider serves no JavaScript and no session or update documents. Anything but the three
    /// routes it owns is a 404, which is what keeps the browser tools client app hosted.
    /// </summary>
    [TestMethod]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/session.json")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/updates/1.json")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/browser-tools.js")]
    [DataRow(BrowserToolsProtocol.RoutePrefix + "/")]
    public async Task UnknownPaths_RespondWithNotFound(string path)
    {
        var baseAddress = await StartServerAsync();

        var response = await TestHttpClient.GetAsync(baseAddress, path, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task NonGetRequests_RespondWithMethodNotAllowed()
    {
        var baseAddress = await StartServerAsync();

        var response = await TestHttpClient.PostAsync(baseAddress, ClearCachePath, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
