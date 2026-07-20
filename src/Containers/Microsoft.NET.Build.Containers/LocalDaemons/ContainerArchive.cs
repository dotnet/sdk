// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Text.Json.Nodes;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Produces importable Docker or OCI tar streams for local runtimes that load images from files or standard input.
/// </summary>
/// <remarks>
/// OCI archives use the
/// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#content">required image-layout content</see>
/// and may be transported as tar archives as described by the
/// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#oci-image-layout-specification">OCI Image Layout Specification</see>.
/// Docker archives use Moby's de facto archive contract: the
/// <see href="https://github.com/moby/moby/blob/c5b47383f5108b7a0c05b4334ef31644158553cf/daemon/internal/image/tarexport/tarexport.go#L15-L26"><c>manifest.json</c> schema</see>
/// identifies the configuration, tags, and ordered layer paths consumed by the
/// <see href="https://github.com/moby/moby/blob/c5b47383f5108b7a0c05b4334ef31644158553cf/daemon/internal/image/tarexport/load.go#L57-L130">Moby archive loader</see>.
/// </remarks>
internal static class ContainerArchive
{
    /// <summary>
    /// The SDK emits SHA-256 descriptors, whose content is stored beneath <c>blobs/sha256/&lt;encoded&gt;</c> as required by the
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#blobs">OCI blobs layout</see>.
    /// </summary>
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

        string configTarballPath = $"{GetRequiredImageSha(image)}.json";
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

    /// <summary>
    /// Records archive entry paths using the fields interpreted by Moby's
    /// <see href="https://github.com/moby/moby/blob/c5b47383f5108b7a0c05b4334ef31644158553cf/daemon/internal/image/tarexport/tarexport.go#L20-L26"><c>manifestItem</c> contract</see>.
    /// </summary>
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

        JsonNode manifestNode = new JsonArray(
            new JsonObject
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

    /// <summary>
    /// Writes the required layout marker and version defined by the
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#oci-layout-file"><c>oci-layout</c> file specification</see>.
    /// </summary>
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

    /// <summary>
    /// Stores the image manifest at the content-addressed path prescribed by the
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#blobs">OCI blobs layout</see>.
    /// </summary>
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

    /// <summary>
    /// Creates the required image-layout entry point defined by the
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#indexjson-file"><c>index.json</c> file specification</see>.
    /// </summary>
    private static async Task WriteIndexJsonForOciImage(
        TarWriter writer,
        BuiltImage image,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string indexJson = ImageIndexGenerator.GenerateImageIndexWithAnnotations(
            image.ManifestMediaType,
            image.ManifestDigest,
            image.Manifest.Length,
            destinationReference.Repository,
            destinationReference.Tags,
            new PlatformInformation
            {
                architecture = image.Architecture,
                os = image.OS
            });

        using MemoryStream indexStream = new(Encoding.UTF8.GetBytes(indexJson));
        PaxTarEntry indexEntry = new(TarEntryType.RegularFile, "index.json")
        {
            DataStream = indexStream
        };
        await writer.WriteEntryAsync(indexEntry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Places layers, configuration, and the manifest in the content-addressed
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#blobs">OCI blobs layout</see>.
    /// </summary>
    private static async Task WriteOciImageToBlobs(
        TarWriter writer,
        BuiltImage image,
        SourceImageReference sourceReference,
        CancellationToken cancellationToken)
    {
        await WriteImageLayers(writer, image, sourceReference, d => $"{BlobsPath}/{d.Substring("sha256:".Length)}", cancellationToken)
            .ConfigureAwait(false);
        await WriteImageConfig(writer, image, $"{BlobsPath}/{GetRequiredImageSha(image)}", cancellationToken).ConfigureAwait(false);
        await WriteManifestForOciImage(writer, image, cancellationToken).ConfigureAwait(false);
    }

    private static string GetRequiredImageSha(BuiltImage image)
        => image.ImageSha ?? throw new InvalidOperationException("An image SHA is required to create a local container archive.");

    /// <summary>
    /// Makes the multi-platform image index discoverable through the required
    /// <see href="https://github.com/opencontainers/image-spec/blob/v1.1.1/image-layout.md#indexjson-file"><c>index.json</c> entry point</see>.
    /// </summary>
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
            destinationReference.Tags,
            // OCI defines this descriptor as a referrer to the multi-platform index, so it has no single platform.
            // Apple container requires platform metadata here; Docker and Podman accept unknown values.
            new PlatformInformation { architecture = "unknown", os = "unknown" });

        using MemoryStream indexStream = new(Encoding.UTF8.GetBytes(indexJson));
        PaxTarEntry indexEntry = new(TarEntryType.RegularFile, "index.json")
        {
            DataStream = indexStream
        };
        await writer.WriteEntryAsync(indexEntry, cancellationToken).ConfigureAwait(false);
    }
}
