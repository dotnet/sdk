// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class LayerCreation(ConformanceLayerFixture conformanceLayerFixture) : IClassFixture<ConformanceLayerFixture>
{
    [Fact]
    public async Task ComputesSameDescriptorForCanonicalLayerTarball()
    {
        var dataStream = new MemoryStream(Data.Layer.ConformanceLayerBytes);
        var descriptor = await Layer.DescriptorFromStream(dataStream, SchemaTypes.OciLayerGzipV1, DigestAlgorithm.sha256);
        descriptor.Should().Be(Data.Layer.ConformanceLayerDescriptor);
    }

    [Fact]
    public void ComputesSameLayerForCanonicalLayerTarball()
    {
        conformanceLayerFixture.Layer.Descriptor.Should().Be(Data.Layer.ConformanceLayerDescriptor);
    }
}
