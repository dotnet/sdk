// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Security.AntiSSRF;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class AuthRealmHandlerFactoryTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("  ", true)]
    [DataRow("Public", false)]
    [DataRow("public", true)]
    [DataRow(" PUBLIC ", false)]
    public void CreatePolicy_Public(string? policyValue, bool isInsecureRegistry)
    {
        AntiSSRFPolicy policy = CreateFactory(policyValue, allowedAddresses: null, isInsecureRegistry).CreatePolicy();

        Assert.IsFalse(policy.DenyAllUnspecifiedIPs);
        Assert.HasCount(IPAddressRanges.recommendedLatest.Length, policy.DeniedAddresses);
        Assert.IsEmpty(policy.AllowedAddresses);
        Assert.AreEqual(isInsecureRegistry, policy.AllowPlainTextHttp);
        Assert.IsFalse(policy.AddXFFHeader);
    }

    [TestMethod]
    [DataRow("AllowlistOnly")]
    [DataRow("allowlistonly")]
    public void CreatePolicy_AllowlistOnly(string policyValue)
    {
        AntiSSRFPolicy policy = CreateFactory(policyValue, allowedAddresses: null).CreatePolicy();

        Assert.IsTrue(policy.DenyAllUnspecifiedIPs);
        Assert.IsEmpty(policy.DeniedAddresses);
        Assert.IsFalse(policy.AddXFFHeader);
    }

    [TestMethod]
    [DataRow("None")]
    [DataRow("none")]
    public void CreatePolicy_None(string policyValue)
    {
        AntiSSRFPolicy policy = CreateFactory(policyValue, allowedAddresses: null).CreatePolicy();

        Assert.IsFalse(policy.DenyAllUnspecifiedIPs);
        Assert.IsEmpty(policy.DeniedAddresses);
    }

    [TestMethod]
    [DataRow("Strict")]
    [DataRow("ExternalOnly")]
    [DataRow("InternalOnly")]
    [DataRow("0")]
    public void CreatePolicy_RejectsInvalidPolicy(string policyValue)
    {
        InvalidAuthRealmConfigurationException exception = Assert.ThrowsExactly<InvalidAuthRealmConfigurationException>(
            () => CreateFactory(policyValue, allowedAddresses: null).CreatePolicy());

        Assert.Contains("CONTAINER2034", exception.Message);
        Assert.Contains(RegistrySettings.EnvVariables.AuthRealmPolicy, exception.Message);
        Assert.Contains(policyValue, exception.Message);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Public")]
    [DataRow("AllowlistOnly")]
    [DataRow("None")]
    public void CreatePolicy_AddsAllowedAddresses(string? policyValue)
    {
        AntiSSRFPolicy policy = CreateFactory(policyValue, " 10.0.0.0/8 ; 192.168.1.5;;fd00::/8 ").CreatePolicy();

        Assert.AreSequenceEqual(new[] { "10.0.0.0/8", "192.168.1.5/32", "fd00::/8" }, policy.AllowedAddresses.ToArray());
    }

    [TestMethod]
    [DataRow("10.0.0.1;bogus", "bogus")]
    [DataRow("10.0.0.0/33", "10.0.0.0/33")]
    [DataRow("auth.corp", "auth.corp")]
    [DataRow("10.0.0.0/", "10.0.0.0/")]
    public void CreatePolicy_RejectsInvalidAllowedAddress(string allowedAddresses, string invalidEntry)
    {
        InvalidAuthRealmConfigurationException exception = Assert.ThrowsExactly<InvalidAuthRealmConfigurationException>(
            () => CreateFactory(policyValue: null, allowedAddresses).CreatePolicy());

        Assert.Contains("CONTAINER2033", exception.Message);
        Assert.Contains(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses, exception.Message);
        Assert.Contains($"'{invalidEntry}'", exception.Message);
    }

    /// <summary>
    /// Verifies through the real SSRF-protected transport which configurations allow a token request
    /// to a loopback realm. An IP literal is used so the result doesn't depend on how the machine
    /// resolves "localhost".
    /// </summary>
    [TestMethod]
    [DataRow(null, null, false)]
    [DataRow(null, "10.0.0.0/8", false)]
    [DataRow(null, "127.0.0.1", true)]
    [DataRow("Public", "127.0.0.0/8", true)]
    [DataRow("AllowlistOnly", null, false)]
    [DataRow("AllowlistOnly", "127.0.0.0/8", true)]
    [DataRow("None", null, true)]
    public async Task Create_EnforcesPolicyWhenConnecting(string? policyValue, string? allowedAddresses, bool expectAllowed)
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Uri realmUri = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        AuthRealmHandlerFactory factory = CreateFactory(policyValue, allowedAddresses, isInsecureRegistry: true);
        using var client = new HttpMessageInvoker(factory.Create(realmUri));

        if (expectAllowed)
        {
            Task<string> serve = ServeAsync(listener, "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", TestContext.CancellationToken);
            using HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, realmUri), TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            await serve;
        }
        else
        {
            InvalidAuthResponseException exception = await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
                () => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, realmUri), TestContext.CancellationToken));
            Assert.Contains("CONTAINER1019", exception.Message);
            Assert.Contains(realmUri.ToString(), exception.Message);
            Assert.IsFalse(listener.Pending());
        }
    }

    [TestMethod]
    [DataRow("[::ffff:127.0.0.1]")]
    [DataRow("127\uFF0E0\uFF0E0\uFF0E1")]
    [DataRow("127\u30020\u30020\u30021")]
    public async Task Create_RejectsNormalizedLoopbackBeforeConnecting(string host)
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Uri realmUri = new($"http://{host}:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(realmUri.IdnHost.Trim('[', ']'), TestContext.CancellationToken);
        Assert.IsNotEmpty(addresses);
        Assert.IsTrue(addresses.All(IPAddress.IsLoopback));

        AuthRealmHandlerFactory factory = CreateFactory(policyValue: null, allowedAddresses: null, isInsecureRegistry: true);
        using var client = new HttpMessageInvoker(factory.Create(realmUri));

        InvalidAuthResponseException exception = await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
            () => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, realmUri), TestContext.CancellationToken));

        Assert.IsInstanceOfType<AntiSSRFException>(exception.InnerException);
        Assert.Contains("CONTAINER1019", exception.Message);
        Assert.IsFalse(listener.Pending());
    }

    /// <summary>
    /// Verifies that the production realm transport returns redirects without opening a connection
    /// to the untrusted target, even when the initial realm is explicitly allowed.
    /// </summary>
    [TestMethod]
    [DataRow("http://169.254.169.254/latest/meta-data/")]
    [DataRow("http://[fe80::1]/token")]
    [DataRow("http://10.0.0.7/token")]
    public async Task Create_DoesNotFollowRedirect(string redirectTarget)
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Uri realmUri = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        AuthRealmHandlerFactory factory = CreateFactory(policyValue: null, "127.0.0.1", isInsecureRegistry: true);

        Task<string> serve = ServeAsync(
            listener,
            $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {redirectTarget}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            TestContext.CancellationToken);
        using var client = new HttpMessageInvoker(factory.Create(realmUri));

        // Send one authentication request through the real AntiSSRF-protected handler.
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, realmUri),
            TestContext.CancellationToken);
        await serve;

        // The redirect belongs to the authentication caller; the transport must not follow it.
        Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.AreEqual(redirectTarget, response.Headers.Location?.AbsoluteUri);
    }

    /// <summary>
    /// Verifies that plain http realms are refused when connecting for secure registries, even to an
    /// allowed address.
    /// </summary>
    [TestMethod]
    public async Task Create_BlocksHttpRealmForSecureRegistry()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Uri realmUri = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        AuthRealmHandlerFactory factory = CreateFactory("None", allowedAddresses: null, isInsecureRegistry: false);

        using var client = new HttpMessageInvoker(factory.Create(realmUri));

        await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
            () => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, realmUri), TestContext.CancellationToken));
        Assert.IsFalse(listener.Pending());
    }

    [TestMethod]
    public async Task Create_DoesNotDowngradeHttpsRealmForInsecureRegistry()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Uri realmUri = new($"https://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        AuthRealmHandlerFactory factory = CreateFactory("None", allowedAddresses: null, isInsecureRegistry: true);

        using var client = new HttpMessageInvoker(factory.Create(realmUri));
        using CancellationTokenSource listenerTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        listenerTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        Task<int> acceptedConnections = AcceptConnectionsAsync(listener, listenerTimeout.Token);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, realmUri),
            TestContext.CancellationToken));
        listenerTimeout.Cancel();

        Assert.AreEqual(1, await acceptedConnections);

        static async Task<int> AcceptConnectionsAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            int count = 0;
            try
            {
                while (true)
                {
                    using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                    count++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return count;
            }
        }
    }

    [TestMethod]
    [DataRow(HttpRequestError.ResponseEnded)]
    [DataRow(HttpRequestError.InvalidResponse)]
    [DataRow(HttpRequestError.Unknown)]
    public async Task ExceptionTranslationHandler_PreservesHttpRequestError(HttpRequestError error)
    {
        IOException expected = error == HttpRequestError.Unknown
            ? new IOException("Transport failed.")
            : new HttpIOException(error, "Transport failed.");
        Uri realmUri = new("https://auth.test/token");
        using var client = new HttpMessageInvoker(new AuthRealmHandlerFactory.ExceptionTranslationHandler(
            new RecordingHandler([], _ => throw expected), "registry.test", realmUri));
        using var request = new HttpRequestMessage(HttpMethod.Get, realmUri);

        HttpRequestException actual = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => client.SendAsync(request, TestContext.CancellationToken));

        Assert.AreEqual(error, actual.HttpRequestError);
        Assert.AreSame(expected, actual.InnerException);
        Assert.AreEqual(expected.Message, actual.Message);
    }

    private sealed record RecordedRequest(Uri Uri, string Method, string? AuthorizationScheme, string? Content);

    private sealed class RecordingHandler(List<RecordedRequest> requests, Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add(new RecordedRequest(request.RequestUri!, request.Method.Method, request.Headers.Authorization?.Scheme, content));
            HttpResponseMessage response = respond(requests.Count);
            response.RequestMessage = request;
            return response;
        }
    }

    private static AuthRealmHandlerFactory CreateFactory(string? policyValue, string? allowedAddresses, bool isInsecureRegistry = false)
    {
        var environment = new Mock<IEnvironmentProvider>();
        environment.Setup(e => e.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy)).Returns(policyValue);
        environment.Setup(e => e.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses)).Returns(allowedAddresses);
        return new AuthRealmHandlerFactory(
            "registry.test",
            new Uri("https://registry.test"),
            isInsecureRegistry,
            environment.Object);
    }

    private static async Task<string> ServeAsync(TcpListener listener, string response, CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        using NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];
        string request = "";
        while (!request.Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            request += Encoding.ASCII.GetString(buffer, 0, read);
        }
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        return request;
    }
}
