// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/ICache.cs
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

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// ICache caches authentication schemes and tokens for registry hosts.
/// </summary>
public interface ICache
{
    /// <summary>
    /// TryGetScheme attempts to retrieve the authentication scheme associated with the specified
    /// registry host.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="scheme">
    /// When this method returns, contains the <see cref="ChallengeScheme"/> associated with the
    /// registry if found in the cache; otherwise, <see cref="ChallengeScheme.Unknown"/>.
    /// </param>
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same registry.
    /// </param>
    /// <returns>
    /// <c>true</c> if the authentication scheme was found in the cache; otherwise, <c>false</c>.
    /// </returns>
    bool TryGetScheme(string registry, out ChallengeScheme scheme, string? partitionId = null);

    /// <summary>
    /// SetCache sets or updates the cache for a specific registry and authentication scheme.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="scheme">The authentication scheme associated with the cache entry.</param>
    /// <param name="scopeKey">
    /// The OAuth2 scope key used to identify the token within the cache entry.
    /// </param>
    /// <param name="token">The token to be stored in the cache.</param>
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same registry.
    /// </param>
    void SetCache(
        string registry,
        ChallengeScheme scheme,
        string scopeKey,
        string token,
        string? partitionId = null);

    /// <summary>
    /// TryGetToken attempts to retrieve a token from the cache for the specified registry,
    /// scheme, and scope key.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="scheme">The authentication scheme associated with the token.</param>
    /// <param name="scopeKey">
    /// The OAuth2 scope key used to identify the token within the cache.
    /// </param>
    /// <param name="token">
    /// When this method returns, contains the token associated with the specified registry,
    /// scheme, and scope key, if found; otherwise, an empty string.
    /// </param>
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same registry.
    /// </param>
    /// <returns>
    /// <c>true</c> if a token matching the specified registry, scheme, and scope key is found;
    /// otherwise, <c>false</c>.
    /// </returns>
    bool TryGetToken(
        string registry,
        ChallengeScheme scheme,
        string scopeKey,
        out string token,
        string? partitionId = null);
}
