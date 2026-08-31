// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Oci;

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
    [DataRow(OrasProject.Oras.Docker.MediaType.Manifest)]
    [DataRow(MediaType.ImageManifest)]
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
                ManifestMediaType = OrasProject.Oras.Docker.MediaType.Manifest,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = OrasProject.Oras.Docker.MediaType.Manifest,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.AreEqual(OrasProject.Oras.Docker.MediaType.ManifestList, mediaType);
        AssertImageIndex(imageIndex, mediaType, OrasProject.Oras.Docker.MediaType.Manifest);
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
                ManifestMediaType = MediaType.ImageManifest,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = MediaType.ImageManifest,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.AreEqual(MediaType.ImageIndex, mediaType);
        AssertImageIndex(imageIndex, mediaType, MediaType.ImageManifest);
    }

    [TestMethod]
    public void GenerateImageIndexWithAnnotations()
    {
        string imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", "sha256:digest", 3, "repository", ["1.0", "2.0"]);

        var index = JsonSerializer.Deserialize<OrasProject.Oras.Oci.Index>(imageIndex);
        Assert.IsNotNull(index);
        Assert.AreEqual(2, index.SchemaVersion);
        Assert.AreEqual(MediaType.ImageIndex, index.MediaType);
        Assert.HasCount(2, index.Manifests);
        for (int i = 0; i < index.Manifests.Count; i++)
        {
            Descriptor manifest = index.Manifests[i];
            string tag = $"{i + 1}.0";
            Assert.AreEqual("mediaType", manifest.MediaType);
            Assert.AreEqual(3, manifest.Size);
            Assert.AreEqual("sha256:digest", manifest.Digest);
            Assert.IsNull(manifest.Platform);
            Assert.IsNotNull(manifest.Annotations);
            Assert.AreEqual($"docker.io/library/repository:{tag}", manifest.Annotations["io.containerd.image.name"]);
            Assert.AreEqual(tag, manifest.Annotations["org.opencontainers.image.ref.name"]);
        }
    }

    private static void AssertImageIndex(string imageIndex, string indexMediaType, string manifestMediaType)
    {
        var index = JsonSerializer.Deserialize<OrasProject.Oras.Oci.Index>(imageIndex);
        Assert.IsNotNull(index);
        Assert.AreEqual(2, index.SchemaVersion);
        Assert.AreEqual(indexMediaType, index.MediaType);
        Assert.HasCount(2, index.Manifests);
        for (int i = 0; i < index.Manifests.Count; i++)
        {
            Descriptor manifest = index.Manifests[i];
            Assert.AreEqual(manifestMediaType, manifest.MediaType);
            Assert.AreEqual(3, manifest.Size);
            Assert.AreEqual($"sha256:digest{i + 1}", manifest.Digest);
            Assert.IsNotNull(manifest.Platform);
            Assert.AreEqual($"arch{i + 1}", manifest.Platform.Architecture);
            Assert.AreEqual($"os{i + 1}", manifest.Platform.Os);
        }
    }
}
