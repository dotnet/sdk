// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/RepositoryOptions.cs
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
using OrasProject.Oras.Content;
using OrasProject.Oras.Registry.Remote.Auth;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// RepositoryOption is used to configure a remote repository.
/// </summary>
public struct RepositoryOptions
{
    /// <summary>
    /// The default chunk size used for chunked blob uploads: 5 MiB.
    /// </summary>
    public const int DefaultBlobUploadChunkSize = 5 * 1024 * 1024;

    /// <summary>
    /// Client is the underlying HTTP client used to access the remote registry.
    /// </summary>
    public required IClient Client { get; set; }

    /// <summary>
    /// Reference references the remote repository.
    /// </summary>
    public required Reference Reference { get; set; }

    /// <summary>
    /// PlainHttp signals the transport to access the remote repository via HTTP
    /// instead of HTTPS.
    /// </summary>
    public bool PlainHttp { get; set; }

    /// <summary>
    /// ManifestMediaTypes is used in `Accept` header for resolving manifests
    /// from references. It is also used in identifying manifests and blobs from
    /// descriptors. If null, default manifest media types are used.
    /// </summary>
    public IEnumerable<string>? ManifestMediaTypes { get; set; }

    /// <summary>
    /// TagListPageSize specifies the page size when invoking the tag list API.
    /// If zero, the page size is determined by the remote registry.
    /// Reference: https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    public int TagListPageSize { get; set; }

    /// <summary>
    /// SkipReferrersGc specifies whether to delete the dangling referrers
    /// index when referrers tag schema is utilized.
    ///  - If false, the old referrers index will be deleted after the new one is successfully uploaded.
    ///  - If true, the old referrers index is kept.
    /// By default, it is disabled (set to false). See also:
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#referrers-tag-schema
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#pushing-manifests-with-subject
    ///  - https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#deleting-manifests
    /// </summary>
    public bool SkipReferrersGc { get; set; }

    /// <summary>
    /// BlobUploadMode specifies how blobs are uploaded. The default is
    /// <see cref="BlobUploadMode.Monolithic"/>.
    /// </summary>
    public BlobUploadMode BlobUploadMode { get; set; }

    /// <summary>
    /// BlobUploadChunkSize specifies the maximum number of bytes sent in each <c>PATCH</c>
    /// request during a chunked blob upload. The getter returns 5 MiB when the value is zero
    /// or not set. If the registry advertises a larger <c>OCI-Chunk-Min-Length</c>, that value
    /// takes precedence for the upload. Registry minimums requiring a buffer larger than both
    /// the configured size and the 5 MiB default are rejected before reading content. The final
    /// chunk may be smaller. Increase this option explicitly to permit a larger minimum;
    /// each upload buffers one chunk for replay.
    /// </summary>
    public int BlobUploadChunkSize
    {
        get => _blobUploadChunkSize == 0 ? DefaultBlobUploadChunkSize : _blobUploadChunkSize;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "BlobUploadChunkSize must be greater than zero.");
            }
            _blobUploadChunkSize = value;
        }
    }

    /// <summary>
    /// PartitionId is an optional cache partition identifier for multi-partition scenarios.
    /// When set, authentication tokens are isolated by this ID, allowing different credentials
    /// to be cached separately for the same upstream registry.
    /// <para>
    /// Use cases include:
    /// <list type="bullet">
    /// <item>Multi-partition services where each customer has different upstream credentials</item>
    /// <item>
    /// Sync scenarios where the same upstream registry is accessed with different tokens
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// The consumer is responsible for determining what value to use (e.g., customer ID,
    /// hash of full image reference, or other business logic).
    /// </para>
    /// </summary>
    public string? PartitionId { get; set; }

    /// <summary>
    /// MaxMetadataBytes specifies a limit on how many response bytes are allowed
    /// in the server's response to the metadata APIs, such as catalog list, tag
    /// list, and referrers list.
    /// The getter returns a default value of 4 MiB if the value is zero or not set.
    /// The setter throws an <see cref="ArgumentOutOfRangeException"/> if the value is less than or equal to zero.
    /// </summary>
    public long MaxMetadataBytes
    {
        get => _maxMetadataBytes == 0 ? OciLimits.MaxManifestBytes : _maxMetadataBytes;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxMetadataBytes must be greater than zero.");
            }
            _maxMetadataBytes = value;
        }
    }

    private long _maxMetadataBytes;
    private int _blobUploadChunkSize;
}
