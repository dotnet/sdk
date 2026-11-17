// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/CopyGraphOptions.cs
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
using System.Collections.Generic;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras;

/// <summary>
/// CopyGraphOptions allows users to customize copy operation's options
/// </summary>
public class CopyGraphOptions
{
    private const int _defaultConcurrency = 3;

    private int _concurrency = _defaultConcurrency;
    // Defaults to the OCI manifest size limit since manifests are
    // the primary metadata cached during copy operations.
    private long _maxMetadataBytes = OciLimits.MaxManifestBytes;

    /// <summary>
    /// Concurrency limits the maximum number of concurrent copy tasks.
    /// Must be greater than 0. Default: 3.
    /// </summary>
    public int Concurrency
    {
        get => _concurrency;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Concurrency must be greater than 0.");
            }
            _concurrency = value;
        }
    }

    /// <summary>
    /// MaxMetadataBytes limits the maximum size of the metadata that can be
    /// cached in the memory. Must be greater than 0. Default: 4 MiB.
    /// </summary>
    public long MaxMetadataBytes
    {
        get => _maxMetadataBytes;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxMetadataBytes must be greater than 0.");
            }
            _maxMetadataBytes = value;
        }
    }

    /// <summary>
    /// PreCopyAsync handles the current descriptor before it is copied. PreCopy can
    /// return CopyNodeDecision.SkipNode to signal that desc should be skipped when it already
    /// exists in the target.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task<CopyNodeDecision>>? PreCopyAsync { get; set; }

    /// <summary>
    /// PostCopyAsync handles the current descriptor after it is copied.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? PostCopyAsync { get; set; }

    /// <summary>
    /// OnCopySkippedAsync will be called when the sub-DAG rooted by the current node
    /// is skipped.
    /// </summary>
    public Func<Descriptor, CancellationToken, Task>? OnCopySkippedAsync { get; set; }

    /// <summary>
    /// FindSuccessorsAsync finds the successors of the current node.
    /// <see cref="IFetchable"/> provides cached access to the source storage, and is suitable
    /// for fetching non-leaf nodes like manifests. Since anything fetched from
    /// the IFetchable instance will be cached in the memory, it is recommended to use original
    /// source storage to fetch large blobs.
    /// If FindSuccessorsAsync is not set, FetchableExtensions.GetSuccessorsAsync will be used.
    /// </summary>
    public Func<IFetchable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>> FindSuccessorsAsync { get; set; }
        = FetchableExtensions.GetSuccessorsAsync;
}
