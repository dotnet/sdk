// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/ExtendedCopyGraphOptions.cs
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
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Content;

/// <summary>
/// ExtendedCopyGraphOptions contains parameters for ExtendedCopyGraph.
/// </summary>
public class ExtendedCopyGraphOptions : CopyGraphOptions
{
    private int _depth;

    /// <summary>
    /// Depth limits the maximum depth of the directed acyclic graph (DAG) that
    /// will be extended-copied.
    /// If Depth is not specified, or the specified value is less than or
    /// equal to 0, the depth limit will be considered as infinity.
    /// </summary>
    public int Depth
    {
        get => _depth;
        set => _depth = Math.Max(0, value);
    }

    /// <summary>
    /// FindPredecessors finds the predecessors of the current node.
    /// Defaults to calling <see cref="IPredecessorFindable.GetPredecessorsAsync"/>.
    /// </summary>
    public Func<IPredecessorFindable, Descriptor, CancellationToken, Task<IEnumerable<Descriptor>>>? FindPredecessors
    {
        get;
        set;
    }
}
