// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/CopyOptions.cs
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

namespace OrasProject.Oras;

/// <summary>
/// CopyOptions contains parameters for oras.Copy.
/// </summary>
public class CopyOptions : CopyGraphOptions
{
    /// <summary>
    /// MapRoot maps the resolved root node to a desired root node for copy.
    /// When MapRoot is provided, the descriptor resolved from the source
    /// reference will be passed to MapRoot, and the mapped descriptor will be
    /// used as the root node for copy.
    /// </summary>
    public Func<IReadOnlyStorage, Descriptor, CancellationToken, Task<Descriptor>>? MapRoot { get; set; }
}
