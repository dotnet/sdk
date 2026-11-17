// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/MemoryStore.cs
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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

/// <summary>
/// MemoryStore is an in-memory implementation of a content-addressable target store.
/// </summary>
public class MemoryStore : ITarget, IReadOnlyGraphStorage
{
    private readonly MemoryStorage _storage = new();
    private readonly MemoryTagStore _tagResolver = new();
    private readonly MemoryGraph _graph = new();

    /// <summary>
    /// ExistsAsync returns checks if the described content exists.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await _storage.ExistsAsync(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// FetchAsync fetches the content identified by the descriptor.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
        => await _storage.FetchAsync(target, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// PushAsync pushes the content, matching the expected descriptor.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="contentStream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
    {
        await _storage.PushAsync(expected, contentStream, cancellationToken).ConfigureAwait(false);
        await _graph.IndexAsync(_storage, expected, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ResolveAsync resolves a reference to a descriptor.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        => await _tagResolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// TagAsync tags a descriptor with a reference string.
    /// It throws NotFoundException if the tagged content does not exist.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public async Task TagAsync(Descriptor descriptor, string reference, CancellationToken cancellationToken = default)
    {
        if (!await _storage.ExistsAsync(descriptor, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException($"{descriptor.Digest}: {descriptor.MediaType}");
        }
        await _tagResolver.TagAsync(descriptor, reference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PredecessorsAsync returns the nodes directly pointing to the current node.
    /// Predecessors returns null without error if the node does not exists in the
    /// store.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Descriptor>> GetPredecessorsAsync(Descriptor node, CancellationToken cancellationToken = default)
        => await _graph.GetPredecessorsAsync(node, cancellationToken).ConfigureAwait(false);
}
