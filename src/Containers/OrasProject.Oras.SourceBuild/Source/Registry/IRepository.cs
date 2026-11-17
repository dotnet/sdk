// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IRepository.cs
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

using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using System.Collections.Generic;
using System.Threading;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Repository is an ORAS target and an union of the blob and the manifest CASs.
/// As specified by https://docs.docker.com/registry/spec/api/, it is natural to
/// assume that IResolver interface only works for manifests. Tagging a
/// blob may be resulted in an `UnsupportedException` error. However, this interface
/// does not restrict tagging blobs.
/// Since a repository is an union of the blob and the manifest CASs, all
/// operations defined in the `IBlobStore` are executed depending on the media
/// type of the given descriptor accordingly.
/// Furthermore, this interface also provides the ability to enforce the
/// separation of the blob and the manifests CASs.
/// </summary>
public interface IRepository : ITarget, IBlobLocationProvider, IReferenceFetchable, IReferencePushable, IDeletable, ITagListable, IMounter
{
    /// <summary>
    /// Blobs provides access to the blob CAS only, which contains config blobs, layers, and other generic blobs.
    /// </summary>
    IBlobStore Blobs { get; }

    /// <summary>
    /// Manifests provides access to the manifest CAS only.
    /// </summary>
    IManifestStore Manifests { get; }

    /// <summary>
    /// FetchReferrersAsync retrieves referrers for the given descriptor
    /// and returns a stream of descriptors asynchronously for consumption.
    /// If referrers API is not supported, the function falls back to a tag
    /// schema for retrieving referrers.
    /// If the referrers are supported via an API, the state is updated accordingly.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    /// <param name="descriptor">
    /// The target descriptor whose referrers are to be retrieved.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// An asynchronous enumerable of Descriptor objects representing the referrers.
    /// </returns>
    IAsyncEnumerable<Descriptor> FetchReferrersAsync(
        Descriptor descriptor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FetchReferrersAsync retrieves referrers for the given descriptor and artifact type
    /// and returns a stream of descriptors asynchronously for consumption.
    /// If referrers API is not supported, the function falls back to a tag
    /// schema for retrieving referrers.
    /// If the referrers are supported via an API, the state is updated accordingly.
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#listing-referrers
    /// </summary>
    /// <param name="descriptor">
    /// The target descriptor whose referrers are to be retrieved.
    /// </param>
    /// <param name="artifactType">
    /// Filters referrers by the specified artifact type.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// An asynchronous enumerable of Descriptor objects representing the referrers
    /// filtered by artifact type.
    /// </returns>
    IAsyncEnumerable<Descriptor> FetchReferrersAsync(
        Descriptor descriptor,
        string? artifactType,
        CancellationToken cancellationToken = default);

}
