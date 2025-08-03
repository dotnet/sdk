// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

public record Scenario(string Registry, string Repository, string Tag, Descriptor[] ManifestDescriptors, Descriptor[] ConfigDescriptors, Descriptor[] LayerDescriptors);

public static class ManifestScenarios
{
    public static Scenario SingleArchManifest => new(
        "mcr.microsoft.com",
        "dotnet/runtime",
        "10.0.0-preview.6-noble-amd64",
        [
            new(SchemaTypes.DockerManifestV2, new(DigestAlgorithm.sha256, "bd04fbec8522502608c556038e2bc766e3d4ed2ab7f0b661dd9e2b3c305005dc"), 1080)
        ],
        [
            new(SchemaTypes.DockerContainerV1, new(DigestAlgorithm.sha256, "f991f793246a717da29e8257a6b18df67b2120fca0a1f149667f992078b10f7d"), 2963)
        ],
        [
            new(SchemaTypes.DockerLayerGzip, new(DigestAlgorithm.sha256, "b71466b94f266b4c2e0881749670e5b88ab7a0fd4ca4a4cdf26cb45e4bde7e4e"), 29723215),
            new(SchemaTypes.DockerLayerGzip, new(DigestAlgorithm.sha256, "93aaf34b20e979de46ff6f92e6b52a91af30758b610fb6a71ccc39ff1413cdb3"), 16817581),
            new(SchemaTypes.DockerLayerGzip, new(DigestAlgorithm.sha256, "f0672408aab4806d992c3f8194f201609e6037e97b68bffb09d03c3a093cd586"), 3532),
            new(SchemaTypes.DockerLayerGzip, new(DigestAlgorithm.sha256, "ee0379483e2cf881d542467c0dfd7a7ed71fe4cc10fc8a23100ffccedc82b3e6"), 36345490),
            new(SchemaTypes.DockerLayerGzip, new(DigestAlgorithm.sha256, "581456a34d77131e71e93c53a9ed78386e1070603775afda388538bce99ef39b"), 154)
        ]);
}

public class DownloadContainerManifestTests(ITestOutputHelper testOutput, TransientTestFolderFixture testFolderFixture, LoggingBuildEngineFixture loggingBuildEngineFixture) : IClassFixture<LoggingBuildEngineFixture>, IClassFixture<TransientTestFolderFixture>
{
    [Fact]
    public async Task CanResolveSingleArchManifest()
    {
        loggingBuildEngineFixture.SetupBuildEngine(testOutput);
        DownloadContainerManifest downloadContainerManifest = new()
        {
            BuildEngine = loggingBuildEngineFixture.BuildEngine,
            ContentStore = testFolderFixture.TestFolder.FullName,
            Registry = "mcr.microsoft.com",
            Repository = "dotnet/runtime",
            Tag = "10.0.0-preview.6-noble-amd64" // this is known to have a single architecture manifest, and it's a stable tag so digests should not change
        };
        await downloadContainerManifest.ExecuteAsync();
        loggingBuildEngineFixture.Errors.Should().BeEmpty();
        loggingBuildEngineFixture.Warnings.Should().BeEmpty();
        (Descriptor[] manifests, Descriptor[] configs, Descriptor[] layers) = GetDescriptorsFromTask(downloadContainerManifest);
        manifests.Should().Equal(ManifestScenarios.SingleArchManifest.ManifestDescriptors);
        configs.Should().Equal(ManifestScenarios.SingleArchManifest.ConfigDescriptors);
        layers.Should().Equal(ManifestScenarios.SingleArchManifest.LayerDescriptors);
    }

    public DownloadContainerManifest InitScenario(Scenario scenario) => 
        new() {
            BuildEngine = loggingBuildEngineFixture.BuildEngine,
            ContentStore = testFolderFixture.TestFolder.FullName,
            Registry = scenario.Registry,
            Repository = scenario.Repository,
            Tag = scenario.Tag
        };

    public static (Descriptor[] Manifests, Descriptor[] Configs, Descriptor[] Layers) GetDescriptorsFromTask(DownloadContainerManifest task)
    {
        return (
            task.Manifests.Select(GetDescriptorFromItem).ToArray(),
            task.Configs.Select(GetDescriptorFromItem).ToArray(),
            task.Layers.Select(GetDescriptorFromItem).ToArray());
    }

    public static Descriptor GetDescriptorFromItem(ITaskItem item) => new Descriptor(
            item.GetMetadata("MediaType"),
            Digest.Parse(item.GetMetadata("Digest")),
            long.Parse(item.GetMetadata("Size"), System.Globalization.CultureInfo.InvariantCulture));
}
