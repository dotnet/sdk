// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Oci/Index.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using OrasProject.Oras.Serialization;

namespace OrasProject.Oras.Oci;

/// <summary>
/// Index is a higher-level manifest which points to specific image manifests, ideal for one or more platforms.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/image-index.md
/// </summary>
public class Index : Versioned
{
    /// <summary>
    /// MediaType specifies the type of this document data structure.
    /// </summary>
    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? MediaType { get; set; }

    /// <summary>
    /// ArtifactType is the type of an artifact when the index is used for an artifact.
    /// </summary>
    [JsonPropertyName("artifactType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ArtifactType { get; set; }

    /// <summary>
    /// Manifests references platform-specific manifests.
    /// </summary>
    [JsonPropertyName("manifests")]
    public required IList<Descriptor> Manifests { get; set; }

    /// <summary>
    /// Subject is an optional link to another manifest that this index refers to.
    /// </summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Descriptor? Subject { get; set; }

    /// <summary>
    /// Annotations contains arbitrary metadata for the image index.
    /// </summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IDictionary<string, string>? Annotations { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Index"/> class.
    /// </summary>
    public Index() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Index"/> class for the given manifests,
    /// with the OCI image index media type and schema version 2.
    /// </summary>
    /// <param name="manifests">The platform-specific manifests referenced by the index.</param>
    [SetsRequiredMembers]
    public Index(IList<Descriptor> manifests)
    {
        Manifests = manifests;
        MediaType = Oci.MediaType.ImageIndex;
        SchemaVersion = 2;
    }

    internal static (Descriptor Descriptor, byte[] Content) GenerateIndex(
        IList<Descriptor> manifests)
    {
        var index = new Index(manifests);
        var indexContent = OciJsonSerializer.SerializeToUtf8Bytes(index);
        return (Descriptor.Create(indexContent, Oci.MediaType.ImageIndex), indexContent);
    }
}
