// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Authentication;
using Moq;
using Moq.Protected;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
[DoNotParallelize]
public class OrasRegistryAdapterTests
{
    public TestContext TestContext { get; set; } = default!;

    private static readonly string[] CredentialEnvironmentVariables =
    [
        ContainerHelpers.PushHostObjectUser,
        ContainerHelpers.PushHostObjectPass,
        ContainerHelpers.PullHostObjectUser,
        ContainerHelpers.PullHostObjectPass,
        ContainerHelpers.HostObjectUser,
        ContainerHelpers.HostObjectPass,
        ContainerHelpers.HostObjectUserLegacy,
        ContainerHelpers.HostObjectPassLegacy,
    ];

    [TestMethod]
    public async Task ForeignUploadBasicChallengeCannotObtainEnvironmentCredentials()
    {
        string? oldUser = Environment.GetEnvironmentVariable(ContainerHelpers.PushHostObjectUser);
        string? oldPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PushHostObjectPass);
        try
        {
            Environment.SetEnvironmentVariable(ContainerHelpers.PushHostObjectUser, "synthetic-user");
            Environment.SetEnvironmentVariable(ContainerHelpers.PushHostObjectPass, "synthetic-password");
            string registryHost = Guid.NewGuid().ToString("N") + ".example.com";
            int foreignRequests = 0;
            var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
            handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    Assert.AreEqual("http", request.RequestUri!.Scheme);
                    var response = new HttpResponseMessage(HttpStatusCode.Accepted) { RequestMessage = request };
                    if (request.Method == HttpMethod.Post)
                    {
                        response.Headers.Location = new Uri("http://upload.example.com/session");
                    }
                    else
                    {
                        Assert.AreEqual("upload.example.com", request.RequestUri.Host);
                        Assert.IsNull(request.Headers.Authorization);
                        foreignRequests++;
                        response.StatusCode = HttpStatusCode.Unauthorized;
                        response.Headers.WwwAuthenticate.ParseAdd("Basic realm=foreign");
                    }
                    return response;
                });
            using var http = new HttpClient(handler.Object);
            var client = new Client(http, new OrasCredentialProvider(RegistryMode.Push, registryHost));
            var factory = new OrasRepositoryFactory(new Uri($"http://{registryHost}"), client,
                new RegistrySettings { ForceChunkedUpload = true });
            using var content = new MemoryStream();
            await Assert.ThrowsExactlyAsync<AuthenticationException>(() =>
                factory.Create("app").Blobs.PushAsync(Descriptor.Create([], "application/octet-stream"), content, TestContext.CancellationToken));
            Assert.AreEqual(1, foreignRequests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ContainerHelpers.PushHostObjectUser, oldUser);
            Environment.SetEnvironmentVariable(ContainerHelpers.PushHostObjectPass, oldPassword);
        }
    }

    [TestMethod]
    [DataRow("http://registry.example.com:5000", false, null, (int)BlobUploadMode.MonolithicWithChunkedFallback, 64 * 1024)]
    [DataRow("https://registry.example.com", false, null, (int)BlobUploadMode.MonolithicWithChunkedFallback, 64 * 1024)]
    [DataRow("https://registry.example.com", true, 1024 * 1024, (int)BlobUploadMode.Chunked, 1024 * 1024)]
    [DataRow("https://123456789012.dkr.ecr.eu-west-1.amazonaws.com", false, null, (int)BlobUploadMode.MonolithicWithChunkedFallback, 5_248_080)]
    [DataRow("https://123456789012.dkr.ecr.eu-west-1.amazonaws.com", true, 6 * 1024 * 1024, (int)BlobUploadMode.Chunked, 6 * 1024 * 1024)]
    public void RepositoryFactoryPreservesUploadConfiguration(
        string registryUri,
        bool forceChunkedUpload,
        int? configuredChunkSize,
        int expectedUploadMode,
        int expectedChunkSize)
    {
        var settings = new RegistrySettings
        {
            ForceChunkedUpload = forceChunkedUpload,
            ChunkedUploadSizeBytes = configuredChunkSize,
        };
        var factory = new OrasRepositoryFactory(new Uri(registryUri), Mock.Of<IClient>(), settings);

        var repository = (Repository)factory.Create("test/repository");

        Assert.AreEqual((BlobUploadMode)expectedUploadMode, repository.Options.BlobUploadMode);
        Assert.AreEqual(expectedChunkSize, repository.Options.BlobUploadChunkSize);
        Assert.AreEqual(new Uri(registryUri).Scheme == Uri.UriSchemeHttp, repository.Options.PlainHttp);
    }

    [TestMethod]
    [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.Push)]
    [DataRow("DOTNET_CONTAINER_PUSH_REGISTRY_UNAME", "DOTNET_CONTAINER_PUSH_REGISTRY_PWORD", (int)RegistryMode.Push)]
    [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.Pull)]
    [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
    [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
    public async Task CredentialProviderUsesEnvironmentOverrides(string usernameVariable, string passwordVariable, int mode)
    {
        Dictionary<string, string?> originalValues = CredentialEnvironmentVariables.ToDictionary(
            variable => variable,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (string variable in CredentialEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
            Environment.SetEnvironmentVariable(usernameVariable, "uname");
            Environment.SetEnvironmentVariable(passwordVariable, "pword");

            var provider = new OrasCredentialProvider((RegistryMode)mode, "registry.example.com");
            OrasProject.Oras.Registry.Remote.Auth.Credential credential =
                await provider.ResolveCredentialAsync("registry.example.com", TestContext.CancellationToken);

            Assert.AreEqual("uname", credential.Username);
            Assert.AreEqual("pword", credential.Password);
            var foreignCredential = await provider.ResolveCredentialAsync("upload.example.com", TestContext.CancellationToken);
            Assert.IsTrue(foreignCredential.IsEmpty());
            var differentPort = await provider.ResolveCredentialAsync("registry.example.com:5000", TestContext.CancellationToken);
            Assert.IsTrue(differentPort.IsEmpty());
        }
        finally
        {
            foreach ((string variable, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }

    [TestMethod]
    public async Task CredentialProviderReadsDockerAuthFile()
    {
        const string usernameRegistry = "username.registry.test";
        const string tokenRegistry = "token.registry.test";
        string authFile = Path.GetTempFileName();
        string? originalAuthFile = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
        Dictionary<string, string?> originalValues = CredentialEnvironmentVariables.ToDictionary(
            variable => variable,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (string variable in CredentialEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            string usernamePassword = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
            string placeholder = Convert.ToBase64String(Encoding.UTF8.GetBytes("__:__"));
            await File.WriteAllTextAsync(
                authFile,
                $$"""
                {
                  "auths": {
                    "{{usernameRegistry}}": { "auth": "{{usernamePassword}}" },
                    "{{tokenRegistry}}": { "auth": "{{placeholder}}", "identitytoken": "refresh-token" }
                  }
                }
                """,
                TestContext.CancellationToken);
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);

            var provider = new OrasCredentialProvider(RegistryMode.Push, usernameRegistry);
            OrasProject.Oras.Registry.Remote.Auth.Credential usernameCredential =
                await provider.ResolveCredentialAsync(usernameRegistry, TestContext.CancellationToken);
            OrasProject.Oras.Registry.Remote.Auth.Credential tokenCredential =
                await new OrasCredentialProvider(RegistryMode.Push, tokenRegistry).ResolveCredentialAsync(tokenRegistry, TestContext.CancellationToken);

            Assert.AreEqual("user", usernameCredential.Username);
            Assert.AreEqual("password", usernameCredential.Password);
            Assert.AreEqual("refresh-token", tokenCredential.RefreshToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", originalAuthFile);
            foreach ((string variable, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
            File.Delete(authFile);
        }
    }

    [TestMethod]
    [DataRow("docker.io")]
    [DataRow("https://index.docker.io/v1/")]
    [DataRow("registry-1.docker.io")]
    public async Task DockerHubCredentialsSurviveRegistryAliasRewriting(string credentialKey)
    {
        string authFile = Path.GetTempFileName();
        string? previousFile = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
        var previousCredentials = CredentialEnvironmentVariables.ToDictionary(variable => variable, Environment.GetEnvironmentVariable);
        try
        {
            foreach (string variable in CredentialEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes("hub-user:hub-password"));
            await File.WriteAllTextAsync(authFile,
                System.Text.Json.JsonSerializer.Serialize(new { auths = new Dictionary<string, object> { [credentialKey] = new { auth } } }), TestContext.CancellationToken);
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);
            var provider = new OrasCredentialProvider(RegistryMode.Pull, "registry-1.docker.io");
            var credential = await provider.ResolveCredentialAsync("registry-1.docker.io", TestContext.CancellationToken);
            Assert.AreEqual("hub-user", credential.Username);
            Assert.AreEqual("hub-password", credential.Password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", previousFile);
            foreach ((string variable, string? value) in previousCredentials)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
            File.Delete(authFile);
        }
    }

    [TestMethod]
    [DataRow("https://auth.example.com/token", false)]
    [DataRow("https://auth.example.com:8443/token", false)]
    [DataRow("https://203.0.113.10/token", false)]
    [DataRow("http://auth.example.com/token", true)]
    public void ValidateRealmUriAcceptsAllowedSchemes(string realm, bool isInsecureRegistry)
    {
        Uri uri = OrasRealmValidator.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry);
        Assert.AreEqual(realm, uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("http://auth.example.com/token", false)]
    [DataRow("https://user:pass@auth.example.com/token", false)]
    [DataRow("https://user@auth.example.com/token", true)]
    [DataRow("ftp://auth.example.com/token", false)]
    [DataRow("ftp://auth.example.com/token", true)]
    [DataRow("file:///etc/passwd", false)]
    public void ValidateRealmUriRejectsDisallowedSchemes(string realm, bool isInsecureRegistry)
    {
        Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
            OrasRealmValidator.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry));
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("/relative/path")]
    [DataRow("auth.example.com/token")]
    public void ValidateRealmUriRejectsRelativeOrUnparseableRealms(string realm)
    {
        Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
            OrasRealmValidator.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task BearerHandshakeUsesProtectedRealmTransportAndNeverFollowsRedirect(bool redirect, bool passwordFallback)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var realm = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/token");
        const string token = "{\"token\":\"local-token\",\"access_token\":\"local-token\"}";
        string reply = redirect
            ? "HTTP/1.1 307 Temporary Redirect\r\nLocation: http://127.0.0.1:1/never\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
            : $"HTTP/1.1 200 OK\r\nContent-Length: {token.Length}\r\nConnection: close\r\n\r\n{token}";
        Task<string> serve = ServeTokenRequestsAsync();
        async Task<string> ServeTokenRequestsAsync()
        {
            string? getRequest = null;
            if (passwordFallback)
                getRequest = await AuthRealmHandlerFactoryTests.ServeAsync(listener,
                    "HTTP/1.1 405 Method Not Allowed\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", timeout.Token);
            string finalRequest = await AuthRealmHandlerFactoryTests.ServeAsync(listener, reply, timeout.Token);
            if (getRequest is not null) Assert.StartsWith("GET /token?", getRequest);
            return finalRequest;
        }
        var environment = new Mock<IEnvironmentProvider>();
        environment.Setup(e => e.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy)).Returns("AllowlistOnly");
        environment.Setup(e => e.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses)).Returns("127.0.0.1");
        string host = Guid.NewGuid().ToString("N") + ".example.com";
        var registryUri = new Uri($"https://{host}");
        var realmFactory = new AuthRealmHandlerFactory(host, registryUri, true, environment.Object);
        var transport = new Mock<HttpMessageHandler>();
        transport.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                bool authenticated = request.Headers.Authorization?.ToString() == "Bearer local-token";
                var response = new HttpResponseMessage(authenticated ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent([]),
                };
                if (!authenticated)
                    response.Headers.WwwAuthenticate.ParseAdd($"Bearer realm=\"{realm}\",service=\"registry\"");
                return response;
            });
        string authFile = Path.GetTempFileName();
        string? previousFile = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
        var previousCredentials = CredentialEnvironmentVariables.ToDictionary(variable => variable, Environment.GetEnvironmentVariable);
        try
        {
            await File.WriteAllTextAsync(authFile, "{\"auths\":{}}", timeout.Token);
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);
            foreach (string variable in CredentialEnvironmentVariables) Environment.SetEnvironmentVariable(variable, null);
            if (passwordFallback)
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "synthetic-user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "synthetic-password");
            }
            var settings = new RegistrySettings { IsInsecure = true };
            var client = OrasRepositoryFactory.CreateClient(host, registryUri, settings, NullLogger.Instance,
                RegistryMode.Pull, transport.Object, realmFactory.Create);
            using var registryHttp = client.BaseClient;
            using var tokenHttp = client.TokenClient;
            var factory = new OrasRepositoryFactory(registryUri, client, settings);
            Task<bool> exists = factory.Create("app").Blobs.ExistsAsync(Descriptor.Create([], "application/octet-stream"), timeout.Token);
            if (redirect)
                await Assert.ThrowsExactlyAsync<OrasProject.Oras.Registry.Remote.Exceptions.ResponseException>(() => exists);
            else
                Assert.IsTrue(await exists);
            string request = await serve;
            Assert.StartsWith(passwordFallback ? "POST /token " : "GET /token?", request);
            if (passwordFallback)
            {
                Assert.Contains("grant_type=password", request);
                Assert.Contains("username=synthetic-user", request);
                Assert.Contains("password=synthetic-password", request);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", previousFile);
            foreach ((string variable, string? value) in previousCredentials) Environment.SetEnvironmentVariable(variable, value);
            File.Delete(authFile);
        }
    }

}
