// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal static class ImageIndexGenerator
{
    /// <summary>
    /// Generates an image index from the given images.
    /// </summary>
    /// <param name="images">Images to generate image index from.</param>
    /// <returns>Returns json string of image index and image index mediaType.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    internal static (string, string) GenerateImageIndex(BuiltImage[] images)
    {
        if (images.Length == 0)
        {
            throw new ArgumentException(Strings.ImagesEmpty);
        }

        string manifestMediaType = images[0].ManifestMediaType;

        if (!images.All(image => string.Equals(image.ManifestMediaType, manifestMediaType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(Strings.MixedMediaTypes);
        }

        if (manifestMediaType == SchemaTypes.DockerManifestV2)
        {
            return (GenerateImageIndex(images, SchemaTypes.DockerManifestV2, SchemaTypes.DockerManifestListV2), SchemaTypes.DockerManifestListV2);
        }
        else if (manifestMediaType == SchemaTypes.OciManifestV1)
        {
            return (GenerateImageIndex(images, SchemaTypes.OciManifestV1, SchemaTypes.OciImageIndexV1), SchemaTypes.OciImageIndexV1);
        }
        else
        {
            throw new NotSupportedException(string.Format(Strings.UnsupportedMediaType, manifestMediaType));
        }
    }

    /// <summary>
    /// Generates an image index from the given images.
    /// </summary>
    /// <param name="images">Images to generate image index from.</param>
    /// <param name="manifestMediaType">Media type of the manifest.</param>
    /// <param name="imageIndexMediaType">Media type of the produced image index.</param>
    /// <returns>Returns json string of image index and image index mediaType.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    internal static string GenerateImageIndex(BuiltImage[] images, string manifestMediaType, string imageIndexMediaType)
    {
        if (images.Length == 0)
        {
            throw new ArgumentException(Strings.ImagesEmpty);
        }

        // Here we are using ManifestListV2 struct, but we could use ImageIndexV1 struct as well.
        // We are filling the same fields, so we can use the same struct.
        var manifests = new PlatformSpecificManifest[images.Length];
        
        for (int i = 0; i < images.Length; i++)
        {
            manifests[i] = new PlatformSpecificManifest
            {
                mediaType = manifestMediaType,
                size = images[i].Manifest.Length,
                digest = images[i].ManifestDigest,
                platform = new PlatformInformation
                {
                    architecture = images[i].Architecture!,
                    os = images[i].OS!
                }
            };
        }

        var imageIndex = new ManifestListV2
        {
            schemaVersion = 2,
            mediaType = imageIndexMediaType,
            manifests = manifests
        };

        return GetJsonStringFromImageIndex(imageIndex);
    }

    internal static string GenerateImageIndexWithAnnotations(string manifestMediaType, string manifestDigest, long manifestSize, string repository, string[] tags)
    {
        string containerdImageNamePrefix = repository.Contains('/') ? "docker.io/" : "docker.io/library/";
        
        var manifests = new PlatformSpecificOciManifest[tags.Length];
        for (int i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            manifests[i] = new PlatformSpecificOciManifest
            {
                mediaType = manifestMediaType,
                size = manifestSize,
                digest = manifestDigest,
                annotations = new Dictionary<string, string> 
                {
                    { "io.containerd.image.name", $"{containerdImageNamePrefix}{repository}:{tag}" },
                    { "org.opencontainers.image.ref.name", tag } 
                }
            };
        }

        var index = new ImageIndexV1
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.OciImageIndexV1,
            manifests = manifests
        };

        return GetJsonStringFromImageIndex(index);
    }

    private static string GetJsonStringFromImageIndex<T>(T imageIndex)
    {
        var nullIgnoreOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        // To avoid things like \u002B for '+' especially in media types ("application/vnd.oci.image.manifest.v1\u002Bjson"), we use UnsafeRelaxedJsonEscaping.
        var escapeOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.SerializeToNode(imageIndex, nullIgnoreOptions)?.ToJsonString(escapeOptions) ?? "";
    }
}
