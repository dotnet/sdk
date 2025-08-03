// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// An OCI Content Descriptor describing a component. At minimum requires a MediaType, Digest, and Size.
/// </summary>
/// <remarks>
/// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md"/>.
/// </remarks>
public readonly record struct Descriptor
{
    /// <summary>
    /// Media type of the referenced content.
    /// </summary>
    /// <remarks>
    /// Likely to be an OCI media type defined at <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/media-types.md" />.
    /// </remarks>
    // TODO: validate against RFC 6838 naming conventions?
    [JsonPropertyName("mediaType")]
    [property: JsonConverter(typeof(MediaTypeConverter))]
    [field: JsonConverter(typeof(MediaTypeConverter))]
    public string MediaType { get; init; }

    /// <summary>
    /// Digest of the content, specifying algorithm and value.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#digests"/>
    /// </remarks>
    [JsonPropertyName("digest")]
    public Digest Digest { get; init; }

    /// <summary>
    /// Digest of the uncompressed content, specifying algorithm and value.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#digests"/>
    /// </remarks>
    [JsonIgnore]
    public Digest? UncompressedDigest { get; init; }

    /// <summary>
    /// Size, in bytes, of the raw content.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Optional list of URLs where the content may be downloaded.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("urls")]
    public string[]? Urls { get; init; } = null;

    /// <summary>
    /// Arbitrary metadata for this descriptor.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/annotations.md"/>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("annotations")]
    public Dictionary<string, string?>? Annotations { get; init; } = null;

    /// <summary>
    /// Embedded representation of the referenced content, base-64 encoded.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#embedded-content"/>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("data")]
    public string? Data { get; init; } = null;

    /// <summary>
    /// The IANA media type of this artifact
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("artifactType")]
    public string? ArtifactType { get; init; } = null;

    public Descriptor(string mediaType, Digest digest, long size)
    {
        MediaType = mediaType;
        Digest = digest;
        Size = size;
    }

    public static Descriptor FromContent<T>(string mediaType, DigestAlgorithm algorithm, T content)
    {
        ArgumentException.ThrowIfNullOrEmpty(mediaType);
        ArgumentNullException.ThrowIfNull(content);
        var serializedContent = Json.Serialize(content);
        (long contentLength, string contentHash) = algorithm.HashInput(serializedContent);
        var digest = new Digest(algorithm, contentHash);

        return new Descriptor(mediaType, digest, contentLength);
    }

    public static Descriptor Empty = new Descriptor(
        mediaType: "application/vnd.oci.empty.v1+json",
        digest: new(DigestAlgorithm.sha256, "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"),
        size: 2
    )
    {
        Data = Convert.ToBase64String([((byte)'{'), ((byte)'}')]), // base64 of '{}'
    };
}
