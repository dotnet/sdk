// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using System.Formats.Tar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers.LocalDaemons;

internal class OciArchiveFileRegistry : ArchiveFileRegistry
{
    public OciArchiveFileRegistry(string archiveOutputPath) : base(archiveOutputPath, ContainerImageArchiveFormat.OpenContainerInitiative)
    {
    }

    protected override async Task WriteImageToStreamAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken)
    {
        // Format reference: https://github.com/opencontainers/image-spec/blob/main/image-layout.md

        cancellationToken.ThrowIfCancellationRequested();
        await using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, "blobs"), cancellationToken);
        await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, "blobs/sha256"), cancellationToken);

        // layer blobs
        foreach (var d in image.LayerDescriptors)
        {
            if (sourceReference.Registry is { } registry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string localPath = await registry.DownloadBlobAsync(sourceReference.Repository, d, cancellationToken).ConfigureAwait(false);

                string layerTarballPath = $"blobs/sha256/{DigestUtils.TrimDigest(d.Digest)}";
                await writer.WriteEntryAsync(localPath, layerTarballPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotImplementedException(Resource.FormatString(
                    nameof(Strings.MissingLinkToRegistry),
                    d.Digest,
                    sourceReference.Registry?.ToString() ?? "<null>"));
            }
        }

        // image config blob
        cancellationToken.ThrowIfCancellationRequested();
        await using (MemoryStream config = new(Encoding.UTF8.GetBytes(image.Config)))
        {
            await writer
                .WriteEntryAsync(
                    new PaxTarEntry(TarEntryType.RegularFile, $"blobs/sha256/{DigestUtils.TrimDigest(image.ImageDigest)}") { DataStream = config },
                    cancellationToken).ConfigureAwait(false);
        }

        // manifest blob
        string manifestJson = JsonSerializer.SerializeToNode(image.Manifest)?.ToJsonString() ?? "";
        string manifestDigest = DigestUtils.GetDigest(manifestJson);
        string manifestTarballPath = $"blobs/sha256/{DigestUtils.TrimDigest(manifestDigest)}";
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        await using (MemoryStream manifestStream = new(manifestBytes))
        {
            await writer
                .WriteEntryAsync(
                    new PaxTarEntry(TarEntryType.RegularFile, manifestTarballPath) { DataStream = manifestStream },
                    cancellationToken).ConfigureAwait(false);
        }

        // oci-layout
        cancellationToken.ThrowIfCancellationRequested();
        JsonObject ociLayout = new() { ["imageLayoutVersion"] = "1.0.0" };
        using (MemoryStream ociLayoutStream = new(Encoding.UTF8.GetBytes(ociLayout.ToJsonString())))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, "oci-layout")
            {
                DataStream = ociLayoutStream
            };
            await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
        }

        // index.json
        cancellationToken.ThrowIfCancellationRequested();
        OciImageIndex index = CreateIndex(destinationReference, manifestDigest, manifestBytes.Length);
        string indexJson = JsonSerializer.SerializeToNode(index)?.ToJsonString() ?? "";
        using (MemoryStream indexStream = new(Encoding.UTF8.GetBytes(indexJson)))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, "index.json")
            {
                DataStream = indexStream
            };
            await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private OciImageIndex CreateIndex(DestinationImageReference destinationReference, string manifestDigest, long manifestSize)
    {
        OciImageIndex index = new()
        {
            SchemaVersion = 2,
            MediaType = "application/vnd.oci.image.index.v1+json",
            Manifests = new List<OciImageManifestDescriptor>(),
            Annotations = new Dictionary<string, string>()
        };
        
        // image manifest for all tags
        foreach (var tag in destinationReference.Tags)
        {
            index.Manifests.Add(new OciImageManifestDescriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = manifestDigest,
                Size = manifestSize,
                Annotations = new Dictionary<string, string>
                {
                    [OciAnnotations.AnnotationRefName] = $"{destinationReference.Repository}:{tag}"
                }
            });
        }

        return index;
    }
}
