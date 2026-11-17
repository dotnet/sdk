// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Oci/Descriptor.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Descriptor describes a content addressable blob.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/descriptor.md
/// </summary>
public class Descriptor
{
    /// <summary>
    /// MediaType is the media type of the object this descriptor refers to.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; set; }

    /// <summary>
    /// Digest is the digest of the targeted content.
    /// </summary>
    [JsonPropertyName("digest")]
    public required string Digest { get; set; }

    /// <summary>
    /// Size specifies the size in bytes of the blob.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Urls specifies a list of URLs from which this object may be downloaded.
    /// </summary>
    [JsonPropertyName("urls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IList<string>? Urls { get; set; }

    /// <summary>
    /// Annotations contains arbitrary metadata relating to the targeted content.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IDictionary<string, string>? Annotations { get; set; }

    /// <summary>
    /// Data is an embedding of the targeted content, encoded as a byte array.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? Data { get; set; }

    /// <summary>
    /// Platform describes the platform which the image in the manifest runs on.
    /// </summary>
    [JsonPropertyName("platform")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Platform? Platform { get; set; }

    /// <summary>
    /// ArtifactType is the type of an artifact when the descriptor points to an artifact.
    /// </summary>
    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Creates a <see cref="Descriptor"/> for the given content and media type,
    /// computing the digest and size from the supplied data.
    /// </summary>
    /// <param name="data">The content to describe.</param>
    /// <param name="mediaType">The media type of the content.</param>
    /// <returns>A descriptor identifying the content.</returns>
    public static Descriptor Create(Span<byte> data, string mediaType)
    {
        byte[] byteData = data.ToArray();
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = Content.Digest.ComputeSha256(byteData),
            Size = byteData.Length
        };
    }

    /// <summary>
    /// Gets a descriptor for the empty JSON object (<c>{}</c>).
    /// </summary>
    public static Descriptor Empty => new()
    {
        MediaType = Oci.MediaType.EmptyJson,
        Digest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
        Size = 2,
        Data = [0x7B, 0x7D]
    };

    internal BasicDescriptor BasicDescriptor => new BasicDescriptor(MediaType, Digest, Size);

    internal static bool IsNullOrInvalid(Descriptor? descriptor)
    {
        return descriptor == null || string.IsNullOrWhiteSpace(descriptor.Digest) || string.IsNullOrWhiteSpace(descriptor.MediaType);
    }

    /// <summary>
    /// IsManifestType is to check if the given descriptor a manifest
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    internal static bool IsManifestType(Descriptor descriptor) =>
        descriptor.MediaType is
            Docker.MediaType.Manifest or
            Oci.MediaType.ImageManifest or
            Docker.MediaType.ManifestList or
            Oci.MediaType.ImageIndex;
}
