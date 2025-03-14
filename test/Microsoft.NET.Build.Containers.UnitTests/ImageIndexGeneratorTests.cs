// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ImageIndexGeneratorTests
{
    [Fact]
    public void ImagesCannotBeEmpty()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.ImagesEmpty, ex.Message);
    }

    [Fact]
    public void ImagesCannotBeEmpty_SpecifiedMediaType()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images, "manifestMediaType", "imageIndexMediaType"));
        Assert.Equal(Strings.ImagesEmpty, ex.Message);
    }

    [Fact]
    public void UnsupportedMediaTypeThrows()
    {
        BuiltImage[] images = 
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = "unsupported"
            }
        ];

        var ex = Assert.Throws<NotSupportedException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(string.Format(Strings.UnsupportedMediaType, "unsupported"), ex.Message);
    }

    [Theory]
    [InlineData(SchemaTypes.DockerManifestV2)]
    [InlineData(SchemaTypes.OciManifestV1)]
    public void ImagesWithMixedMediaTypes(string supportedMediaType)
    {
        BuiltImage[] images = 
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = supportedMediaType,
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = "anotherMediaType"
            }
        ];

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.MixedMediaTypes, ex.Message);
    }

    [Fact]
    public void GenerateDockerManifestList()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.DockerManifestV2,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = SchemaTypes.DockerManifestV2,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.docker.distribution.manifest.list.v2+json\",\"manifests\":[{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.Equal(SchemaTypes.DockerManifestListV2, mediaType);
    }

    [Fact]
    public void GenerateOciImageIndex()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.OciManifestV1,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = SchemaTypes.OciManifestV1,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.Equal(SchemaTypes.OciImageIndexV1, mediaType);
    }

    [Fact]
    public void GenerateImageIndexWithAnnotations()
    {
        string imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", "sha256:digest", 3, "repository", ["1.0", "2.0"]);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:1.0\",\"org.opencontainers.image.ref.name\":\"1.0\"}},{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:2.0\",\"org.opencontainers.image.ref.name\":\"2.0\"}}]}", imageIndex);
    }
}
