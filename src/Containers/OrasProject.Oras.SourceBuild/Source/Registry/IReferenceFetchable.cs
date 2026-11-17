// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IReferenceFetchable.cs
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

using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry;

/// <summary>
/// Provides advanced fetch with the tag service.
/// </summary>
public interface IReferenceFetchable
{
    /// <summary>
    /// Fetches the content identified by the reference.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the content identified by the reference with additional options.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="options">
    /// Options for the fetch operation. The default interface implementation ignores this parameter
    /// and forwards to <see cref="FetchAsync(string, CancellationToken)"/>; concrete implementations
    /// may override this overload to honor the provided options (for example, to apply custom headers).
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <remarks>
    /// The default implementation of this overload does not apply <paramref name="options"/> and instead
    /// delegates to <see cref="FetchAsync(string, CancellationToken)"/>. Callers should not assume that
    /// options are respected unless the concrete implementation overrides this method.
    /// </remarks>
    Task<(Descriptor Descriptor, Stream Stream)> FetchAsync(string reference, FetchOptions options, CancellationToken cancellationToken = default)
        => FetchAsync(reference, cancellationToken);
}
