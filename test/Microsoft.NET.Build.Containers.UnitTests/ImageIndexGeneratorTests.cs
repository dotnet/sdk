// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ImageIndexGeneratorTests
{
    [TestMethod]
    public void ImagesCannotBeEmpty()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.ThrowsExactly<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.AreEqual(Strings.ImagesEmpty, ex.Message);
    }

    [TestMethod]
    public void ImagesCannotBeEmpty_SpecifiedMediaType()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.ThrowsExactly<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images, "manifestMediaType", "imageIndexMediaType"));
        Assert.AreEqual(Strings.ImagesEmpty, ex.Message);
    }

    [TestMethod]
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

        var ex = Assert.ThrowsExactly<NotSupportedException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.AreEqual(string.Format(Strings.UnsupportedMediaType, "unsupported"), ex.Message);
    }

    [TestMethod]
    [DataRow(SchemaTypes.DockerManifestV2)]
    [DataRow(SchemaTypes.OciManifestV1)]
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

        var ex = Assert.ThrowsExactly<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.AreEqual(Strings.MixedMediaTypes, ex.Message);
    }

    [TestMethod]
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
        Assert.AreEqual("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.docker.distribution.manifest.list.v2+json\",\"manifests\":[{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.AreEqual(SchemaTypes.DockerManifestListV2, mediaType);
    }

    [TestMethod]
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
        Assert.AreEqual("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.AreEqual(SchemaTypes.OciImageIndexV1, mediaType);
    }

    [TestMethod]
    public void GenerateImageIndexWithAnnotations()
    {
        string imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", "sha256:digest", 3, "repository", ["1.0", "2.0"]);
        Assert.AreEqual("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:1.0\",\"org.opencontainers.image.ref.name\":\"1.0\"}},{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:2.0\",\"org.opencontainers.image.ref.name\":\"2.0\"}}]}", imageIndex);
    }
}
