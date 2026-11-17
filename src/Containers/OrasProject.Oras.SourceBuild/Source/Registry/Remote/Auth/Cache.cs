// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/Cache.cs
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

using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Cache is the default in-memory implementation of <see cref="ICache"/>.
/// </summary>
public sealed class Cache : ICache
{
    #region private members
    /// <summary>
    /// CacheEntry represents a cache entry for storing authentication tokens associated with a
    /// specific challenge scheme.
    /// </summary>
    /// <param name="Scheme">The authentication scheme associated with the cache entry.</param>
    /// <param name="Tokens">
    /// A dictionary containing authentication tokens, where the key is the scopes and the value
    /// is the token itself.
    /// </param>
    private sealed record CacheEntry(ChallengeScheme Scheme, Dictionary<string, string> Tokens);

    /// <summary>
    /// The underlying memory cache used to store authentication schemes and tokens.
    /// </summary>
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Dictionary to store per-key locks to ensure thread-safety while allowing
    /// concurrent operations on different cache keys.
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _locks = new();

    /// <summary>
    /// Prefix for cache keys to prevent collisions with other users of the same memory cache.
    /// </summary>
    private const string _cacheKeyPrefix = "ORAS_AUTH_";

    /// <summary>
    /// Default cache entry options with size=1.
    /// These options are used when user-provided options are null.
    /// </summary>
    private static readonly MemoryCacheEntryOptions _defaultCacheEntryOptions = new()
    {
        Size = 1 // always set size to ensure size limits work properly
    };

    /// <summary>
    /// Generates a consistent cache key from the registry and optional PartitionId.
    /// Uses pipe (|) as delimiter since it cannot appear in registry hostnames.
    /// </summary>
    /// <param name="registry">The registry host.</param>
    /// <param name="partitionId">Optional cache partition identifier.</param>
    /// <returns>A prefixed cache key.</returns>
    private static string GetCacheKey(string registry, string? partitionId) =>
        string.IsNullOrEmpty(partitionId)
            ? $"{_cacheKeyPrefix}{registry}"
            : $"{_cacheKeyPrefix}{partitionId}|{registry}";
    #endregion

    /// <summary>
    /// Creates a new Cache instance with the specified memory cache.
    /// </summary>
    /// <param name="memoryCache">The underlying memory cache to use for storage.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="memoryCache"/> is null.
    /// </exception>
    public Cache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Cache entry options used in SetCache for configuring token caching behavior.
    /// If not set, default options with size=1 are used.
    /// </summary>
    /// <remarks>
    /// Note: If the underlying memory cache has a size limit configured, you should
    /// always set the <see cref="MemoryCacheEntryOptions.Size"/> property on your custom
    /// options to ensure proper cache eviction behavior.
    /// </remarks>
    public MemoryCacheEntryOptions? CacheEntryOptions { get; set; }

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
    public bool TryGetScheme(string registry, out ChallengeScheme scheme, string? partitionId = null)
    {
        var cacheKey = GetCacheKey(registry, partitionId);
        if (_memoryCache.TryGetValue(cacheKey, out CacheEntry? cacheEntry) && cacheEntry != null)
        {
            scheme = cacheEntry.Scheme;
            return true;
        }

        scheme = ChallengeScheme.Unknown;
        return false;
    }

    /// <summary>
    /// Sets or updates the cache for a specific registry and authentication scheme.
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
    /// <remarks>
    /// <para>
    /// If the registry already exists in the cache:
    /// <list type="bullet">
    /// <item>
    /// If the provided scheme differs from the existing scheme, the cache entry is replaced with
    /// a new one.
    /// </item>
    /// <item> Otherwise, the token is added or updated in the existing cache entry.</item>
    /// </list>
    /// </para>
    /// <para>
    /// This method uses the <see cref="CacheEntryOptions"/> property if set, or falls back to
    /// the default options with size=1. Using these options ensures proper cache eviction behavior
    /// when size limits are configured.
    /// </para>
    /// </remarks>
    public void SetCache(
        string registry,
        ChallengeScheme scheme,
        string scopeKey,
        string token,
        string? partitionId = null)
    {
        var cacheKey = GetCacheKey(registry, partitionId);
        var lockObj = _locks.GetOrAdd(cacheKey, _ => new object());
        // Lock for atomicity
        lock (lockObj)
        {
            if (_memoryCache.TryGetValue(cacheKey, out CacheEntry? oldEntry) &&
                oldEntry != null &&
                scheme == oldEntry.Scheme)
            {
                // When the scheme matches, update the token in the existing entry
                oldEntry.Tokens[scopeKey] = token;
                return;
            }

            // Otherwise, set a new entry
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [scopeKey] = token
            };
            var newEntry = new CacheEntry(scheme, tokens);

            var entryOptions = CacheEntryOptions ?? _defaultCacheEntryOptions;
            _memoryCache.Set(cacheKey, newEntry, entryOptions);
        }
    }

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
    public bool TryGetToken(
        string registry,
        ChallengeScheme scheme,
        string scopeKey,
        out string token,
        string? partitionId = null)
    {
        var cacheKey = GetCacheKey(registry, partitionId);
        if (_memoryCache.TryGetValue(cacheKey, out CacheEntry? cacheEntry) &&
            cacheEntry != null &&
            cacheEntry.Scheme == scheme &&
            cacheEntry.Tokens.TryGetValue(scopeKey, out var cachedToken))
        {
            token = cachedToken;
            return true;
        }

        token = string.Empty;
        return false;
    }
}
