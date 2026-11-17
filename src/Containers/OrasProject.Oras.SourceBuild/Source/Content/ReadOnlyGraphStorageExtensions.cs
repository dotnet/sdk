// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/ReadOnlyGraphStorageExtensions.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Content;

/// <summary>
/// ReadOnlyGraphStorageExtensions provides extension helpers for read-only graph storage.
/// </summary>
public static class ReadOnlyGraphStorageExtensions
{
    /// <summary>
    /// ExtendedCopyGraphAsync copies the directed acyclic graph (DAG) that is reachable
    /// from the given node from the source GraphStorage to the destination Storage.
    /// In other words, it copies an artifact along with its referrers or other
    /// predecessor manifests referencing it.
    /// The node (e.g. a manifest of the artifact) is identified by a descriptor.
    /// </summary>
    /// <param name="src">The source graph storage</param>
    /// <param name="dst">The destination storage</param>
    /// <param name="node">The descriptor identifying the node to copy</param>
    /// <param name="opts">Options for the extended copy operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when src or dst is null</exception>
    public static async Task ExtendedCopyGraphAsync(
        this IReadOnlyGraphStorage src,
        IStorage dst,
        Descriptor node,
        ExtendedCopyGraphOptions opts,
        CancellationToken cancellationToken = default)
    {
        if (src == null)
        {
            throw new ArgumentNullException(nameof(src), "Source storage cannot be null");
        }
        if (dst == null)
        {
            throw new ArgumentNullException(nameof(dst), "Destination storage cannot be null");
        }

        var roots = await src.FindRootsAsync(node, opts, cancellationToken).ConfigureAwait(false);

        var proxy = new Proxy
        {
            Cache = new LimitedStorage(new MemoryStorage(), opts.MaxMetadataBytes),
            Source = src
        };

        // Use semaphore for concurrency control
        using var limiter = new SemaphoreSlim(opts.Concurrency, opts.Concurrency);

        // Track nodes being copied to avoid duplicate work across parallel root copies
        var copied = new ConcurrentDictionary<BasicDescriptor, Lazy<Task>>();

        // Copy the sub-DAGs rooted by the root nodes
        var copyTasks = new List<Task>();
        foreach (var root in roots)
        {
            copyTasks.Add(Task.Run(
                () => src.CopyGraphAsync(dst, root, proxy, opts, limiter, copied, cancellationToken),
                cancellationToken));
        }

        await Task.WhenAll(copyTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents a node in the graph traversal with its depth information.
    /// Used during the depth-first search to find root nodes.
    /// </summary>
    internal record NodeInfo(Descriptor Node, int Depth);

    /// <summary>
    /// Finds the root nodes reachable from the given node through a depth-first search.
    /// </summary>
    /// <param name="src">The source graph storage.</param>
    /// <param name="node">The descriptor identifying the starting node.</param>
    /// <param name="opts">Options for the extended copy operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of descriptors representing the root nodes.</returns>
    internal static async Task<List<Descriptor>> FindRootsAsync(
        this IReadOnlyGraphStorage src,
        Descriptor node,
        ExtendedCopyGraphOptions opts,
        CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<BasicDescriptor>();
        var roots = new List<Descriptor>();

        var stack = new Stack<NodeInfo>();

        // push the initial node to the stack, set the depth to 0
        stack.Push(new NodeInfo(node, 0));
        while (stack.TryPop(out var current))
        {
            var currentNode = current.Node;
            var currentKey = currentNode.BasicDescriptor;
            if (visited.Contains(currentKey))
            {
                // skip the current node if it has been visited
                continue;
            }
            visited.Add(currentKey);

            // stop finding predecessors if the target depth is reached
            if (opts.Depth > 0 && current.Depth == opts.Depth)
            {
                roots.Add(currentNode);
                continue;
            }

            var findPredecessors = opts.FindPredecessors
                ?? ((s, desc, ct) => s.GetPredecessorsAsync(desc, ct));
            var predecessors = await findPredecessors(src, currentNode, cancellationToken).ConfigureAwait(false);
            var predecessorList = predecessors.ToList();
            if (predecessorList.Count == 0)
            {
                roots.Add(currentNode);
                continue;
            }
            foreach (var predecessor in predecessorList)
            {
                var predecessorKey = predecessor.BasicDescriptor;
                if (!visited.Contains(predecessorKey))
                {
                    stack.Push(new NodeInfo(predecessor, current.Depth + 1));
                }
            }
        }
        return roots;
    }
}
