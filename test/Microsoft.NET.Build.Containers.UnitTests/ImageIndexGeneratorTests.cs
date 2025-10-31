﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
    var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateDockerManifestList(images, "manifestMediaType", "imageIndexMediaType"));
    Assert.Equal(Strings.ImagesEmpty, ex.Message);
  }

  static BuiltImage EmptyWithMediaType(string mediaType) =>
      new()
      {
        Image = new()
        {
          Architecture = "amd64",
          OS = "linux",
          RootFS = RootFS.Empty with { }
        },
        Manifest = new()
        {
          MediaType = mediaType,
          Layers = [],
          SchemaVersion = 2,
          Config = new(),
        },
      };

  [Fact]
  public void UnsupportedMediaTypeThrows()
  {
    BuiltImage[] images =
    [
        EmptyWithMediaType("unsupported"),
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
        EmptyWithMediaType(supportedMediaType),
            EmptyWithMediaType("unsupported"),
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
                Image = new()
                  {
                    Architecture = "arch1",
                    OS = "os1",
                    RootFS = RootFS.Empty with { }
                  },
                Manifest =  new(){
                    KnownDigest =  new Digest(DigestAlgorithm.sha256,"digest1"),
                    MediaType = SchemaTypes.DockerManifestV2,
                    SchemaVersion = 2,
                    Config = new(),
                    Layers = [],
                }
            },
            new BuiltImage
            {
                Image = new()
                {
                    Architecture = "arch2",
                    OS = "os2",
                    RootFS = RootFS.Empty with { }
                },
                Manifest = new(){
                    KnownDigest =  new Digest(DigestAlgorithm.sha256,"digest2"),
                    MediaType = SchemaTypes.DockerManifestV2,
                    SchemaVersion = 2,
                    Config = new(),
                    Layers = [],
                }
            }
    ];

    var imageIndex = ImageIndexGenerator.GenerateImageIndex(images);
    var imageIndexJson = JsonSerializer.Serialize(imageIndex, new JsonSerializerOptions() { WriteIndented = true });
    var expectedJson = """
        {
          "schemaVersion": 2,
          "mediaType": "application/vnd.docker.distribution.manifest.list.v2+json",
          "manifests": [
            {
              "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
              "size": 154,
              "digest": "sha256:digest1",
              "platform": {
                "architecture": "arch1",
                "os": "os1"
              }
            },
            {
              "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
              "size": 154,
              "digest": "sha256:digest2",
              "platform": {
                "architecture": "arch2",
                "os": "os2"
              }
            }
          ]
        }
        """;
    imageIndexJson.Should().BeVisuallyEquivalentTo(expectedJson);
    imageIndex.MediaType.Should().Be(SchemaTypes.DockerManifestListV2);
  }

  [Fact]
  public void GenerateOciImageIndex()
  {
    BuiltImage[] images =
    [
        new BuiltImage
            {
                Image = new()
                {
                    Architecture = "arch1",
                    OS = "os1",
                    RootFS = RootFS.Empty with { }
                },
                Manifest =  new(){
                    KnownDigest = new Digest(DigestAlgorithm.sha256,"digest1"),
                    MediaType = SchemaTypes.OciManifestV1,
                    SchemaVersion = 2,
                    Config = new(),
                    Layers = [],
                },
            },
            new BuiltImage
            {
                Image = new()
                {
                    Architecture = "arch2",
                    OS = "os2",
                    RootFS = RootFS.Empty with { }
                },
                Manifest = new(){
                    KnownDigest = new Digest(DigestAlgorithm.sha256,"digest2"),
                    MediaType = SchemaTypes.OciManifestV1,
                    SchemaVersion = 2,
                    Config = new(),
                    Layers = [],
                }
            }
    ];

    var imageIndex = ImageIndexGenerator.GenerateImageIndex(images);
    var imageIndexJson = JsonSerializer.Serialize(imageIndex, new JsonSerializerOptions() { WriteIndented = true });
    var expectedJson = """
        {
          "schemaVersion": 2,
          "mediaType": "application/vnd.oci.image.index.v1+json",
          "manifests": [
            {
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "size": 144,
              "digest": "sha256:digest1",
              "platform": {
                "architecture": "arch1",
                "os": "os1"
              }
            },
            {
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "size": 144,
              "digest": "sha256:digest2",
              "platform": {
                "architecture": "arch2",
                "os": "os2"
              }
            }
          ]
        }
        """;
    imageIndexJson.Should().BeVisuallyEquivalentTo(expectedJson);
    imageIndex.MediaType.Should().Be(SchemaTypes.OciImageIndexV1);
  }

  [Fact]
  public void GenerateImageIndexWithAnnotations()
  {
    var imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", new Digest(DigestAlgorithm.sha256, "digest"), 3, "repository", ["1.0", "2.0"]);
    var imageIndexJson = JsonSerializer.Serialize(imageIndex, new JsonSerializerOptions() { WriteIndented = true });
    var expectedJson = """
        {
          "schemaVersion": 2,
          "mediaType": "application/vnd.oci.image.index.v1+json",
          "manifests": [
            {
              "mediaType": "mediaType",
              "size": 3,
              "digest": "sha256:digest",
              "annotations": {
                "io.containerd.image.name": "docker.io/library/repository:1.0",
                "org.opencontainers.image.ref.name": "1.0"
              }
            },
            {
              "mediaType": "mediaType",
              "size": 3,
              "digest": "sha256:digest",
              "annotations": {
                "io.containerd.image.name": "docker.io/library/repository:2.0",
                "org.opencontainers.image.ref.name": "2.0"
              }
            }
          ]
        }
        """;
    imageIndexJson.Should().BeVisuallyEquivalentTo(expectedJson);
  }
}
