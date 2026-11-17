// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IBlobLocationProvider.cs
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

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote.Exceptions;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry;

/// <summary>
/// IBlobLocationProvider provides the ability to retrieve blob location URLs from registries.
/// This interface is separate from <see cref="IBlobStore"/> because not all storage backends
/// support blob location retrieval (e.g., manifest stores).
/// </summary>
public interface IBlobLocationProvider
{
    /// <summary>
    /// GetBlobLocationAsync retrieves the location URL for a blob without downloading its content.
    /// Most OCI Distribution Spec v1.1.1 registries return a redirect with a blob location in the header
    /// instead of returning the content directly on a /v2/{name}/blobs/{digest} request.
    /// This method captures that location URL.
    /// 
    /// Returns null if the registry returns the content directly (HTTP 200) instead of a redirect.
    /// 
    /// Reference: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#pulling-blobs
    /// </summary>
    /// <param name="target">The descriptor identifying the blob</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The blob location URL if a redirect is returned, otherwise null.
    /// A relative Location header is resolved against the request URI, which may yield a URL on the
    /// registry host that requires registry credentials rather than a direct storage backend URL.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the provided HttpClient has AllowAutoRedirect enabled</exception>
    /// <exception cref="HttpIOException">Thrown when the response is invalid</exception>
    /// <exception cref="NotFoundException">Thrown when the blob is not found</exception>
    /// <exception cref="ResponseException">Thrown when the request fails</exception>
    Task<Uri?> GetBlobLocationAsync(Descriptor target, CancellationToken cancellationToken = default);
}
