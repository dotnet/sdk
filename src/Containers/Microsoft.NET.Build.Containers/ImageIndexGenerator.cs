// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Oci;

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

        if (manifestMediaType == OrasProject.Oras.Docker.MediaType.Manifest)
        {
            return (GenerateImageIndex(images, OrasProject.Oras.Docker.MediaType.Manifest, OrasProject.Oras.Docker.MediaType.ManifestList), OrasProject.Oras.Docker.MediaType.ManifestList);
        }
        else if (manifestMediaType == MediaType.ImageManifest)
        {
            return (GenerateImageIndex(images, MediaType.ImageManifest, MediaType.ImageIndex), MediaType.ImageIndex);
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

        var manifests = new Descriptor[images.Length];

        for (int i = 0; i < images.Length; i++)
        {
            manifests[i] = new Descriptor
            {
                MediaType = manifestMediaType,
                Size = images[i].Manifest.Length,
                Digest = images[i].ManifestDigest,
                Platform = new Platform
                {
                    Architecture = images[i].Architecture,
                    Os = images[i].OS
                }
            };
        }

        var imageIndex = new OrasProject.Oras.Oci.Index
        {
            SchemaVersion = 2,
            MediaType = imageIndexMediaType,
            Manifests = manifests
        };

        return GetJsonStringFromImageIndex(imageIndex);
    }

    internal static string GenerateImageIndexWithAnnotations(
        string manifestMediaType,
        string manifestDigest,
        long manifestSize,
        string repository,
        string[] tags,
        Platform? platform = null)
    {
        string containerdImageNamePrefix = repository.Contains('/') ? "docker.io/" : "docker.io/library/";

        var manifests = new Descriptor[tags.Length];
        for (int i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            manifests[i] = new Descriptor
            {
                MediaType = manifestMediaType,
                Size = manifestSize,
                Digest = manifestDigest,
                Platform = platform,
                Annotations = new Dictionary<string, string>
                {
                    { "io.containerd.image.name", $"{containerdImageNamePrefix}{repository}:{tag}" },
                    { "org.opencontainers.image.ref.name", tag }
                }
            };
        }

        var index = new OrasProject.Oras.Oci.Index
        {
            SchemaVersion = 2,
            MediaType = MediaType.ImageIndex,
            Manifests = manifests
        };

        return GetJsonStringFromImageIndex(index);
    }

    private static string GetJsonStringFromImageIndex(OrasProject.Oras.Oci.Index imageIndex)
    {
        // To avoid things like \u002B for '+' especially in media types ("application/vnd.oci.image.manifest.v1\u002Bjson"), we use UnsafeRelaxedJsonEscaping.
        var escapeOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(imageIndex, escapeOptions);
    }
}
