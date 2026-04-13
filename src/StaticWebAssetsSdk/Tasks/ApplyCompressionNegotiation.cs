// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ApplyCompressionNegotiation : Task
{
    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    public ITaskItem[] CompressionFormats { get; set; }

    // Dictionary candidates from ResolveDictionaryCandidates.
    // Each item's Identity is the extracted dictionary bytes path.
    // Metadata: Hash (structured field ":base64-sha256:"), TargetAsset (new asset identity), MatchPattern (URL pattern for Use-As-Dictionary).
    public ITaskItem[] DictionaryCandidates { get; set; }

    public string AttachWeakETagToCompressedAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    // Per-group state for compression processing.
    private class CompressionGroupState
    {
        // Whether any endpoint in this route group was modified during processing.
        public bool Modified { get; set; }
        // Compressed assets whose endpoints live in this route group, with pre-computed quality.
        public List<(StaticWebAsset Asset, string Quality)> CompressedAssets { get; } = new();
        // Newly created synthetic endpoints to add to this group.
        public List<StaticWebAssetEndpoint> SyntheticEndpoints { get; } = new();
        // When non-null, indicates that this group has dictionary-compressed variants
        // and all non-dcz endpoints should include Use-As-Dictionary with this match pattern.
        public string DictionaryMatchPattern { get; set; }
    }

    public override bool Execute()
    {
        // === Walk 1: Parse endpoints → route groups + endpointsByAsset ===
        var allEndpoints = StaticWebAssetEndpoint.FromItemGroup(CandidateEndpoints);
        var routeGroups = StaticWebAssetEndpointGroup<CompressionGroupState>.CreateEndpointGroups(allEndpoints);

        var endpointsByAsset = new Dictionary<string, List<StaticWebAssetEndpoint>>(CandidateEndpoints.Length / 2, StringComparer.Ordinal);
        foreach (var endpoint in allEndpoints)
        {
            if (!endpointsByAsset.TryGetValue(endpoint.AssetFile, out var eps))
            {
                eps = new List<StaticWebAssetEndpoint>(5);
                endpointsByAsset[endpoint.AssetFile] = eps;
            }
            eps.Add(endpoint);
        }

        // === Walk 2: Parse + sort assets, walk backwards to compute quality rankings ===
        var assets = StaticWebAsset.FromTaskItemGroup(CandidateAssets);
        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        var formatPriority = BuildFormatPriority(CompressionFormats);
        var formatUsesDictionary = BuildFormatUsesDictionary(CompressionFormats);

        var dictionaryHashByAsset = new Dictionary<string, string>(StringComparer.Ordinal);
        var dictionaryMatchPatternByAsset = new Dictionary<string, string>(StringComparer.Ordinal);
        if (DictionaryCandidates != null)
        {
            foreach (var candidate in DictionaryCandidates)
            {
                var targetAsset = candidate.GetMetadata("TargetAsset");
                var hash = candidate.GetMetadata("Hash");
                var matchPattern = candidate.GetMetadata("MatchPattern");
                if (!string.IsNullOrEmpty(targetAsset) && !string.IsNullOrEmpty(hash))
                {
                    dictionaryHashByAsset[targetAsset] = hash;
                }
                if (!string.IsNullOrEmpty(targetAsset) && !string.IsNullOrEmpty(matchPattern))
                {
                    dictionaryMatchPatternByAsset[targetAsset] = matchPattern;
                }
            }
        }

        // Accumulate compressed variants per related asset during backward walk
        var compressedByRelated = new Dictionary<string, List<StaticWebAsset>>(StringComparer.Ordinal);
        // Quality map: compressed asset identity → quality string
        var qualityMap = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = assets.Length - 1; i >= 0; i--)
        {
            var asset = assets[i];

            if (string.Equals(asset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                // Compressed asset: accumulate for quality computation
                if (!compressedByRelated.TryGetValue(asset.RelatedAsset, out var variants))
                {
                    variants = new List<StaticWebAsset>();
                    compressedByRelated[asset.RelatedAsset] = variants;
                }
                variants.Add(asset);
            }
            else
            {
                // Primary asset: if it has compressed variants, sort + assign quality
                if (compressedByRelated.TryGetValue(asset.Identity, out var variants))
                {
                    // Sort: smallest file first, then by format priority for ties
                    variants.Sort((a, b) =>
                    {
                        var cmp = a.FileLength.CompareTo(b.FileLength);
                        if (cmp != 0)
                        {
                            return cmp;
                        }

                        if (!formatPriority.TryGetValue(a.AssetTraitValue, out var pa))
                        {
                            pa = int.MaxValue;
                        }
                        if (!formatPriority.TryGetValue(b.AssetTraitValue, out var pb))
                        {
                            pb = int.MaxValue;
                        }
                        if (pa != pb)
                        {
                            return pa.CompareTo(pb);
                        }

                        return string.Compare(a.AssetTraitValue, b.AssetTraitValue, StringComparison.Ordinal);
                    });

                    // Assign quality rankings
                    for (var rank = 0; rank < variants.Count; rank++)
                    {
                        qualityMap[variants[rank].Identity] = ComputeQualityValue(rank);
                    }
                }
                // else: primary asset with no compressed variants — skip entirely
            }
        }

        // === Walk 3: Process compressed assets — update headers, create synthetics, link groups ===
        var compressionHeadersByEncoding = new Dictionary<string, StaticWebAssetEndpointResponseHeader[]>(2);

        foreach (var kvp in compressedByRelated)
        {
            var relatedAssetIdentity = kvp.Key;
            var variants = kvp.Value;

            // Get all primary endpoints for the related asset
            if (!endpointsByAsset.TryGetValue(relatedAssetIdentity, out var primaryEndpoints))
            {
                continue;
            }

            foreach (var compressed in variants)
            {
                if (!qualityMap.TryGetValue(compressed.Identity, out var quality))
                {
                    continue;
                }

                Log.LogMessage("Processing compressed asset: {0}", compressed.Identity);
                var compressionHeaders = GetOrCreateCompressionHeaders(compressionHeadersByEncoding, compressed);

                // Check if this compressed asset's format uses dictionaries
                var isDictionaryFormat = formatUsesDictionary.TryGetValue(compressed.AssetTraitValue, out var usesDict) && usesDict;
                string dictionaryHash = null;
                string dictionaryMatchPattern = null;
                if (isDictionaryFormat)
                {
                    dictionaryHashByAsset.TryGetValue(relatedAssetIdentity, out dictionaryHash);
                    dictionaryMatchPatternByAsset.TryGetValue(relatedAssetIdentity, out dictionaryMatchPattern);
                }

                if (!endpointsByAsset.TryGetValue(compressed.Identity, out var compressedEndpoints))
                {
                    Log.LogWarning("Endpoints not found for compressed asset: {0} {1}", compressed.RelativePath, compressed.Identity);
                    throw new InvalidOperationException($"Endpoints not found for compressed asset: {compressed.Identity}");
                }

                // Update all compressed endpoints with Content-Encoding + Vary headers
                foreach (var compressedEndpoint in compressedEndpoints)
                {
                    if (HasContentEncodingSelector(compressedEndpoint))
                    {
                        Log.LogMessage(MessageImportance.Low, "  Skipping endpoint '{0}' since it already has a Content-Encoding selector", compressedEndpoint.Route);
                        continue;
                    }

                    if (!HasContentEncodingResponseHeader(compressedEndpoint))
                    {
                        compressedEndpoint.ResponseHeaders = [
                            ..compressedEndpoint.ResponseHeaders,
                            ..compressionHeaders
                        ];
                    }

                    // Mark the compressed endpoint's route group as modified
                    if (routeGroups.TryGetValue(compressedEndpoint.Route, out var compGroup))
                    {
                        compGroup.State ??= new CompressionGroupState();
                        compGroup.State.Modified = true;
                    }

                    Log.LogMessage(MessageImportance.Low, "  Updated endpoint '{0}' with Content-Encoding and Vary headers", compressedEndpoint.Route);
                }

                // Create synthetic endpoints at each primary endpoint's route
                foreach (var primaryEndpoint in primaryEndpoints)
                {
                    // Find the compatible compressed endpoint for this primary endpoint
                    StaticWebAssetEndpoint compatibleCompressed = null;
                    foreach (var compEp in compressedEndpoints)
                    {
                        if (IsCompatible(compEp, primaryEndpoint))
                        {
                            compatibleCompressed = compEp;
                            break;
                        }
                    }
                    if (compatibleCompressed == null)
                    {
                        continue;
                    }

                    var compressedHeaders = GetCompressedHeaders(compatibleCompressed);
                    var endpointCopy = CreateUpdatedEndpoint(compressed, quality, compatibleCompressed, compressedHeaders, primaryEndpoint, isDictionaryFormat, dictionaryHash);

                    // Add synthetic to primary's route group
                    if (routeGroups.TryGetValue(primaryEndpoint.Route, out var primaryGroup))
                    {
                        primaryGroup.State ??= new CompressionGroupState();
                        primaryGroup.State.SyntheticEndpoints.Add(endpointCopy);
                        EnsureVaryHeader(primaryEndpoint);

                        // For dictionary formats, record the match pattern on the group
                        // so Walk 4 can add Use-As-Dictionary to ALL endpoints (not just identity)
                        if (isDictionaryFormat && !string.IsNullOrEmpty(dictionaryHash))
                        {
                            primaryGroup.State.DictionaryMatchPattern = dictionaryMatchPattern;
                        }

                        primaryGroup.State.Modified = true;
                    }
                }
            }
        }

        // === Walk 4: Collect results from modified groups ===
        var result = new HashSet<StaticWebAssetEndpoint>(CandidateEndpoints.Length, StaticWebAssetEndpoint.RouteAndAssetComparer);
        foreach (var group in routeGroups.Values)
        {
            if (group.State is not { Modified: true })
            {
                continue;
            }

            // Per RFC 9842, Use-As-Dictionary must be on ALL content-negotiated responses
            // for the resource (identity, gzip, br, zstd) — not just the identity endpoint.
            // The client decompresses first, then stores the raw body as a dictionary.
            // Only dcz endpoints (which consume the dictionary) should NOT get it.
            var matchPattern = group.State?.DictionaryMatchPattern;

            foreach (var item in group.Items)
            {
                EnsureVaryHeader(item.Endpoint);
                if (matchPattern != null)
                {
                    EnsureVaryAvailableDictionaryHeader(item.Endpoint);
                    EnsureUseDictionaryHeader(item.Endpoint, matchPattern);
                }
                result.Add(item.Endpoint);
            }
            if (group.State != null)
            {
                foreach (var synthetic in group.State.SyntheticEndpoints)
                {
                    if (matchPattern != null && !HasAvailableDictionarySelector(synthetic))
                    {
                        EnsureVaryAvailableDictionaryHeader(synthetic);
                        EnsureUseDictionaryHeader(synthetic, matchPattern);
                    }
                    result.Add(synthetic);
                }
            }
        }

        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(result);

        return true;
    }

    private static void EnsureVaryHeader(StaticWebAssetEndpoint endpoint)
    {
        if (!HasVaryResponseHeaderWithAcceptEncoding(endpoint))
        {
            endpoint.ResponseHeaders = [
                ..endpoint.ResponseHeaders,
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ];
        }
    }

    private static bool HasVaryResponseHeaderWithAcceptEncoding(StaticWebAssetEndpoint endpoint)
    {
        for (var i = 0; i < endpoint.ResponseHeaders.Length; i++)
        {
            var header = endpoint.ResponseHeaders[i];
            if (string.Equals(header.Name, "Vary", StringComparison.OrdinalIgnoreCase) &&
                header.Value.Contains("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> GetCompressedHeaders(StaticWebAssetEndpoint compressedEndpoint)
    {
        var result = new HashSet<string>(compressedEndpoint.ResponseHeaders.Length, StringComparer.Ordinal);
        for (var i = 0; i < compressedEndpoint.ResponseHeaders.Length; i++)
        {
            var responseHeader = compressedEndpoint.ResponseHeaders[i];
            result.Add(responseHeader.Name);
        }

        return result;
    }

    private static StaticWebAssetEndpointResponseHeader[] GetOrCreateCompressionHeaders(Dictionary<string, StaticWebAssetEndpointResponseHeader[]> compressionHeadersByEncoding, StaticWebAsset compressedAsset)
    {
        if (!compressionHeadersByEncoding.TryGetValue(compressedAsset.AssetTraitValue, out var compressionHeaders))
        {
            compressionHeaders = CreateCompressionHeaders(compressedAsset);
            compressionHeadersByEncoding.Add(compressedAsset.AssetTraitValue, compressionHeaders);
        }

        return compressionHeaders;
    }

    private static StaticWebAssetEndpointResponseHeader[] CreateCompressionHeaders(StaticWebAsset compressedAsset) =>
        [
            new()
            {
                Name = "Content-Encoding",
                Value = compressedAsset.AssetTraitValue
            },
            new()
            {
                Name = "Vary",
                Value = "Accept-Encoding"
            }
        ];

    private StaticWebAssetEndpoint CreateUpdatedEndpoint(
        StaticWebAsset compressedAsset,
        string quality,
        StaticWebAssetEndpoint compressedEndpoint,
        HashSet<string> compressedHeaders,
        StaticWebAssetEndpoint relatedEndpointCandidate,
        bool isDictionaryFormat = false,
        string dictionaryHash = null)
    {
        Log.LogMessage(MessageImportance.Low, "Processing related endpoint '{0}'", relatedEndpointCandidate.Route);
        var encodingSelector = new StaticWebAssetEndpointSelector
        {
            Name = "Content-Encoding",
            Value = compressedAsset.AssetTraitValue,
            Quality = quality
        };
        Log.LogMessage(MessageImportance.Low, "  Created Content-Encoding selector for compressed asset '{0}' with size '{1}' is '{2}'", encodingSelector.Value, encodingSelector.Quality, relatedEndpointCandidate.Route);

        // Handle EndpointProperty case for ETag
        var endpointProperties = relatedEndpointCandidate.EndpointProperties.ToList();
        if (string.Equals(AttachWeakETagToCompressedAssets, "EndpointProperty", StringComparison.Ordinal))
        {
            // Find ETag header in the related endpoint candidate
            foreach (var header in relatedEndpointCandidate.ResponseHeaders)
            {
                if (string.Equals(header.Name, "ETag", StringComparison.Ordinal))
                {
                    Log.LogMessage(MessageImportance.Low, "  Adding original-resource endpoint property for related endpoint '{0}'", relatedEndpointCandidate.Route);
                    endpointProperties.Add(new StaticWebAssetEndpointProperty
                    {
                        Name = "original-resource",
                        Value = header.Value
                    });
                    break;
                }
            }
        }

        // Build selectors list
        var selectorsList = new List<StaticWebAssetEndpointSelector>(relatedEndpointCandidate.Selectors.Length + 2);
        selectorsList.AddRange(relatedEndpointCandidate.Selectors);
        selectorsList.Add(encodingSelector);

        // For dictionary formats, add an Available-Dictionary selector so the endpoint
        // only matches when the browser advertises the correct dictionary
        if (isDictionaryFormat && !string.IsNullOrEmpty(dictionaryHash))
        {
            selectorsList.Add(new StaticWebAssetEndpointSelector
            {
                Name = "Available-Dictionary",
                Value = dictionaryHash,
                Quality = "1.0"
            });
        }

        var endpointCopy = new StaticWebAssetEndpoint
        {
            AssetFile = compressedAsset.Identity,
            Route = relatedEndpointCandidate.Route,
            Order = relatedEndpointCandidate.Order,
            Selectors = [.. selectorsList],
            EndpointProperties = [.. endpointProperties]
        };
        var headers = new List<StaticWebAssetEndpointResponseHeader>(7);
        ApplyCompressedEndpointHeaders(headers, compressedEndpoint, relatedEndpointCandidate.Route);
        ApplyRelatedEndpointCandidateHeaders(headers, relatedEndpointCandidate, compressedHeaders);

        // For dictionary formats, add Vary: Available-Dictionary
        if (isDictionaryFormat && !string.IsNullOrEmpty(dictionaryHash))
        {
            headers.Add(new StaticWebAssetEndpointResponseHeader
            {
                Name = "Vary",
                Value = "Available-Dictionary"
            });
        }

        endpointCopy.ResponseHeaders = [.. headers];

        // Update the endpoint
        Log.LogMessage(MessageImportance.Low, "  Updated related endpoint '{0}' with Content-Encoding selector '{1}={2}'", relatedEndpointCandidate.Route, encodingSelector.Value, encodingSelector.Quality);
        return endpointCopy;
    }

    private static bool HasContentEncodingResponseHeader(StaticWebAssetEndpoint compressedEndpoint)
    {
        for (var i = 0; i < compressedEndpoint.ResponseHeaders.Length; i++)
        {
            var responseHeader = compressedEndpoint.ResponseHeaders[i];
            if (string.Equals(responseHeader.Name, "Content-Encoding", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasContentEncodingSelector(StaticWebAssetEndpoint compressedEndpoint)
    {
        for (var i = 0; i < compressedEndpoint.Selectors.Length; i++)
        {
            var selector = compressedEndpoint.Selectors[i];
            if (string.Equals(selector.Name, "Content-Encoding", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAvailableDictionarySelector(StaticWebAssetEndpoint endpoint)
    {
        for (var i = 0; i < endpoint.Selectors.Length; i++)
        {
            if (string.Equals(endpoint.Selectors[i].Name, "Available-Dictionary", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompatible(StaticWebAssetEndpoint compressedEndpoint, StaticWebAssetEndpoint relatedEndpointCandidate)
    {
        var compressedFingerprint = ResolveFingerprint(compressedEndpoint);
        var relatedFingerprint = ResolveFingerprint(relatedEndpointCandidate);
        return string.Equals(compressedFingerprint.Value, relatedFingerprint.Value, StringComparison.Ordinal);
    }

    private static StaticWebAssetEndpointProperty ResolveFingerprint(StaticWebAssetEndpoint compressedEndpoint)
    {
        foreach (var property in compressedEndpoint.EndpointProperties)
        {
            if (string.Equals(property.Name, "fingerprint", StringComparison.Ordinal))
            {
                return property;
            }
        }
        return default;
    }

    private void ApplyCompressedEndpointHeaders(List<StaticWebAssetEndpointResponseHeader> headers, StaticWebAssetEndpoint compressedEndpoint, string relatedEndpointCandidateRoute)
    {
        foreach (var header in compressedEndpoint.ResponseHeaders)
        {
            if (string.Equals(header.Name, "Content-Type", StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, "  Skipping Content-Type header for related endpoint '{0}'", relatedEndpointCandidateRoute);
                // Skip the content-type header since we are adding it from the original asset.
                continue;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "  Adding header '{0}' to related endpoint '{1}'", header.Name, relatedEndpointCandidateRoute);
                headers.Add(header);
            }
        }
    }

    private void ApplyRelatedEndpointCandidateHeaders(List<StaticWebAssetEndpointResponseHeader> headers, StaticWebAssetEndpoint relatedEndpointCandidate, HashSet<string> compressedHeaders)
    {
        foreach (var header in relatedEndpointCandidate.ResponseHeaders)
        {
            // We need to keep the headers that are specific to the compressed asset like Content-Length,
            // Last-Modified and ETag. Any other header we should add it.
            if (!compressedHeaders.Contains(header.Name))
            {
                Log.LogMessage(MessageImportance.Low, "  Adding header '{0}' to related endpoint '{1}'", header.Name, relatedEndpointCandidate.Route);
                headers.Add(header);
            }
            else if (string.Equals(AttachWeakETagToCompressedAssets, "ResponseHeader", StringComparison.Ordinal) && string.Equals(header.Name, "ETag", StringComparison.Ordinal))
            {
                // A resource can have multiple ETags. Since the uncompressed resource has an ETag,
                // and we are serving the compressed resource from the same URL, we need to update
                // the ETag on the compressed resource to indicate that is dependent on the representation
                // For example, a compressed resource has two ETags: W/"original-resource-etag" and
                // "compressed-resource-etag".
                // The browser will send both ETags in the If-None-Match header, and having the strong ETag
                // allows the server to support conditional range requests.
                Log.LogMessage(MessageImportance.Low, "  Updating ETag header for related endpoint '{0}'", relatedEndpointCandidate.Route);
                headers.Add(new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = $"W/{header.Value}"
                });
            }else if (string.Equals(header.Name, "Content-Type", StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, "Adding Content-Type '{1}' header to related endpoint '{0}'", relatedEndpointCandidate.Route, header.Value);
                // Add the Content-Type to make sure it matches the original asset.
                headers.Add(header);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "  Skipping header '{0}' for related endpoint '{1}'", header.Name, relatedEndpointCandidate.Route);
            }
        }
    }

    internal static Dictionary<string, int> BuildFormatPriority(ITaskItem[] compressionFormats)
    {
        var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (compressionFormats == null || compressionFormats.Length == 0)
        {
            return priority;
        }

        for (var i = 0; i < compressionFormats.Length; i++)
        {
            var encoding = compressionFormats[i].GetMetadata("ContentEncoding");
            if (!string.IsNullOrEmpty(encoding))
            {
            if (!string.IsNullOrEmpty(encoding) && !priority.ContainsKey(encoding))
            {
                priority[encoding] = i;
            }
            }
        }

        return priority;
    }

    internal static Dictionary<string, bool> BuildFormatUsesDictionary(ITaskItem[] compressionFormats)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (compressionFormats == null || compressionFormats.Length == 0)
        {
            return result;
        }

        for (var i = 0; i < compressionFormats.Length; i++)
        {
            var encoding = compressionFormats[i].GetMetadata("ContentEncoding");
            var usesDictionary = string.Equals(compressionFormats[i].GetMetadata("UsesDictionary"), "true", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(encoding))
            {
            if (!string.IsNullOrEmpty(encoding) && !result.ContainsKey(encoding))
            {
                result[encoding] = usesDictionary;
            }
            }
        }

        return result;
    }

    private static void EnsureVaryAvailableDictionaryHeader(StaticWebAssetEndpoint endpoint)
    {
        for (var i = 0; i < endpoint.ResponseHeaders.Length; i++)
        {
            var header = endpoint.ResponseHeaders[i];
            if (string.Equals(header.Name, "Vary", StringComparison.OrdinalIgnoreCase) &&
                header.Value.Contains("Available-Dictionary", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        endpoint.ResponseHeaders = [
            ..endpoint.ResponseHeaders,
            new StaticWebAssetEndpointResponseHeader
            {
                Name = "Vary",
                Value = "Available-Dictionary"
            }
        ];
    }

    private static void EnsureUseDictionaryHeader(StaticWebAssetEndpoint endpoint, string matchPattern)
    {
        // Check if header already exists
        for (var i = 0; i < endpoint.ResponseHeaders.Length; i++)
        {
            if (string.Equals(endpoint.ResponseHeaders[i].Name, "Use-As-Dictionary", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        // Use the match pattern from the dictionary candidate (derived from the old asset's
        // RelativePath with fingerprint tokens converted to wildcards).
        // Falls back to the endpoint's route if no match pattern is available.
        var pattern = !string.IsNullOrEmpty(matchPattern) ? matchPattern : endpoint.Route;

        endpoint.ResponseHeaders = [
            ..endpoint.ResponseHeaders,
            new StaticWebAssetEndpointResponseHeader
            {
                Name = "Use-As-Dictionary",
                Value = $"match=\"/{pattern}\""
            }
        ];
    }

    // Produces a descending quality series: 1.0, 0.9, 0.8, ..., 0.1, 0.09, 0.08, ..., 0.01, 0.009, ...
    // Each "tier" covers 9 values at one decimal place deeper, ensuring unique qualities for any number of variants.
    internal static string ComputeQualityValue(int rank)
    {
        if (rank == 0)
        {
            return "1.0";
        }

        var tier = (rank - 1) / 9;
        var position = (rank - 1) % 9;
        var digit = 9 - position;
        var decimals = tier + 1;
        var value = digit * Math.Pow(10, -decimals);
        return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
