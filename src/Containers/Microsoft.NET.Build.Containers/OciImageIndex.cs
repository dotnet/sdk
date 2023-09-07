// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The struct represents OCI image index specification.
/// </summary>
/// <remarks>
/// https://github.com/opencontainers/image-spec/blob/main/image-index.md
/// </remarks>
internal readonly record struct OciImageIndex
{
    /// <summary>
    /// This REQUIRED property specifies the image manifest schema version.
    /// For this version of the specification, this MUST be 2 to ensure backward compatibility with older versions of Docker.
    /// The value of this field will not change. This field MAY be removed in a future version of the specification.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// This property SHOULD be used and remain compatible with earlier versions of this specification and with other similar external formats.
    /// When used, this field MUST contain the media type application/vnd.oci.image.index.v1+json. This field usage differs from the descriptor use of mediaType.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; init; }

    /// <summary>
    /// This REQUIRED property contains a list of manifests for specific platforms.
    /// While this property MUST be present, the size of the array MAY be zero.
    /// Each object in manifests includes a set of descriptor.

    /// </summary>
    [JsonPropertyName("manifests")]
    public required List<OciImageManifestDescriptor> Manifests { get; init; }

    /// <summary>
    /// This OPTIONAL property contains arbitrary metadata for the image index. This OPTIONAL property MUST use the annotation rules.
    /// </summary>
    [JsonPropertyName("annotations")]
    public Dictionary<string, string>? Annotations { get; init; }
}


/// <summary>
/// The class holds the well-known annotation keys of a <see cref="OciImageIndex"/>
/// </summary>
/// <remarks>
/// https://github.com/opencontainers/image-spec/blob/main/annotations.md#pre-defined-annotation-keys
/// </remarks>
internal static class OciAnnotations
{
    /// <summary>
    /// AnnotationRefName is the annotation key for the name of the reference for a target.
    /// SHOULD only be considered valid when on descriptors on `index.json` within image layout.
    /// </summary>
    public const string AnnotationRefName = "org.opencontainers.image.ref.name";
}

/// <summary>
/// The struct represents manifest descriptor inside a <see cref="OciImageIndex"/>
/// </summary>
/// <remarks>
/// https://github.com/opencontainers/image-spec/blob/main/image-index.md
/// https://github.com/opencontainers/image-spec/blob/main/descriptor.md#properties
/// </remarks>
internal readonly record struct OciImageManifestDescriptor
{
    /// <summary>
    /// MediaType is the media type of the object this schema refers to.
    /// This descriptor property has additional restrictions for manifests.
    /// Implementations MUST support at least the following media types:
    /// 
    /// - application/vnd.oci.image.manifest.v1+json
    /// 
    /// Also, implementations SHOULD support the following media types:
    /// - application/vnd.oci.image.index.v1+json(nested index)
    /// 
    /// Image indexes concerned with portability SHOULD use one of the above media types.
    /// Future versions of the spec MAY use a different mediatype (i.e. a new versioned format).
    /// An encountered mediaType that is unknown to the implementation MUST NOT generate an error.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; init; }

    /// <summary>
    /// This REQUIRED property is the digest of the targeted content,
    /// conforming to the requirements outlined in Digests.
    /// Retrieved content SHOULD be verified against this digest when consumed via untrusted sources.
    /// </summary>
    [JsonPropertyName("digest")]
    public required string Digest { get; init; }

    /// <summary>
    /// This REQUIRED property specifies the size, in bytes, of the raw content.
    /// This property exists so that a client will have an expected size for the content before processing.
    /// If the length of the retrieved content does not match the specified length, the content SHOULD NOT be trusted.
    /// </summary>
    [JsonPropertyName("size")]
    public required long Size { get; init; }

    /// <summary>
    /// This OPTIONAL property contains arbitrary metadata for this descriptor.
    /// This OPTIONAL property MUST use the annotation rules.
    /// </summary>
    [JsonPropertyName("annotations")]
    public Dictionary<string, string>? Annotations { get; init; }
}
