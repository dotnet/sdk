// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class TransientTestFolderFixture : IDisposable
{
    public readonly DirectoryInfo TestFolder;

    public TransientTestFolderFixture()
    {
        TestFolder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        TestFolder.Create();
    }

    public void Dispose()
    {
        try
        {
            if (TestFolder.Exists)
            {
                TestFolder.Delete(recursive: true);
            }
        }
        catch
        {
            // Handle exceptions
        }
    }
}

public class LayerCreation(ITestOutputHelper testOutput, TransientTestFolderFixture testFolderFixture) : IClassFixture<TransientTestFolderFixture>
{
    public DirectoryInfo TestFolder => testFolderFixture.TestFolder;
    public ContentStore ContentStore => new(TestFolder);

    [Fact]
    public async Task ComputesSameDescriptorForCanonicalLayerTarball()
    {
        var dataStream = new MemoryStream(Data.Layer.ConformanceLayerBytes);
        var descriptor = await Layer.DescriptorFromStream(dataStream, SchemaTypes.OciLayerGzipV1, DigestAlgorithm.sha256);
        descriptor.Should().Be(Data.Layer.ConformanceLayerDescriptor);
    }

    [Fact]
    public async Task ComputesSameLayerForCanonicalLayerTarball()
    {
        testOutput.WriteLine($"Conformance layer digest: {Data.Layer.ConformanceLayerSha256DigestString}");
        testOutput.WriteLine($"Conformance layer content length: {Data.Layer.ConformanceLayerContentLength}");
        testOutput.WriteLine($"Conformance layer descriptor: {Data.Layer.ConformanceLayerDescriptor}");
        var layer = await Data.Layer.CreateConformanceLayer(TestFolder);
        layer.Descriptor.Should().Be(Data.Layer.ConformanceLayerDescriptor);
    }
}
