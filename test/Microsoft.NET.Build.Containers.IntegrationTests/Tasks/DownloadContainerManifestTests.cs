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

    public static Scenario MultiArchManifest => new(
        "mcr.microsoft.com",
        "dotnet/runtime",
        "10.0.0-preview.6-noble",
        [
            new Descriptor(SchemaTypes.DockerManifestV2, Digest.Parse("sha256:bd04fbec8522502608c556038e2bc766e3d4ed2ab7f0b661dd9e2b3c305005dc"),1080),
            new Descriptor(SchemaTypes.DockerManifestV2, Digest.Parse("sha256:e5b030ee6f553ff6bd74fa8ce7eb9759ef0894f0542e1b7e9e69a7465bfc9909"),1080),
            new Descriptor(SchemaTypes.DockerManifestV2, Digest.Parse("sha256:a887cf14f38d13f3d71012dbc1a74fe11fefaa343cadf94661aa7437d590e721"),1080)
        ],
        [
            new Descriptor(SchemaTypes.DockerContainerV1, Digest.Parse("sha256:f991f793246a717da29e8257a6b18df67b2120fca0a1f149667f992078b10f7d"),2963),
            new Descriptor(SchemaTypes.DockerContainerV1, Digest.Parse("sha256:6ec93d06b5b4e1af883d2145efe8468b537df1d174dcb45c7de43576c66e78e0"),2993),
            new Descriptor(SchemaTypes.DockerContainerV1, Digest.Parse("sha256:72fd5d8ea5d169276c6a30bb064278640a0fde3f20b464e742da0fab26f45248"),2971)
        ],
        [
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:b71466b94f266b4c2e0881749670e5b88ab7a0fd4ca4a4cdf26cb45e4bde7e4e"),29723215),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:93aaf34b20e979de46ff6f92e6b52a91af30758b610fb6a71ccc39ff1413cdb3"),16817581),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:f0672408aab4806d992c3f8194f201609e6037e97b68bffb09d03c3a093cd586"),3532),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:ee0379483e2cf881d542467c0dfd7a7ed71fe4cc10fc8a23100ffccedc82b3e6"),36345490),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:581456a34d77131e71e93c53a9ed78386e1070603775afda388538bce99ef39b"),154),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:5775aaee0b6caf578e138eda76ce3385180e0796b81e02b9edf4909084017d62"),26851072),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:e2bac3f8b76ce0337121933b2cc7fabf1f9587d6d4e7c507c9f94a6c499be64d"),16279582),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:cfebab4d4ca862afbc148ec14b28c29e5fe6f62d5e7aa3b6e596b14f07c2ed1a"),3564),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:e58c893e8c29b5db7024a76edab10fc1ed6c6042ac9f105bb3b55273a4eba38e"),33649646),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:309d6ddfd5de4f2d9f56b978940790f43679c78f99323bf6573a0835374ac7b7"),153),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:49a8ca9a328e179fe07d40f7f2fd5fb2860b5c45463c288b64f05be521173d2e"),28860377),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:70a25b2805c1ce672e11176836f56afc05d16ab9d9c7728a4b95f0b3e12b0489"),16793109),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:f84f2fcf56b6e1bb71401c8fdcd3da4ad5300833c8a5062bcc6a896094e1847a"),3566),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:e3598f08947b508545f75e530a609d3fee38a1568997c7a1855ae48c65c5eeee"),34295761),
            new Descriptor(SchemaTypes.DockerLayerGzip, Digest.Parse("sha256:8e12a8bc4f1a7836a02ff462b039e9d75365cbec1d40ceaceff2c1327864a1b9"),154)
        ]);
}

public class DownloadContainerManifestTests(ITestOutputHelper testOutput, TransientTestFolderFixture testFolderFixture, LoggingBuildEngineFixture loggingBuildEngineFixture) : IClassFixture<LoggingBuildEngineFixture>, IClassFixture<TransientTestFolderFixture>
{
    [Fact]
    public async Task CanResolveSingleArchManifest()
    {
        loggingBuildEngineFixture.SetupBuildEngine(testOutput);
        var scenario = ManifestScenarios.SingleArchManifest;
        DownloadContainerManifest downloadContainerManifest = InitScenario(scenario);
        await downloadContainerManifest.ExecuteAsync();
        loggingBuildEngineFixture.Errors.Should().BeEmpty();
        loggingBuildEngineFixture.Warnings.Should().BeEmpty();
        (Descriptor[] manifests, Descriptor[] configs, Descriptor[] layers) = GetDescriptorsFromTask(downloadContainerManifest);
        manifests.Should().Equal(scenario.ManifestDescriptors);
        configs.Should().Equal(scenario.ConfigDescriptors);
        layers.Should().Equal(scenario.LayerDescriptors);
    }

    [Fact]
    public async Task CanResolveMultiArchManifestAndChildManifests()
    {
        loggingBuildEngineFixture.SetupBuildEngine(testOutput);
        var scenario = ManifestScenarios.MultiArchManifest;
        DownloadContainerManifest downloadContainerManifest = InitScenario(scenario);
        await downloadContainerManifest.ExecuteAsync();
        loggingBuildEngineFixture.Errors.Should().BeEmpty();
        loggingBuildEngineFixture.Warnings.Should().BeEmpty();
        (Descriptor[] manifests, Descriptor[] configs, Descriptor[] layers) = GetDescriptorsFromTask(downloadContainerManifest);
        manifests.Should().Equal(scenario.ManifestDescriptors);
        configs.Should().Equal(scenario.ConfigDescriptors);
        layers.Should().Equal(scenario.LayerDescriptors);
    }

    public DownloadContainerManifest InitScenario(Scenario scenario) =>
        new()
        {
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
