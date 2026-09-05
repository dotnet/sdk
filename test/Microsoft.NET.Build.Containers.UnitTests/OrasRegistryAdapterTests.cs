// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;
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
    [DataRow("https://registry.example.com", false, null, (int)BlobUploadMode.MonolithicWithChunkedFallback, 64 * 1024)]
    [DataRow("https://registry.example.com", true, 1024 * 1024, (int)BlobUploadMode.Chunked, 1024 * 1024)]
    [DataRow("https://123456789012.dkr.ecr.eu-west-1.amazonaws.com", false, null, (int)BlobUploadMode.MonolithicWithChunkedFallback, 64 * 1024)]
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

            var provider = new OrasCredentialProvider((RegistryMode)mode);
            OrasProject.Oras.Registry.Remote.Auth.Credential credential =
                await provider.ResolveCredentialAsync("registry.example.com", TestContext.CancellationToken);

            Assert.AreEqual("uname", credential.Username);
            Assert.AreEqual("pword", credential.Password);
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

            var provider = new OrasCredentialProvider(RegistryMode.Push);
            OrasProject.Oras.Registry.Remote.Auth.Credential usernameCredential =
                await provider.ResolveCredentialAsync(usernameRegistry, TestContext.CancellationToken);
            OrasProject.Oras.Registry.Remote.Auth.Credential tokenCredential =
                await provider.ResolveCredentialAsync(tokenRegistry, TestContext.CancellationToken);

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
    [DataRow("https://127.0.0.1/token")]
    [DataRow("https://127.5.6.7/token")]
    [DataRow("https://0.0.0.0/token")]
    [DataRow("https://10.0.0.5/token")]
    [DataRow("https://172.16.0.1/token")]
    [DataRow("https://172.31.255.255/token")]
    [DataRow("https://192.168.1.5/token")]
    [DataRow("https://169.254.169.254/token")]
    [DataRow("https://224.0.0.1/token")]
    [DataRow("https://[::1]/token")]
    [DataRow("https://[::]/token")]
    [DataRow("https://[fe80::1]/token")]
    [DataRow("https://[ff02::1]/token")]
    [DataRow("https://[fc00::1]/token")]
    [DataRow("https://[fec0::1]/token")]
    [DataRow("https://[::ffff:127.0.0.1]/token")]
    [DataRow("https://[::ffff:169.254.169.254]/token")]
    [DataRow("https://127\uFF0E0\uFF0E0\uFF0E1/token")]
    [DataRow("https://169\uFF0E254\uFF0E169\uFF0E254/token")]
    [DataRow("https://10\uFF0E0\uFF0E0\uFF0E1/token")]
    [DataRow("https://127\u30020\u30020\u30021/token")]
    [DataRow("https://127.0.0.1./token")]
    [DataRow("https://169.254.169.254./token")]
    [DataRow("https://10.0.0.5./token")]
    public void ValidateRealmUriRejectsBlockedIpLiteralsOnSecureRegistry(string realm)
    {
        Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
            OrasRealmValidator.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
    }

    [TestMethod]
    [DataRow("https://169.254.169.254/token", "192.168.1.5:5000")]
    [DataRow("https://10.0.0.5/token", "192.168.1.5:5000")]
    [DataRow("https://[::1]/token", "192.168.1.5:5000")]
    [DataRow("https://169.254.169.254/token", "localhost:5000")]
    [DataRow("https://192.168.1.5/token", "localhost:5000")]
    [DataRow("https://127.0.0.1/token", "localhost.example.com:5000")]
    public void ValidateRealmUriRejectsBlockedIpLiteralsWhenInsecureRegistryHostsDiffer(string realm, string registryName)
    {
        Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
            OrasRealmValidator.ValidateRealmUri(realm, registryName, isInsecureRegistry: true));
    }

    [TestMethod]
    [DataRow("http://192.168.1.5/auth", "192.168.1.5")]
    [DataRow("http://192.168.1.5:6000/auth", "192.168.1.5:5000")]
    [DataRow("https://192.168.1.5/auth", "192.168.1.5:5000")]
    [DataRow("http://127.0.0.1:7000/auth", "127.0.0.1:5000")]
    [DataRow("https://[::1]:7000/auth", "[::1]:5000")]
    [DataRow("http://127.0.0.1:5000/auth", "localhost:5000")]
    [DataRow("http://[::1]:5000/auth", "localhost:5000")]
    [DataRow("http://127.0.0.1:5000/auth", "registry.localhost:5000")]
    public void ValidateRealmUriAllowsMatchingIpLiteralWhenInsecure(string realm, string registryName)
    {
        Uri uri = OrasRealmValidator.ValidateRealmUri(realm, registryName, isInsecureRegistry: true);
        Assert.AreEqual(realm, uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("https://localhost/token", "registry.example.com", false)]
    [DataRow("https://foo.localhost/token", "registry.example.com", false)]
    [DataRow("https://localhost./token", "registry.example.com", false)]
    [DataRow("https://localhost\u3002/token", "registry.example.com", false)]
    [DataRow("http://localhost/token", "192.168.1.5:5000", true)]
    [DataRow("http://localhost/token", "localhost.example.com:5000", true)]
    public void ValidateRealmUriRejectsLoopbackDnsNameRealm(string realm, string registryName, bool isInsecureRegistry)
    {
        Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
            OrasRealmValidator.ValidateRealmUri(realm, registryName, isInsecureRegistry));
    }

    [TestMethod]
    [DataRow("http://localhost:5000/auth", "localhost:5000")]
    [DataRow("https://localhost:5000/auth", "localhost:5000")]
    [DataRow("http://localhost:7000/auth", "localhost:5000")]
    [DataRow("http://foo.localhost:5000/auth", "localhost:5000")]
    [DataRow("http://localhost:5000/auth", "registry.localhost:5000")]
    [DataRow("http://localhost:5000/auth", "127.0.0.1:5000")]
    [DataRow("http://localhost:5000/auth", "[::1]:5000")]
    public async Task RealmValidatorAllowsLoopbackOnlyForInsecureLoopbackRegistry(string realm, string registryName)
    {
        var validator = new OrasRealmValidator(registryName, isInsecureRegistry: true);
        bool allowed = await validator.IsRealmAllowedAsync(
            new Uri($"http://{registryName}"),
            new Uri(realm),
            TestContext.CancellationToken);
        Assert.IsTrue(allowed);
    }

    [TestMethod]
    public async Task RealmValidatorRejectsPrivateTokenEndpoint()
    {
        var validator = new OrasRealmValidator("registry.example.com", isInsecureRegistry: false);
        bool allowed = await validator.IsRealmAllowedAsync(
            new Uri("https://registry.example.com"),
            new Uri("https://169.254.169.254/token"),
            TestContext.CancellationToken);
        Assert.IsFalse(allowed);
    }
}
