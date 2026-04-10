// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public partial class GenerateModuleWorkerAssetsManifest : Task
{
    private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly string[] DefaultCompressionFormats = ["br", "gzip"];

    [Required]
    public ITaskItem[] Assets { get; set; }

    public string Version { get; set; }

    [Required]
    public string OutputPath { get; set; }

    public bool MapCompressedAssets { get; set; }

    public string PreferredCompressionFormats { get; set; }

    [Output]
    public string CalculatedVersion { get; set; }

    public override bool Execute()
    {
        CalculatedVersion = GenerateAssetManifest();
        return !Log.HasLoggedErrors;
    }

    private string GenerateAssetManifest()
    {
        var assets = Assets.Select(a => new AssetRoute
        {
            Asset = StaticWebAsset.FromTaskItem(a),
            Url = a.GetMetadata("AssetUrl"),
        }).ToArray();

        var entries = CreateManifestEntries(assets);
        Array.Sort(entries, CompareEntries);

        var version = !string.IsNullOrEmpty(Version) ? Version : ComputeVersion(entries);

        var manifest = new ServiceWorkerManifest
        {
            Version = version,
            Assets = entries,
        };

        PersistManifest(manifest);
        return version;
    }

    private ManifestEntry[] CreateManifestEntries(AssetRoute[] assets)
    {
        var compressedAssetsByRelatedAsset = new Dictionary<string, List<AssetRoute>>(OSPath.PathComparer);
        if (MapCompressedAssets)
        {
            for (var i = 0; i < assets.Length; i++)
            {
                var candidate = assets[i];
                if (!IsCompressedAlternative(candidate.Asset))
                {
                    continue;
                }

                if (!compressedAssetsByRelatedAsset.TryGetValue(candidate.Asset.RelatedAsset, out var alternatives))
                {
                    alternatives = [];
                    compressedAssetsByRelatedAsset.Add(candidate.Asset.RelatedAsset, alternatives);
                }

                alternatives.Add(candidate);
            }
        }

        var entries = new List<ManifestEntry>(assets.Length);
        for (var i = 0; i < assets.Length; i++)
        {
            var candidate = assets[i];
            if (string.Equals(candidate.Asset.AssetRole, StaticWebAsset.AssetRoles.Alternative, StringComparison.Ordinal))
            {
                continue;
            }

            var selectedAsset = SelectResolvedAsset(candidate, compressedAssetsByRelatedAsset);
            entries.Add(CreateManifestEntry(candidate, selectedAsset));
        }

        return [.. entries];
    }

    private AssetRoute SelectResolvedAsset(AssetRoute requestedAsset, Dictionary<string, List<AssetRoute>> compressedAssetsByRelatedAsset)
    {
        if (!MapCompressedAssets || !compressedAssetsByRelatedAsset.TryGetValue(requestedAsset.Asset.Identity, out var alternatives))
        {
            return requestedAsset;
        }

        var preferredFormats = ResolveCompressionFormats();
        for (var i = 0; i < preferredFormats.Length; i++)
        {
            var preferredFormat = preferredFormats[i];
            for (var j = 0; j < alternatives.Count; j++)
            {
                var alternative = alternatives[j];
                if (string.Equals(alternative.Asset.AssetTraitValue, preferredFormat, StringComparison.Ordinal))
                {
                    return alternative;
                }
            }
        }

        return requestedAsset;
    }

    private string[] ResolveCompressionFormats()
    {
        if (string.IsNullOrWhiteSpace(PreferredCompressionFormats))
        {
            return DefaultCompressionFormats;
        }

        var formats = PreferredCompressionFormats.Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(static format =>
            {
                var trimmed = format.Trim();
                return string.Equals(trimmed, "gz", StringComparison.OrdinalIgnoreCase) ? "gzip" : trimmed.ToLowerInvariant();
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return formats.Length > 0 ? formats : DefaultCompressionFormats;
    }

    private static ManifestEntry CreateManifestEntry(AssetRoute requestedAsset, AssetRoute resolvedAsset)
    {
        var contentEncoding = IsCompressedAlternative(resolvedAsset.Asset) ? resolvedAsset.Asset.AssetTraitValue : null;
        return new ManifestEntry
        {
            Hash = $"sha256-{resolvedAsset.Asset.Integrity}",
            Url = requestedAsset.Url,
            ResolvedUrl = string.Equals(requestedAsset.Url, resolvedAsset.Url, StringComparison.Ordinal) ? null : resolvedAsset.Url,
            ContentEncoding = contentEncoding,
        };
    }

    private static int CompareEntries(ManifestEntry left, ManifestEntry right)
    {
        var urlComparison = string.Compare(left.Url, right.Url, StringComparison.Ordinal);
        if (urlComparison != 0)
        {
            return urlComparison;
        }

        var resolvedUrlComparison = string.Compare(left.ResolvedUrl, right.ResolvedUrl, StringComparison.Ordinal);
        if (resolvedUrlComparison != 0)
        {
            return resolvedUrlComparison;
        }

        return string.Compare(left.Hash, right.Hash, StringComparison.Ordinal);
    }

    private static bool IsCompressedAlternative(StaticWebAsset asset) =>
        string.Equals(asset.AssetRole, StaticWebAsset.AssetRoles.Alternative, StringComparison.Ordinal) &&
        string.Equals(asset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal) &&
        !string.IsNullOrEmpty(asset.AssetTraitValue);

    private static string ComputeVersion(ManifestEntry[] assets)
    {
        var combinedHash = string.Join(
            Environment.NewLine,
            assets.OrderBy(f => f.Url, StringComparer.Ordinal).Select(f => f.Hash));

        var data = Encoding.UTF8.GetBytes(combinedHash);
#if !NET9_0_OR_GREATER
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(data);
        var version = Convert.ToBase64String(bytes).Substring(0, 8);
#else
        var bytes = SHA256.HashData(data);
        var version = Convert.ToBase64String(bytes)[..8];
#endif

        return version;
    }

    private void PersistManifest(ServiceWorkerManifest manifest)
    {
        var content = BuildModuleSource(manifest);
        var contentHash = ComputeFileHash(content);
        var fileExists = File.Exists(OutputPath);
        var existingManifestHash = fileExists ? ComputeFileHash(File.ReadAllText(OutputPath)) : "";

        if (!fileExists)
        {
            Log.LogMessage(MessageImportance.Low, $"Creating manifest with content hash '{contentHash}' because manifest file '{OutputPath}' does not exist.");
            File.WriteAllText(OutputPath, content);
        }
        else if (!string.Equals(contentHash, existingManifestHash, StringComparison.Ordinal))
        {
            Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest hash '{contentHash}' is different from existing manifest hash '{existingManifestHash}'.");
            File.WriteAllText(OutputPath, content);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest hash '{contentHash}' has not changed.");
        }
    }

    private static string BuildModuleSource(ServiceWorkerManifest manifest)
    {
        var manifestJson = JsonSerializer.Serialize(manifest, ManifestSerializationOptions);
        return $$"""
export const assetsManifest = {{manifestJson}};

const contentCacheName = 'blazor-service-worker-assets';
const metadataCachePrefix = 'blazor-service-worker-metadata-';
const metadataCacheName = `${metadataCachePrefix}${assetsManifest.version}`;
const serviceWorkerScope = typeof self === 'undefined' ? globalThis : self;
const serviceWorkerBaseUrl = new URL('/', serviceWorkerScope.location?.origin ?? 'http://localhost/').href;
const assetsByUrl = new Map(assetsManifest.assets.map(asset => [new URL(asset.url, serviceWorkerBaseUrl).href, asset]));

function createAssetCacheKey(asset) {
  return new Request(`https://service-worker-assets.invalid/${encodeURIComponent(asset.hash)}`);
}

function createAssetRequest(asset) {
  return new Request(new URL(asset.resolvedUrl ?? asset.url, serviceWorkerBaseUrl).href, {
    cache: 'no-cache',
    integrity: asset.hash,
  });
}

async function ensureCached(cache, asset) {
  const cacheKey = createAssetCacheKey(asset);
  const cachedResponse = await cache.match(cacheKey);
  if (cachedResponse) {
    return cachedResponse;
  }

  const response = await fetch(createAssetRequest(asset));
  if (!response.ok) {
    throw new Error(`Failed to fetch '${asset.resolvedUrl ?? asset.url}' while populating the service worker cache.`);
  }

  await cache.put(cacheKey, response.clone());
  return response;
}

function decodeResponse(response, encoding) {
  const headers = new Headers(response.headers);
  headers.delete('Content-Encoding');
  headers.delete('Content-Length');

  return new Response(response.body.pipeThrough(new DecompressionStream(encoding)), {
    headers,
    status: response.status,
    statusText: response.statusText,
  });
}

function finalizeResponse(response, asset) {
  if (!asset.contentEncoding) {
    return response;
  }

  return decodeResponse(response, asset.contentEncoding);
}

function match(request) {
  const method = typeof request === 'string' ? 'GET' : (request?.method ?? 'GET');
  if (method !== 'GET') {
    return null;
  }

  const requestUrl = typeof request === 'string'
    ? new URL(request, serviceWorkerBaseUrl).href
    : request.url;

  return assetsByUrl.get(requestUrl) ?? null;
}

function precacheEntries() {
  return assetsManifest.assets.map(asset => asset.url);
}

async function install() {
  const cache = await caches.open(contentCacheName);
  await Promise.all(assetsManifest.assets.map(asset => ensureCached(cache, asset)));
  await caches.open(metadataCacheName);
}

async function activate() {
  const cache = await caches.open(contentCacheName);
  const expectedKeys = new Set(assetsManifest.assets.map(asset => createAssetCacheKey(asset).url));
  const cachedRequests = await cache.keys();

  await Promise.all(cachedRequests
    .filter(request => !expectedKeys.has(request.url))
    .map(request => cache.delete(request)));

  const cacheKeys = await caches.keys();
  await Promise.all(cacheKeys
    .filter(key => key.startsWith(metadataCachePrefix) && key !== metadataCacheName)
    .map(key => caches.delete(key)));
}

async function handleFetch(request) {
  const asset = match(request);
  if (!asset) {
    return fetch(request);
  }

  const cache = await caches.open(contentCacheName);
  let response = await cache.match(createAssetCacheKey(asset));
  if (!response) {
    response = await ensureCached(cache, asset);
  }

  return finalizeResponse(response.clone(), asset);
}

export const router = {
  version: assetsManifest.version,
  assets: assetsManifest.assets,
  match,
  precacheEntries,
  install,
  activate,
  handleFetch,
};
""";
    }

    private static string ComputeFileHash(string contents)
    {
        var data = Encoding.UTF8.GetBytes(contents);
#if !NET9_0_OR_GREATER
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(data);
#else
        var bytes = SHA256.HashData(data);
#endif
        return Convert.ToBase64String(bytes);
    }

    private sealed class ServiceWorkerManifest
    {
        public string Version { get; set; }
        public ManifestEntry[] Assets { get; set; }
    }

    private sealed class ManifestEntry
    {
        public string Hash { get; set; }
        public string Url { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ResolvedUrl { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ContentEncoding { get; set; }
    }

    private sealed class AssetRoute
    {
        public StaticWebAsset Asset { get; set; }
        public string Url { get; set; }
    }
}
