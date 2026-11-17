// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/ReadOnlyTargetExtensions.cs
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
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;

namespace OrasProject.Oras;

/// <summary>
/// ReadOnlyTargetExtensions provides extension helpers for copying from read-only targets.
/// </summary>
public static class ReadOnlyTargetExtensions
{
    /// <summary>
    /// Copy copies a rooted directed acyclic graph (DAG) with the tagged root node
    /// in the source Target to the destination Target.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the root node on successful copy.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="srcRef"></param>
    /// <param name="dst"></param>
    /// <param name="dstRef"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this IReadOnlyTarget src, string srcRef, ITarget dst, string dstRef, CancellationToken cancellationToken = default)
    {
        return await src.CopyAsync(srcRef, dst, dstRef, new CopyOptions(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copy copies a rooted directed acyclic graph (DAG) with the tagged root node
    /// in the source Target to the destination Target.
    /// The destination reference will be the same as the source reference if the
    /// destination reference is left blank.
    /// Returns the descriptor of the root node on successful copy.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="srcRef"></param>
    /// <param name="dst"></param>
    /// <param name="dstRef"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="copyOptions"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<Descriptor> CopyAsync(this IReadOnlyTarget src, string srcRef, ITarget dst, string dstRef, CopyOptions copyOptions, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(srcRef))
        {
            throw new ArgumentNullException(nameof(srcRef));
        }

        if (string.IsNullOrEmpty(dstRef))
        {
            dstRef = srcRef;
        }

        var proxy = new Proxy()
        {
            Cache = new LimitedStorage(new MemoryStorage(), copyOptions.MaxMetadataBytes),
            Source = src
        };

        var root = await ResolveRootAsync(src, srcRef, proxy, cancellationToken).ConfigureAwait(false);
        if (copyOptions.MapRoot != null)
        {
            proxy.StopCaching = true;
            try
            {
                root = await copyOptions.MapRoot(proxy, root, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                proxy.StopCaching = false;
            }
        }
        await src.CopyGraphAsync(dst, root, proxy, copyOptions, cancellationToken).ConfigureAwait(false);
        await dst.TagAsync(root, dstRef, cancellationToken).ConfigureAwait(false);
        return root;
    }

    /// <summary>
    /// ResolveRoot resolves the source reference to the root node.
    /// </summary>
    /// <param name="src">The source target</param>
    /// <param name="srcRef">The source reference</param>
    /// <param name="proxy">The CAS proxy for caching</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>The descriptor of the root node</returns>
    private static async Task<Descriptor> ResolveRootAsync(IReadOnlyTarget src, string srcRef, Proxy proxy, CancellationToken cancellationToken)
    {
        // Check if src implements IReferenceFetchable
        if (src is IReferenceFetchable refFetcher)
        {
            // Optimize performance for IReferenceFetchable targets
            var refProxy = new ReferenceProxy(refFetcher, proxy);
            var (root, stream) = await refProxy.FetchAsync(srcRef, cancellationToken).ConfigureAwait(false);
            await using var _ = stream.ConfigureAwait(false);
            return root;
        }
        // Fall back to Resolve if not an IReferenceFetchable
        return await src.ResolveAsync(srcRef, cancellationToken).ConfigureAwait(false);
    }
}
