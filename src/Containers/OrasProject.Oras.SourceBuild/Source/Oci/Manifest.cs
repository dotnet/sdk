// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Oci/Manifest.cs
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

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Manifest describes an image or artifact.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/manifest.md
/// </summary>
public class Manifest : Versioned, IJsonOnDeserialized
{
    /// <summary>
    /// MediaType specifies the type of this document data structure.
    /// </summary>
    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? MediaType { get; set; }

    /// <summary>
    /// ArtifactType is the type of an artifact when the manifest is used for an artifact.
    /// </summary>
    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Config references a configuration object for a container, by digest.
    /// </summary>
    [JsonPropertyName("config")]
    public required Descriptor Config { get; set; }

    /// <summary>
    /// Layers is an indexed list of layers referenced by the manifest.
    /// </summary>
    [JsonPropertyName("layers")]
    public required IList<Descriptor> Layers { get; set; }

    /// <summary>
    /// Subject is an optional link to another manifest that this manifest refers to.
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Descriptor? Subject { get; set; }

    /// <summary>
    /// Annotations contains arbitrary metadata for the image manifest.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IDictionary<string, string>? Annotations { get; set; }

    /// <summary>
    /// Validates spec-required fields after deserialization. Per the OCI image manifest
    /// spec, <c>config</c> is REQUIRED and must be a valid descriptor. System.Text.Json's
    /// <c>required</c> only enforces key presence, so a <c>"config": null</c> (or a
    /// present-but-empty descriptor) would otherwise slip through — reject it here.
    /// </summary>
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (Descriptor.IsNullOrInvalid(Config))
        {
            throw new JsonException(
                "Image manifest 'config' is required and must be a valid descriptor.");
        }
    }
}
