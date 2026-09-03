// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.IntegrationTests;
[TestClass]
public class DockerRegistryTests : SdkTest
{
    private TestLoggerFactory? _loggerFactory;
    private TestLoggerFactory LoggerFactory => _loggerFactory ??= new TestLoggerFactory(Log);

    [TestMethod]
    [Ignore("https://github.com/dotnet/sdk/issues/49300")]
    public async Task GetFromRegistry()
    {
        var loggerFactory = new TestLoggerFactory(Log);
        var logger = loggerFactory.CreateLogger(nameof(GetFromRegistry));
        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);
        var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();

        // Don't need rid graph for local registry image pulls - since we're only pushing single image manifests (not manifest lists)
        // as part of our setup, we could put literally anything in here. The file at the passed-in path would only get read when parsing manifests lists.
        ImageBuilder? downloadedImage = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);

        Assert.IsNotNull(downloadedImage);
    }

    [TestMethod]
    [Ignore("https://github.com/dotnet/sdk/issues/42820")]
    public async Task WriteToPrivateBasicRegistry()
    {
        ILogger logger = LoggerFactory.CreateLogger(nameof(WriteToPrivateBasicRegistry));
        var registryDir = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "AuthenticatedRegistry"));
        var registryAuthDir = new DirectoryInfo(Path.Combine(registryDir.FullName, "auth"));
        var registryCertsDir = new DirectoryInfo(Path.Combine(registryDir.FullName, "certs"));
        var registryName = "localhost:5555";
        try
        {
            if (!registryCertsDir.Exists)
            {
                registryCertsDir.Create();
            }
            var registryCertFile = Path.Combine(registryCertsDir.FullName, "domain.crt");

            // export dev cert, using --no-password also generates a matching key file
            new DotnetCommand(Log, $"dev-certs", "https", "--trust").Execute().Should().Pass();
            new DotnetCommand(Log, $"dev-certs", "https", "--export-path", registryCertFile, "--format", "PEM", "--no-password").Execute().Should().Pass();
            // start up an authenticated registry using that dev cert
            ContainerCli.RunCommand(Log,
                "-d", "--rm",
                "--name", "auth-registry",
                "-p", "5555:5000",
                "-e", "REGISTRY_AUTH=htpasswd",
                "-e", "REGISTRY_AUTH_HTPASSWD_REALM=Registry Realm",
                "-e", "REGISTRY_AUTH_HTPASSWD_PATH=/auth/htpasswd",
                "-e", "REGISTRY_HTTP_TLS_CERTIFICATE=/certs/domain.crt",
                "-e", "REGISTRY_HTTP_TLS_KEY=/certs/domain.key",
                "-v", $"{registryCertsDir.FullName}:/certs:z",
                "-v", $"{registryAuthDir.FullName}:/auth:z",
                "registry:2")
            .WithWorkingDirectory(registryDir.FullName).Execute().Should().Pass();
            // verify that the registry container started successfully
            ContainerCli.InspectCommand(Log, "auth-registry").Execute().Should().Pass();
            // login to that registry
            ContainerCli.LoginCommand(Log, "--username", "testuser", "--password", "testpassword", registryName).Execute().Should().Pass();
            // push an image to that registry using username/password
            Registry localAuthed = new(new Uri($"https://{registryName}"), logger, RegistryMode.Push, settings: new() { ParallelUploadEnabled = false, ForceChunkedUpload = true });
            var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();
            Registry mcr = new(DockerRegistryManager.BaseImageSource, logger, RegistryMode.Pull);

            var sourceImage = new SourceImageReference(mcr, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net6ImageTag, null);
            var destinationImage = new DestinationImageReference(localAuthed, DockerRegistryManager.RuntimeBaseImage, new[] { DockerRegistryManager.Net6ImageTag });
            ImageBuilder? downloadedImage = await mcr.GetImageManifestAsync(
                DockerRegistryManager.RuntimeBaseImage,
                DockerRegistryManager.Net6ImageTag,
                "linux-x64",
                ToolsetUtils.RidGraphManifestPicker,
                cancellationToken: default).ConfigureAwait(false);
            var image = downloadedImage.Build();
            await localAuthed.PushAsync(image, sourceImage, destinationImage, CancellationToken.None);
        }
        finally
        {
            //stop the registry
            ContainerCli.StopCommand(Log, "auth-registry").WithWorkingDirectory(registryDir.FullName).Execute().Should().Pass();
        }
    }
}
