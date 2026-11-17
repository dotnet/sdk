// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IMounter.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Mounter allows cross-repository blob mounts.
/// </summary>
public interface IMounter
{
    /// <summary>
    /// Mount makes the blob with the given descriptor in fromRepo
    /// available in the repository signified by the receiver.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="fromRepository">The source repository name within the destination registry.</param>
    /// <param name="getContent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task MountAsync(Descriptor descriptor,
        string fromRepository,
        Func<CancellationToken, Task<Stream>>? getContent = null,
        CancellationToken cancellationToken = default);
}
