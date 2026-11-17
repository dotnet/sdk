// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IReferencePushable.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Provides advanced push with the tag service.
/// </summary>
public interface IReferencePushable
{
    /// <summary>
    /// PushReferenceAsync pushes the manifest with a reference tag.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="content"></param>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PushAsync(Descriptor descriptor, Stream content, string reference, CancellationToken cancellationToken = default);
}
