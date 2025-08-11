// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

public class DownloadLayersTests(ITestOutputHelper testOutput, HelixTransientTestFolderFixture testFolderFixture, LoggingBuildEngineFixture loggingBuildEngineFixture) : IClassFixture<LoggingBuildEngineFixture>, IClassFixture<HelixTransientTestFolderFixture>
{
    [Fact]
    public async Task CanDownloadLayers()
    {
        loggingBuildEngineFixture.SetupBuildEngine(testOutput);
        var scenario = ManifestScenarios.SingleArchManifest;
        DownloadLayers downloadLayers = new()
        {
            BuildEngine = loggingBuildEngineFixture.BuildEngine,
            Registry = scenario.Registry,
            Repository = scenario.Repository,
            ContentStore = testFolderFixture.TestFolder.FullName,
            Layers = scenario.LayerDescriptors.Select(MakeLayerItem).ToArray()
        };
        (await downloadLayers.ExecuteAsync()).Should().BeTrue();
        loggingBuildEngineFixture.Errors.Should().BeEmpty();
        loggingBuildEngineFixture.Warnings.Should().BeEmpty();
        downloadLayers.Layers.Should().AllSatisfy(layer =>
        {
            var file = new FileInfo(layer.ItemSpec);
            file.Should().Exist();
            file.Length.Should().Be(long.Parse(layer.GetMetadata("Size"), System.Globalization.CultureInfo.InvariantCulture));
        });
    }

    public ITaskItem MakeLayerItem(Descriptor layer)
    {
        var contentStore = new ContentStore(testFolderFixture.TestFolder);
        ITaskItem item = new TaskItem(contentStore.PathForDescriptor(layer));
        item.SetMetadata("Digest", layer.Digest.ToString());
        item.SetMetadata("Size", layer.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        item.SetMetadata("MediaType", layer.MediaType);
        return item;
    }
}

