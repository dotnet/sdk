// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/IResolvable.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

/// <summary>
/// Resolves reference tags.
/// </summary>
public interface IResolvable
{
    /// <summary>
    /// ResolveAsync resolves the reference to a descriptor.
    /// </summary>
    /// <param name="reference"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Descriptor> ResolveAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the reference to a descriptor with additional options.
    /// </summary>
    /// <param name="reference">The reference (for example, tag or digest) to resolve.</param>
    /// <param name="options">
    /// Options that may influence the resolve operation (for example, additional request headers or
    /// other implementation-specific behavior).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolved <see cref="Descriptor"/>.</returns>
    /// <remarks>
    /// The default interface implementation ignores <paramref name="options"/> and forwards the call to
    /// <see cref="ResolveAsync(string, CancellationToken)"/>. As a result, the effect of <paramref name="options"/>
    /// (including any headers it may carry) is best-effort and depends on the concrete implementation.
    /// Implementations that support resolve options should override this overload and honor the provided
    /// <paramref name="options"/>.
    /// </remarks>
    Task<Descriptor> ResolveAsync(string reference, ResolveOptions options, CancellationToken cancellationToken = default)
        => ResolveAsync(reference, cancellationToken);
}
