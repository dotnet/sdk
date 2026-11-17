// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/ReferenceProxy.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Content;

namespace OrasProject.Oras.Registry;

/// <summary>
/// ReferenceProxy is a caching proxy dedicated for <see cref="IReferenceFetchable"/>.
/// The first fetch call of a described content will read from the remote and cache the fetched content.
/// The subsequent fetch call will read from the local cache.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ReferenceProxy"/> class with an existing <see cref="Proxy"/>
/// and <see cref="IReferenceFetchable"/>.
/// </remarks>
/// <param name="referenceFetchable">The reference fetcher to use</param>
/// <param name="proxy">The CAS proxy to use for caching</param>
internal class ReferenceProxy(IReferenceFetchable referenceFetchable, Proxy proxy) : IReferenceFetchable, IReadOnlyStorage
{
    private IReferenceFetchable ReferenceFetchable { get; } = referenceFetchable;

    private Proxy Proxy { get; } = proxy;

    /// <summary>
    /// Fetches the content identified by the reference from the remote and caches the fetched content.
    /// </summary>
    /// <param name="reference">The reference to fetch</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A tuple containing the descriptor and the content stream</returns>
    public async Task<(Descriptor, Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default)
        => await FetchAsync(reference, new FetchOptions(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Fetches the content identified by the reference from the remote with additional options and caches the fetched content.
    /// </summary>
    /// <param name="reference">The reference to fetch</param>
    /// <param name="options">Options for the fetch operation</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A tuple containing the descriptor and the content stream</returns>
    public async Task<(Descriptor, Stream)> FetchAsync(string reference, FetchOptions options, CancellationToken cancellationToken = default)
    {
        var (target, stream) = await ReferenceFetchable.FetchAsync(reference, options, cancellationToken).ConfigureAwait(false);
        if (await Proxy.Cache.ExistsAsync(target, cancellationToken).ConfigureAwait(false))
        {
            return (target, stream);
        }
        return (target, await Proxy.CacheContentAsync(target, stream, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Fetches the content identified by the descriptor from the cache.
    /// </summary>
    /// <param name="desc">The descriptor identifying the content to fetch</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A stream containing the content</returns>
    public async Task<Stream> FetchAsync(Descriptor desc, CancellationToken cancellationToken = default)
    {
        return await Proxy.FetchAsync(desc, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the content identified by the descriptor exists in either the cache or the source.
    /// </summary>
    /// <param name="desc">The descriptor identifying the content to check for existence.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the content exists in the cache or source; otherwise, <c>false</c>.</returns>
    public async Task<bool> ExistsAsync(Descriptor desc, CancellationToken cancellationToken = default)
    {
        return await Proxy.ExistsAsync(desc, cancellationToken).ConfigureAwait(false);
    }
}
