// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/PackManifestOptions.cs
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

using OrasProject.Oras.Oci;

using System.Collections.Generic;

namespace OrasProject.Oras;

/// <summary>
/// PackManifestOptions contains optional parameters for packing a manifest.
/// </summary>
public struct PackManifestOptions
{
    /// <summary>
    /// Gets an empty set of options.
    /// </summary>
    public static PackManifestOptions None { get; }

    /// <summary>
    /// Config references a configuration object for a container, by digest
    /// For more details: https://github.com/opencontainers/image-spec/blob/v1.1.1/manifest.md#image-manifest-property-descriptions.
    /// </summary>
    public Descriptor? Config { get; set; }

    /// <summary>
    /// Layers is an array of objects, and each object id a Content Descriptor (or simply Descriptor)
    /// For more details: https://github.com/opencontainers/image-spec/blob/v1.1.1/manifest.md#image-manifest-property-descriptions.
    /// </summary>
    public IList<Descriptor>? Layers { get; set; }

    /// <summary>
    /// Subject is the subject of the manifest.
    /// This option is invalid when PackManifestVersion is PackManifestVersion1_0.
    /// </summary>
    public Descriptor? Subject { get; set; }

    /// <summary>
    /// ManifestAnnotations is OPTIONAL property. It contains arbitrary metadata for the image manifest
    /// and MUST use the annotation rules
    /// </summary>
    public IDictionary<string, string>? ManifestAnnotations { get; set; }

    /// <summary>
    /// ConfigAnnotations is the annotation map of the config descriptor.
    /// This option is valid only when Config is null.
    /// </summary>
    public IDictionary<string, string>? ConfigAnnotations { get; set; }
}

