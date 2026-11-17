// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/IAccessTokenProvider.cs
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Provides pre-resolved access tokens for registry authentication challenges.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ICredentialProvider"/>, which returns raw credentials that
/// the <see cref="Client"/> exchanges at a token endpoint, an
/// <see cref="IAccessTokenProvider"/> returns a ready-to-use access token.
/// This is useful when token acquisition is handled by an external service
/// (e.g., a gRPC token service) that owns the credential exchange.
/// </para>
/// <para>
/// When both <see cref="IAccessTokenProvider"/> and
/// <see cref="ICredentialProvider"/> are configured on a <see cref="Client"/>,
/// the access-token provider is tried first.  If it returns <c>null</c> or
/// whitespace, the client falls through to credential-based authentication.
/// </para>
/// </remarks>
/// <seealso href="https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md#authorization">
/// OCI Distribution Spec — Authorization
/// </seealso>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Resolves an access token for the given registry authentication
    /// challenge.
    /// </summary>
    /// <param name="hostname">
    /// The registry hostname (authority) that issued the challenge.
    /// </param>
    /// <param name="realm">
    /// The authentication realm URL from the WWW-Authenticate header.
    /// <para>
    /// <b>Trust boundary:</b> This value is derived from a server-supplied
    /// WWW-Authenticate header and must be treated as untrusted input.
    /// Implementations should validate the URL before making outbound
    /// requests (e.g., restrict to HTTPS, allowlist known token
    /// endpoints).
    /// </para>
    /// </param>
    /// <param name="service">
    /// The service identifier from the WWW-Authenticate header.
    /// <para>
    /// <b>Trust boundary:</b> This value is attacker-controlled.
    /// Implementations should not use it to make security decisions
    /// without independent validation.
    /// </para>
    /// </param>
    /// <param name="scopes">
    /// The requested access scopes (read-only).
    /// <para>
    /// <b>Trust boundary:</b> Scope values originate from the
    /// WWW-Authenticate header and should be validated or sanitized
    /// before forwarding to external token services.
    /// </para>
    /// </param>
    /// <param name="forceRefresh">
    /// When <c>true</c>, indicates that a previously returned token was
    /// rejected by the server (e.g., expired or revoked). Implementations
    /// that maintain their own token cache should bypass it and acquire a
    /// fresh token.  When <c>false</c>, a cached token may be returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// The access token string, or <c>null</c> / whitespace if this provider
    /// cannot resolve a token for the given parameters (in which case the
    /// client falls through to credential-based authentication).
    /// </returns>
    Task<string?> ResolveAccessTokenAsync(
        string hostname,
        string realm,
        string service,
        IReadOnlyList<string> scopes,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
