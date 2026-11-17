// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/FetchableExtensions.cs
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
using OrasProject.Oras.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Index = OrasProject.Oras.Oci.Index;
namespace OrasProject.Oras.Content;


/// <summary>
/// FetchableExtensions provides extension helpers for fetching successors of a node.
/// </summary>
public static class FetchableExtensions
{
    /// <summary>
    /// GetSuccessorsAsync retrieves the successors of a node
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<Descriptor>> GetSuccessorsAsync(this IFetchable fetcher, Descriptor node, CancellationToken cancellationToken = default)
    {
        switch (node.MediaType)
        {
            case Docker.MediaType.Manifest:
            case MediaType.ImageManifest:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    var manifest = OciJsonSerializer.Deserialize<Manifest>(content) ??
                                        throw new JsonException("Failed to deserialize manifest");

                    var descriptors = new List<Descriptor>();
                    if (manifest.Subject != null)
                    {
                        // Note: Subject field only works for Oci Image Manifest
                        descriptors.Add(manifest.Subject);
                    }
                    descriptors.Add(manifest.Config);
                    // A non-conformant registry may send "layers": null instead of an empty
                    // array (a valid, spec-permitted state); treat null as empty. Config is
                    // guaranteed valid by Manifest deserialization validation.
                    descriptors.AddRange(manifest.Layers.NullToEmpty());
                    return descriptors;
                }
            case Docker.MediaType.ManifestList:
            case MediaType.ImageIndex:
                {
                    var content = await fetcher.FetchAllAsync(node, cancellationToken).ConfigureAwait(false);
                    var index = OciJsonSerializer.Deserialize<Index>(content) ??
                                        throw new JsonException("Failed to deserialize index manifest");
                    var descriptors = new List<Descriptor>();
                    if (index.Subject != null)
                    {
                        // Note: Subject field only works for Oci Index Manifest
                        descriptors.Add(index.Subject);
                    }
                    descriptors.AddRange(index.Manifests.NullToEmpty());
                    return descriptors;
                }
        }
        return Array.Empty<Descriptor>();
    }

    /// <summary>
    /// Fetches all the content for a given descriptor.
    /// Currently only sha256 is supported but we would supports others hash algorithms in the future.
    /// </summary>
    /// <param name="fetcher"></param>
    /// <param name="desc"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> FetchAllAsync(this IFetchable fetcher, Descriptor desc, CancellationToken cancellationToken = default)
    {
        using var stream = await fetcher.FetchAsync(desc, cancellationToken).ConfigureAwait(false);
        return await stream.ReadAllAsync(desc, cancellationToken).ConfigureAwait(false);
    }
}
