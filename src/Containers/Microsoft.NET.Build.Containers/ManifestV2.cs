// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Marker interface so we can return polymorphic manifests of all kinds
/// </summary>
[JsonDerivedType(typeof(ManifestV2))]
public interface IManifest
{
    [JsonIgnore]
    public string? MediaType { get; }
};

/// <summary>
/// The struct represents image manifest specification.
/// </summary>
/// <remarks>
/// https://github.com/opencontainers/image-spec/blob/main/manifest.md
/// </remarks>
public class ManifestV2 : IManifest
{

    #region Spec properties
    /// <summary>
    /// This REQUIRED property specifies the image manifest schema version.
    /// For this version of the specification, this MUST be 2 to ensure backward compatibility with older versions of Docker.
    /// The value of this field will not change. This field MAY be removed in a future version of the specification.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// This property SHOULD be used and remain compatible with earlier versions of this specification and with other similar external formats.
    /// When used, this field MUST contain the media type application/vnd.oci.image.manifest.v1+json. This field usage differs from the descriptor use of mediaType.
    /// </summary>
    [JsonPropertyName("mediaType")]
    [property:JsonConverter(typeof(MediaTypeConverter))]
    [field:JsonConverter(typeof(MediaTypeConverter))]
    public string? MediaType { get; init; }

    /// <summary>
    /// This OPTIONAL property contains the type of an artifact when the manifest is used for an artifact.
    /// This MUST be set when mediaType is set to the empty value. If defined, the value MUST comply with RFC 6838,
    /// including the naming requirements in its section 4.2, and MAY be registered with IANA.
    /// Implementations storing or copying image manifests MUST NOT error on encountering an artifactType that is unknown to the implementation.
    /// </summary>
    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// This REQUIRED property references a configuration object for a container, by digest.
    /// </summary>
    [JsonPropertyName("config")]
    public required Descriptor Config { get; init; }

    /// <summary>
    /// Each item in the array MUST be a descriptor. The array MUST have the base layer at index 0.
    /// Subsequent layers MUST then follow in stack order (i.e. from layers[0] to layers[len(layers)-1]).
    /// The final filesystem layout MUST match the result of applying the layers to an empty directory.
    /// The ownership, mode, and other attributes of the initial empty directory are unspecified.
    /// These layers are often going to be _compressed_ layers, and the size + digest of the compressed layer is what is stored in the descriptor.
    /// </summary>
    [JsonPropertyName("layers")]
    public required List<Descriptor> Layers { get; init; }

    /// <summary>
    /// This OPTIONAL property specifies a descriptor of another manifest.
    /// This value defines a weak association to a separate Merkle Directed Acyclic Graph (DAG) structure,
    /// and is used by the referrers API to include this manifest in the list of responses for the subject digest.
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Descriptor? Subject { get; set; }

    /// <summary>
    /// This OPTIONAL property contains arbitrary metadata for the image manifest. This OPTIONAL property MUST use the annotation rules.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Annotations { get; set; }
#endregion

    /// <summary>
    /// If this is set, we have a canonical digest for this manifest that came from some trusted source - most likely an upstream container registry.
    /// </summary>
    [JsonIgnore]
    public Digest? KnownDigest { get; set; }

    /// <summary>
    /// Gets the digest for this manifest.
    /// </summary>
    public Digest GetDigest() => KnownDigest ??= Digest.FromContent(DigestAlgorithm.sha256, this);
}
