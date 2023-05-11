// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class RegistryTests
{
    private ITestOutputHelper _testOutput;

    public RegistryTests(ITestOutputHelper output)
    {
        _testOutput = output;
    }

    [DockerDaemonAvailableFact]
    public async Task GetFromRegistry()
    {
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));
        var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();

        // Don't need rid graph for local registry image pulls - since we're only pushing single image manifests (not manifest lists)
        // as part of our setup, we could put literally anything in here. The file at the passed-in path would only get read when parsing manifests lists.
        ImageBuilder? downloadedImage = await registry.GetImageManifestAsync(
            DockerRegistryManager.BaseImage,
            DockerRegistryManager.Net6ImageTag,
            "linux-x64",
            ridgraphfile,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(downloadedImage);
    }

    [DockerDaemonAvailableFact]
    public async Task WriteToPrivateBasicRegistry()
    {
        var registryDir = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "AuthenticatedRegistry"));
        var registryCertsDir = new DirectoryInfo(Path.Combine(registryDir.FullName, "certs"));
        var registryName = "localhost:5555";
        try {
            if (!registryCertsDir.Exists)
            {
                registryCertsDir.Create();
            }
            var registryCertFile = Path.Combine(registryCertsDir.FullName, "domain.crt");

            // create, trust and export the dev cert
            new DotnetCommand(_testOutput, "dev-certs", "https", "--trust").Execute().Should().Pass();
            new DotnetCommand(_testOutput, "dev-certs", "https", "--trust", "--check").Execute().Should().Pass();
            // exporting with --no-password also generates a matching key file
            new DotnetCommand(_testOutput, $"dev-certs", "https", "--export-path", registryCertFile, "--format", "PEM", "--no-password").Execute().Should().Pass();
            // start up an authenticated registry using that dev cert
            new RunExeCommand(_testOutput, "docker", "compose", "up", "-d").WithWorkingDirectory(registryDir.FullName).Execute().Should().Pass();
            // login to that registry
            new RunExeCommand(_testOutput, "docker", "login", registryName, "--username", "testuser", "--password", "testpassword").Execute().Should().Pass();
            // push an image to that registry using username/password
            Registry localAuthed = new Registry(new($"https://{registryName}"));
            var ridgraphfile = ToolsetUtils.GetRuntimeGraphFilePath();
            Registry mcr = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.BaseImageSource));

            var sourceImage = new ImageReference(mcr, DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag);
            var destinationImage = new ImageReference(localAuthed, DockerRegistryManager.BaseImage, DockerRegistryManager.Net6ImageTag);
            ImageBuilder? downloadedImage = await mcr.GetImageManifestAsync(
                DockerRegistryManager.BaseImage,
                DockerRegistryManager.Net6ImageTag,
                "linux-x64",
                ridgraphfile,
                cancellationToken: default).ConfigureAwait(false);
            var image = downloadedImage.Build();
            localAuthed.SupportsParallelUploads = false;
            localAuthed.SupportsChunkedUpload = true;
            await localAuthed.PushAsync(image, sourceImage, destinationImage, _testOutput.WriteLine, CancellationToken.None);
        }
        finally
        {
            //stop the registry
            new RunExeCommand(_testOutput, "docker", "compose", "down").WithWorkingDirectory(registryDir.FullName).Execute().Should().Pass();
        }
    }
}
