// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/IRealmValidator.cs
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

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Validates whether a token authentication realm URL is safe to send
/// credentials to.
/// </summary>
/// <seealso href="https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#authorization">
/// OCI Distribution Spec — Authorization
/// </seealso>
public interface IRealmValidator
{
    /// <summary>
    /// Determines whether the specified realm URL is allowed for the
    /// given registry.
    /// </summary>
    /// <param name="registryUri">
    /// The URI of the registry that issued the challenge.
    /// </param>
    /// <param name="realmUri">
    /// The realm URI from the WWW-Authenticate challenge.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if credentials may be sent to the realm;
    /// otherwise <c>false</c>.
    /// </returns>
    Task<bool> IsRealmAllowedAsync(
        Uri registryUri,
        Uri realmUri,
        CancellationToken cancellationToken = default);
}
