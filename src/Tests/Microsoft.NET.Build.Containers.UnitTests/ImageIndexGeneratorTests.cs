// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ImageIndexGeneratorTests
{
    [Fact]
    public void ImagesCannotBeEmpty()
    {
        ImageInfo[] images = Array.Empty<ImageInfo>();
        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.ImagesEmpty, ex.Message);
    }

    [Fact]
    public void ManifestIsNotJsonObjectThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "",
                Digest = "",
                Manifest = "[]"
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal($"Manifest should be a JSON object. (Parameter 'Manifest')", ex.Message);
    }

    [Fact]
    public void NullMediaTypeThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "",
                Digest = "",
                Manifest = "{}"
            }
        };

        var ex = Assert.Throws<NotSupportedException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(string.Format(Strings.UnsupportedMediaType, "null"), ex.Message);
    }

    [Fact]
    public void UnsupportedMediaTypeThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "",
                Digest = "",
                Manifest = "{\"mediaType\":\"unsupported\"}"
            }
        };

        var ex = Assert.Throws<NotSupportedException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(string.Format(Strings.UnsupportedMediaType, "unsupported"), ex.Message);
    }

    [Fact]
    public void ConfigIsNotJsonObjectThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "[]",
                Digest = "",
                Manifest = "{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\"}"
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal($"Config should be a JSON object. (Parameter 'Config')", ex.Message);
    }

    [Fact]
    public void ConfigDoesNotContainArchitectureThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "{}",
                Digest = "",
                Manifest = "{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\"}"
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal($"Config should contain 'architecture'. (Parameter 'Config')", ex.Message);
    }

    [Fact]
    public void ConfigDoesNotContainOsThrows()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "{\"architecture\":\"amd64\"}",
                Digest = "",
                Manifest = "{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\"}"
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal($"Config should contain 'os'. (Parameter 'Config')", ex.Message);
    }

    [Theory]
    [InlineData(SchemaTypes.DockerManifestV2)]
    [InlineData(SchemaTypes.OciManifestV1)]
    public void ImagesWithMixedMediaTypes(string supportedMediaType)
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "{\"architecture\":\"arch1\",\"os\":\"os1\"}",
                Digest = "",
                Manifest =  $"{{\"mediaType\":\"{supportedMediaType}\"}}"
            },
            new ImageInfo
            {
                Config = "",
                Digest = "",
                Manifest = "{\"mediaType\":\"anotherMediaType\"}"
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.MixedMediaTypes, ex.Message);
    }

    [Fact]
    public void GenerateDockerManifestList()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "{\"architecture\":\"arch1\",\"os\":\"os1\"}",
                Digest = "sha256:digest1",
                Manifest = $"{{\"mediaType\":\"{SchemaTypes.DockerManifestV2}\"}}"
            },
            new ImageInfo
            {
                Config = "{\"architecture\":\"arch2\",\"os\":\"os2\"}",
                Digest = "sha256:digest2",
                Manifest = $"{{\"mediaType\":\"{SchemaTypes.DockerManifestV2}\"}}"
            }
        };

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.docker.distribution.manifest.list.v2\\u002Bjson\",\"manifests\":[{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2\\u002Bjson\",\"size\":68,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\",\"variant\":null,\"features\":null,\"os.version\":null}},{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2\\u002Bjson\",\"size\":68,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\",\"variant\":null,\"features\":null,\"os.version\":null}}]}", imageIndex);
        Assert.Equal(SchemaTypes.DockerManifestListV2, mediaType);
    }

    [Fact]
    public void GenerateOciImageIndex()
    {
        ImageInfo[] images = new ImageInfo[]
        {
            new ImageInfo
            {
                Config = "{\"architecture\":\"arch1\",\"os\":\"os1\"}",
                Digest = "sha256:digest1",
                Manifest = $"{{\"mediaType\":\"{SchemaTypes.OciManifestV1}\"}}"
            },
            new ImageInfo
            {
                Config = "{\"architecture\":\"arch2\",\"os\":\"os2\"}",
                Digest = "sha256:digest2",
                Manifest = $"{{\"mediaType\":\"{SchemaTypes.OciManifestV1}\"}}"
            }
        };

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1\\u002Bjson\",\"manifests\":[{\"mediaType\":\"application/vnd.oci.image.manifest.v1\\u002Bjson\",\"size\":58,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\",\"variant\":null,\"features\":null,\"os.version\":null},\"annotations\":null},{\"mediaType\":\"application/vnd.oci.image.manifest.v1\\u002Bjson\",\"size\":58,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\",\"variant\":null,\"features\":null,\"os.version\":null},\"annotations\":null}]}", imageIndex);
        Assert.Equal(SchemaTypes.OciImageIndexV1, mediaType);
    }
}
