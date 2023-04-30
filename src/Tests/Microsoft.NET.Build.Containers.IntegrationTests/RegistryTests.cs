// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Containers.UnitTests;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class RegistryTests
{
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
}
