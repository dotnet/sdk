// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Proxy.cs
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
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

/// <summary>
/// Proxy class is to cache the manifest for OCI image/index manifest to improve performance
/// </summary>
internal class Proxy : IReadOnlyStorage
{
    public required IStorage Cache { get; init; }
    public required IReadOnlyStorage Source { get; init; }

    public bool StopCaching { get; set; } = false;

    /// <summary>
    /// FetchAsync is to fetch the content for the given target desc,
    /// if it is a cache hit, then return cached content
    /// otherwise, fetch the content, cache it if it is a manifest type, and return the content
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> FetchAsync(Descriptor node, CancellationToken cancellationToken = default)
    {
        if (await Cache.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
        {
            return await Cache.FetchAsync(node, cancellationToken).ConfigureAwait(false);
        }

        var dataStream = await Source.FetchAsync(node, cancellationToken).ConfigureAwait(false);
        if (StopCaching)
        {
            return dataStream;
        }
        return await CacheContentAsync(node, dataStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// CacheContent caches the content if it is a manifest type
    /// </summary>
    /// <param name="node"></param>
    /// <param name="contentStream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<Stream> CacheContentAsync(Descriptor node, Stream contentStream, CancellationToken cancellationToken = default)
    {
        if (!Descriptor.IsManifestType(node))
        {
            return contentStream;
        }

        try
        {
            // Caching index/image manifest is to reduce the number of requests
            // to retrieve image/index manifest by GetSuccessorsAsync and PushAsync in CopyGraphAsync
            await Cache.PushAsync(node, contentStream, cancellationToken).ConfigureAwait(false);
        }
        catch (AlreadyExistsException) { }
        finally
        {
            await contentStream.DisposeAsync().ConfigureAwait(false);
        }

        return await Cache.FetchAsync(node, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ExistsAsync checks if the content for the given descriptor exists in either the cache or the source.
    /// This method first checks the cache for better performance, and if not found, checks the source.
    /// </summary>
    /// <param name="node">The descriptor identifying the content to check for existence</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor node, CancellationToken cancellationToken = default)
    {
        if (await Cache.ExistsAsync(node, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        return await Source.ExistsAsync(node, cancellationToken).ConfigureAwait(false);
    }
}
