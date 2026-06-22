// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerCliTests
{
    private static readonly ILoggerFactory s_loggerFactory = LoggerFactory.Create(_ => { });

    [Theory]
    [InlineData(true, true, true, true, DockerCli.PodmanCommand)]
    [InlineData(true, true, true, false, DockerCli.DockerCommand)]
    [InlineData(false, true, true, false, DockerCli.PodmanCommand)]
    [InlineData(false, false, true, false, DockerCli.ContainerCommand)]
    [InlineData(false, false, false, false, null)]
    public void SelectLocalCommand_PrefersDockerThenPodmanThenContainer(bool dockerAvailable, bool podmanAvailable, bool containerAvailable, bool dockerIsPodmanAlias, string? expectedCommand)
    {
        Assert.Equal(expectedCommand, DockerCli.SelectLocalCommand(dockerAvailable, podmanAvailable, containerAvailable, dockerIsPodmanAlias));
    }

    [Fact]
    public void CreateLocalRegistry_CanCreateContainerCli()
    {
        ILocalRegistry localRegistry = KnownLocalRegistryTypes.CreateLocalRegistry(KnownLocalRegistryTypes.Container, s_loggerFactory);

        var dockerCli = Assert.IsType<DockerCli>(localRegistry);
        Assert.Equal(DockerCli.ContainerCommand, dockerCli.GetCommand());
        Assert.Contains(KnownLocalRegistryTypes.Container, KnownLocalRegistryTypes.SupportedLocalRegistryTypes);
    }

    [Fact]
    public void ContainerLocalRegistry_ForcesOciImageFormat()
    {
        DestinationImageReference destinationImageReference = new(new DockerCli(DockerCli.ContainerCommand, s_loggerFactory), "repo", ["tag"]);

        Assert.True(ContainerBuilder.ShouldForceOciImageFormat(destinationImageReference));
        Assert.Equal(
            SchemaTypes.OciManifestV1,
            ContainerBuilder.GetManifestMediaType(SchemaTypes.DockerManifestV2, KnownImageFormats.Docker, destinationImageReference));
    }
}
