// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Oci/MediaType.cs
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

namespace OrasProject.Oras.Oci;

/// <summary>
/// MediaType constants for OCI image specification.
/// Specification: https://github.com/opencontainers/image-spec/blob/v1.1.0/media-types.md
/// </summary>
public static class MediaType
{
    /// <summary>
    /// Descriptor specifies the media type for a content descriptor.
    /// </summary>
    public const string Descriptor = "application/vnd.oci.descriptor.v1+json";

    /// <summary>
    /// LayoutHeader specifies the media type for the oci-layout.
    /// </summary>
    public const string LayoutHeader = "application/vnd.oci.layout.header.v1+json";

    /// <summary>
    /// ImageIndex specifies the media type for an image index.
    /// </summary>
    public const string ImageIndex = "application/vnd.oci.image.index.v1+json";

    /// <summary>
    /// ImageManifest specifies the media type for an image manifest.
    /// </summary>
    public const string ImageManifest = "application/vnd.oci.image.manifest.v1+json";

    /// <summary>
    /// ImageConfig specifies the media type for the image configuration.
    /// </summary>
    public const string ImageConfig = "application/vnd.oci.image.config.v1+json";

    /// <summary>
    /// EmptyJSON specifies the media type for an unused blob containing the value "{}".
    /// </summary>
    public const string EmptyJson = "application/vnd.oci.empty.v1+json";

    /// <summary>
    /// ImageLayer is the media type used for layers referenced by the manifest.
    /// </summary>
    public const string ImageLayer = "application/vnd.oci.image.layer.v1.tar";

    /// <summary>
    /// ImageLayerGzip is the media type used for gzipped layers
    /// referenced by the manifest.
    /// </summary>
    public const string ImageLayerGzip = "application/vnd.oci.image.layer.v1.tar+gzip";

    /// <summary>
    /// ImageLayerZstd is the media type used for zstd compressed
    /// layers referenced by the manifest.
    /// </summary>
    public const string ImageLayerZstd = "application/vnd.oci.image.layer.v1.tar+zstd";

    /// <summary>
    /// ImageLayerNonDistributable is the media type for layers referenced by
    /// the manifest but with distribution restrictions.
    /// </summary>
    public const string ImageLayerNonDistributable = "application/vnd.oci.image.layer.nondistributable.v1.tar";

    /// <summary>
    /// ImageLayerNonDistributableGzip is the media type for
    /// gzipped layers referenced by the manifest but with distribution
    /// restrictions.
    /// </summary>
    public const string ImageLayerNonDistributableGzip = "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip";

    /// <summary>
    /// ImageLayerNonDistributableZstd is the media type for zstd
    /// compressed layers referenced by the manifest but with distribution
    /// restrictions.
    /// </summary>
    public const string ImageLayerNonDistributableZstd = "application/vnd.oci.image.layer.nondistributable.v1.tar+zstd";
}
