// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Text.Json.Nodes;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Serializes built images into archive formats accepted by local container runtimes.
/// </summary>
internal static class ContainerArchive
{
    private const string BlobsPath = "blobs/sha256";

    public static async Task WriteImageToStreamAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        if (image.ManifestMediaType == SchemaTypes.DockerManifestV2)
        {
            await WriteDockerImageToStreamAsync(image, sourceReference, destinationReference, imageStream, cancellationToken);
        }
        else if (image.ManifestMediaType == SchemaTypes.OciManifestV1)
        {
            await WriteOciImageToStreamAsync(image, sourceReference, destinationReference, imageStream, cancellationToken);
        }
        else
        {
            throw new NotSupportedException(Resource.FormatString(nameof(Strings.UnsupportedMediaTypeForTarball), image.ManifestMediaType));
        }
    }

    internal static async Task WriteDockerImageToStreamAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        JsonArray layerTarballPaths = new();
        await WriteImageLayers(writer, image, sourceReference, d => $"{d.Substring("sha256:".Length)}/layer.tar", cancellationToken, layerTarballPaths)
            .ConfigureAwait(false);

        string configTarballPath = $"{image.ImageSha!}.json";
        await WriteImageConfig(writer, image, configTarballPath, cancellationToken).ConfigureAwait(false);
        await WriteManifestForDockerImage(writer, destinationReference, configTarballPath, layerTarballPaths, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task WriteOciImageToStreamAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        await WriteOciImageToBlobs(writer, image, sourceReference, cancellationToken).ConfigureAwait(false);
        await WriteIndexJsonForOciImage(writer, image, destinationReference, cancellationToken).ConfigureAwait(false);
        await WriteOciLayout(writer, cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteMultiArchOciImageToStreamAsync(
        MultiArchImage multiArchImage,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        Debug.Assert(multiArchImage.Images is not null);
        foreach (BuiltImage image in multiArchImage.Images)
        {
            await WriteOciImageToBlobs(writer, image, sourceReference, cancellationToken).ConfigureAwait(false);
        }

        await WriteIndexJsonForMultiArchOciImage(writer, multiArchImage, destinationReference, cancellationToken)
            .ConfigureAwait(false);
        await WriteOciLayout(writer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteImageLayers(
        TarWriter writer,
        BuiltImage image,
        SourceImageReference sourceReference,
        Func<string, string> layerPathFunc,
        CancellationToken cancellationToken,
        JsonArray? layerTarballPaths = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (Descriptor descriptor in image.LayerDescriptors)
        {
            if (sourceReference.Registry is not { } registry)
            {
                throw new NotImplementedException(Resource.FormatString(
                    nameof(Strings.MissingLinkToRegistry),
                    descriptor.Digest,
                    sourceReference.Registry?.ToString() ?? "<null>"));
            }

            cancellationToken.ThrowIfCancellationRequested();
            string localPath = await registry.DownloadBlobAsync(sourceReference.Repository, descriptor, cancellationToken).ConfigureAwait(false);
            string layerTarballPath = layerPathFunc(descriptor.Digest);
            await writer.WriteEntryAsync(localPath, layerTarballPath, cancellationToken).ConfigureAwait(false);
            layerTarballPaths?.Add(layerTarballPath);
        }
    }

    private static async Task WriteImageConfig(TarWriter writer, BuiltImage image, string configPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream configStream = new(Encoding.UTF8.GetBytes(image.Config));
        PaxTarEntry configEntry = new(TarEntryType.RegularFile, configPath)
        {
            DataStream = configStream
        };
        await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteManifestForDockerImage(
        TarWriter writer,
        DestinationImageReference destinationReference,
        string configTarballPath,
        JsonArray layerTarballPaths,
        CancellationToken cancellationToken)
    {
        JsonArray tagsNode = new();
        foreach (string tag in destinationReference.Tags)
        {
            tagsNode.Add($"{destinationReference.Repository}:{tag}");
        }

        JsonNode manifestNode = new JsonArray(new JsonObject
        {
            { "Config", configTarballPath },
            { "RepoTags", tagsNode },
            { "Layers", layerTarballPaths }
        });

        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream manifestStream = new(Encoding.UTF8.GetBytes(manifestNode.ToJsonString()));
        PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
        {
            DataStream = manifestStream
        };
        await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteOciLayout(TarWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream layoutStream = new(Encoding.UTF8.GetBytes("{\"imageLayoutVersion\": \"1.0.0\"}"));
        PaxTarEntry layoutEntry = new(TarEntryType.RegularFile, "oci-layout")
        {
            DataStream = layoutStream
        };
        await writer.WriteEntryAsync(layoutEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteManifestForOciImage(TarWriter writer, BuiltImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string manifestPath = $"{BlobsPath}/{image.ManifestDigest.Substring("sha256:".Length)}";
        using MemoryStream manifestStream = new(Encoding.UTF8.GetBytes(image.Manifest));
        PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, manifestPath)
        {
            DataStream = manifestStream
        };
        await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteIndexJsonForOciImage(
        TarWriter writer,
        BuiltImage image,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string indexJson = ImageIndexGenerator.GenerateImageIndexWithAnnotations(
            SchemaTypes.OciManifestV1,
            image.ManifestDigest,
            image.Manifest.Length,
            destinationReference.Repository,
            destinationReference.Tags);

        using MemoryStream indexStream = new(Encoding.UTF8.GetBytes(indexJson));
        PaxTarEntry indexEntry = new(TarEntryType.RegularFile, "index.json")
        {
            DataStream = indexStream
        };
        await writer.WriteEntryAsync(indexEntry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteOciImageToBlobs(
        TarWriter writer,
        BuiltImage image,
        SourceImageReference sourceReference,
        CancellationToken cancellationToken)
    {
        await WriteImageLayers(writer, image, sourceReference, d => $"{BlobsPath}/{d.Substring("sha256:".Length)}", cancellationToken)
            .ConfigureAwait(false);
        await WriteImageConfig(writer, image, $"{BlobsPath}/{image.ImageSha!}", cancellationToken).ConfigureAwait(false);
        await WriteManifestForOciImage(writer, image, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteIndexJsonForMultiArchOciImage(
        TarWriter writer,
        MultiArchImage multiArchImage,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string manifestListDigest = DigestUtils.ComputeSha256Digest(multiArchImage.ImageIndex);
        string manifestListSha = DigestUtils.GetEncoded(manifestListDigest);
        using (MemoryStream manifestListStream = new(Encoding.UTF8.GetBytes(multiArchImage.ImageIndex)))
        {
            PaxTarEntry manifestListEntry = new(TarEntryType.RegularFile, $"{BlobsPath}/{manifestListSha}")
            {
                DataStream = manifestListStream
            };
            await writer.WriteEntryAsync(manifestListEntry, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        string indexJson = ImageIndexGenerator.GenerateImageIndexWithAnnotations(
            multiArchImage.ImageIndexMediaType,
            manifestListDigest,
            multiArchImage.ImageIndex.Length,
            destinationReference.Repository,
            destinationReference.Tags);

        using MemoryStream indexStream = new(Encoding.UTF8.GetBytes(indexJson));
        PaxTarEntry indexEntry = new(TarEntryType.RegularFile, "index.json")
        {
            DataStream = indexStream
        };
        await writer.WriteEntryAsync(indexEntry, cancellationToken).ConfigureAwait(false);
    }
}
