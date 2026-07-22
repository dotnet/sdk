// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
                ManifestMediaType = "unsupported",
                Architecture = "unknown",
                OS = "unknown"
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
                Architecture = "unknown",
                OS = "unknown"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = "anotherMediaType",
                Architecture = "unknown",
                OS = "unknown"
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
    public void GenerateOciImageIndexWithAnnotations()
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
            }
        ];
        Dictionary<string, string> annotations = new()
        {
            ["org.opencontainers.image.source"] = "https://github.com/dotnet/sdk",
            ["org.opencontainers.image.revision"] = "abcdef"
        };
        Dictionary<string, string> annotationsInReverseOrder = new()
        {
            ["org.opencontainers.image.revision"] = "abcdef",
            ["org.opencontainers.image.source"] = "https://github.com/dotnet/sdk"
        };

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images, annotations);
        var (imageIndexFromReverseOrder, _) = ImageIndexGenerator.GenerateImageIndex(images, annotationsInReverseOrder);

        Assert.AreEqual(imageIndex, imageIndexFromReverseOrder);
        Assert.AreEqual(SchemaTypes.OciImageIndexV1, mediaType);

        using JsonDocument document = JsonDocument.Parse(imageIndex);
        JsonElement root = document.RootElement;
        Assert.AreEqual(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual(SchemaTypes.OciImageIndexV1, root.GetProperty("mediaType").GetString());

        JsonElement manifest = root.GetProperty("manifests")[0];
        Assert.AreEqual(SchemaTypes.OciManifestV1, manifest.GetProperty("mediaType").GetString());
        Assert.AreEqual(3, manifest.GetProperty("size").GetInt64());
        Assert.AreEqual("sha256:digest1", manifest.GetProperty("digest").GetString());
        Assert.AreEqual("arch1", manifest.GetProperty("platform").GetProperty("architecture").GetString());
        Assert.AreEqual("os1", manifest.GetProperty("platform").GetProperty("os").GetString());

        JsonElement serializedAnnotations = root.GetProperty("annotations");
        Assert.HasCount(2, serializedAnnotations.EnumerateObject());
        Assert.AreEqual("https://github.com/dotnet/sdk", serializedAnnotations.GetProperty("org.opencontainers.image.source").GetString());
        Assert.AreEqual("abcdef", serializedAnnotations.GetProperty("org.opencontainers.image.revision").GetString());
    }

    [TestMethod]
    public void DockerManifestListDoesNotIncludeOciAnnotations()
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
            }
        ];

        var (imageIndex, _) = ImageIndexGenerator.GenerateImageIndex(images, new Dictionary<string, string> { ["example.com/key"] = "value" });

        Assert.IsFalse(imageIndex.Contains("annotations", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GenerateImageIndexWithAnnotations()
    {
        string imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", "sha256:digest", 3, "repository", ["1.0", "2.0"]);
        Assert.AreEqual("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:1.0\",\"org.opencontainers.image.ref.name\":\"1.0\"}},{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:2.0\",\"org.opencontainers.image.ref.name\":\"2.0\"}}]}", imageIndex);
    }
}
