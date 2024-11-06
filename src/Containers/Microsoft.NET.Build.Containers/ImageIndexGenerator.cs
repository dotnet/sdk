// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers;

internal struct ImageInfo
{
    internal string Config { get; init; }
    internal string ManifestDigest { get; init; }
    internal string Manifest { get; init; }
    internal string ManifestMediaType { get; init; }
}

internal static class ImageIndexGenerator
{
    /// <summary>
    /// Generates an image index from the given images.
    /// </summary>
    /// <param name="imageInfos"></param>
    /// <returns>Returns json string of image index and image index mediaType.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    internal static (string, string) GenerateImageIndex(ImageInfo[] imageInfos)
    {
        if (imageInfos.Length == 0)
        {
            throw new ArgumentException(string.Format(Strings.ImagesEmpty));
        }

        string firstManifestMediaType = imageInfos[0].ManifestMediaType;

        if (firstManifestMediaType == SchemaTypes.DockerManifestV2)
        {
            return GenerateDockerManifestList(imageInfos, firstManifestMediaType);
        }
        else if (firstManifestMediaType == SchemaTypes.OciManifestV1)
        {
            return GenerateOciImageIndex(imageInfos, firstManifestMediaType);
        }
        else
        {
            throw new NotSupportedException(string.Format(Strings.UnsupportedMediaType, firstManifestMediaType));
        }
    }

    private static (string, string) GenerateDockerManifestList(ImageInfo[] images, string firstManifestMediaType)
    {
        var manifests = new PlatformSpecificManifest[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];

            if (i > 0 && image.ManifestMediaType != firstManifestMediaType)
            {
                throw new ArgumentException(Strings.MixedMediaTypes);
            }

            var manifest = new PlatformSpecificManifest
            {
                mediaType = firstManifestMediaType,
                size = image.Manifest.Length,
                digest = image.ManifestDigest,
                platform = GetArchitectureAndOsFromConfig(image)
            };
            manifests[i] = manifest;
        }

        var dockerManifestList = new ManifestListV2
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.DockerManifestListV2,
            manifests = manifests
        };

        return (JsonSerializer.SerializeToNode(dockerManifestList)?.ToJsonString() ?? "", dockerManifestList.mediaType);
    }

    private static (string, string) GenerateOciImageIndex(ImageInfo[] images, string firstManifestMediaType)
    {
        var manifests = new PlatformSpecificOciManifest[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];

            if (i > 0 && image.ManifestMediaType != firstManifestMediaType)
            {
                throw new ArgumentException(Strings.MixedMediaTypes);
            }

            var manifest = new PlatformSpecificOciManifest
            {
                mediaType = firstManifestMediaType,
                size = image.Manifest.Length,
                digest = image.ManifestDigest,
                platform = GetArchitectureAndOsFromConfig(image)
            };
            manifests[i] = manifest;
        }

        var ociImageIndex = new ImageIndexV1
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.OciImageIndexV1,
            manifests = manifests
        };

        return (JsonSerializer.SerializeToNode(ociImageIndex)?.ToJsonString() ?? "", ociImageIndex.mediaType);
    }

    private static PlatformInformation GetArchitectureAndOsFromConfig(ImageInfo image)
    {
        var configJson = JsonNode.Parse(image.Config) as JsonObject ??
            throw new ArgumentException($"{nameof(image.Config)} should be a JSON object.", nameof(image.Config));

        var architecture = configJson["architecture"]?.ToString() ??
            throw new ArgumentException($"{nameof(image.Config)} should contain 'architecture'.", nameof(image.Config));

        var os = configJson["os"]?.ToString() ??
            throw new ArgumentException($"{nameof(image.Config)} should contain 'os'.", nameof(image.Config));

        return new PlatformInformation { architecture = architecture, os = os };
    }
}
