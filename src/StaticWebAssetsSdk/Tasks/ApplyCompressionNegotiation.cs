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

    public string AttachWeakETagToCompressedAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    public override bool Execute()
    {
        var assetsById = StaticWebAsset.ToAssetDictionary(CandidateAssets);

        var endpointsByAsset = StaticWebAssetEndpoint.ToAssetFileDictionary(CandidateEndpoints);

        var updatedEndpoints = new HashSet<StaticWebAssetEndpoint>(CandidateEndpoints.Length, StaticWebAssetEndpoint.RouteAndAssetComparer);

        var compressionHeadersByEncoding = new Dictionary<string, StaticWebAssetEndpointResponseHeader[]>(2);

        // Add response headers to compressed endpoints
        foreach (var compressedAsset in assetsById.Values)
        {
            if (!string.Equals(compressedAsset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                continue;
            }

            var (compressedEndpoints, relatedAssetEndpoints) = ResolveEndpoints(assetsById, endpointsByAsset, compressedAsset);

            Log.LogMessage("Processing compressed asset: {0}", compressedAsset.Identity);
            var compressionHeaders = GetOrCreateCompressionHeaders(compressionHeadersByEncoding, compressedAsset);

            var quality = ResolveQuality(compressedAsset);
            foreach (var compressedEndpoint in compressedEndpoints)
            {
                if (HasContentEncodingSelector(compressedEndpoint))
                {
                    Log.LogMessage(MessageImportance.Low, "  Skipping endpoint '{0}' since it already has a Content-Encoding selector", compressedEndpoint.Route);
                    continue;
                }

                if (!HasContentEncodingResponseHeader(compressedEndpoint))
                {
                    // Add the Content-Encoding and Vary headers
                    compressedEndpoint.ResponseHeaders = [
                        ..compressedEndpoint.ResponseHeaders,
                        ..compressionHeaders
                    ];
                }

                var compressedHeaders = GetCompressedHeaders(compressedEndpoint);

                Log.LogMessage(MessageImportance.Low, "  Updated endpoint '{0}' with Content-Encoding and Vary headers", compressedEndpoint.Route);
                updatedEndpoints.Add(compressedEndpoint);

                foreach (var relatedEndpointCandidate in relatedAssetEndpoints)
                {
                    if (!IsCompatible(compressedEndpoint, relatedEndpointCandidate))
                    {
                        continue;
                    }

                    var endpointCopy = CreateUpdatedEndpoint(compressedAsset, quality, compressedEndpoint, compressedHeaders, relatedEndpointCandidate);
                    updatedEndpoints.Add(endpointCopy);
                    // Since we are going to remove the endpoints from the associated item group and the route is
                    // the ItemSpec, we want to add the original as well so that it gets re-added.
                    // The endpoint pointing to the uncompressed asset doesn't have a Content-Encoding selector and
                    // will use the default "identity" encoding during content negotiation.
                    if(!HasVaryResponseHeaderWithAcceptEncoding(relatedEndpointCandidate))
                    {
                        Log.LogMessage(MessageImportance.Low, "  Adding Vary response header to related endpoint '{0}'", relatedEndpointCandidate.Route);

                        relatedEndpointCandidate.ResponseHeaders = [
                            ..relatedEndpointCandidate.ResponseHeaders,
                            new StaticWebAssetEndpointResponseHeader
                            {
                                Name = "Vary",
                                Value = "Accept-Encoding"
                            }
                        ];
                    }
                    updatedEndpoints.Add(relatedEndpointCandidate);
                }
            }
        }

        // Before we return the updated endpoints we need to capture any other endpoint whose asset is not associated
        // with the compressed asset. This is because we are going to remove the endpoints from the associated item group
        // and the route is the ItemSpec, so it will cause those endpoints to be removed.
        // For example, we have css/app.css and Link/css/app.css where Link=css/app.css and the first asset is a build asset
        // and the second asset is a publish asset.
        // If we are processing build assets, we'll mistakenly remove the endpoints associated with the publish asset.

        // Iterate over the endpoints and find those endpoints whose route is in the set of updated endpoints but whose asset
        // is not, and add them to the updated endpoints.

        // Reuse the map we created at the beginning.
        // Remove all the endpoints that were updated to avoid adding them again.
        foreach (var endpoint in updatedEndpoints)
        {
            if (endpointsByAsset.TryGetValue(endpoint.AssetFile, out var endpointsToSkip))
            {
                foreach (var endpointToSkip in endpointsToSkip)
                {
                    Log.LogMessage(MessageImportance.Low, "    Skipping endpoint '{0}' since and endpoint for the same asset was updated.", endpointToSkip.Route);
                }
            }
            endpointsByAsset.Remove(endpoint.AssetFile);
        }

        // We now have only endpoints that might have the same route but point to different assets
        // and we want to include them in the updated endpoints so that we don't incorrectly remove
        // them from the associated item group when we update the endpoints.
        var endpointsByRoute = GetEndpointsByRoute(endpointsByAsset);
        var additionalUpdatedEndpoints = new HashSet<StaticWebAssetEndpoint>(updatedEndpoints.Count, StaticWebAssetEndpoint.RouteAndAssetComparer);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            var route = updatedEndpoint.Route;
            Log.LogMessage(MessageImportance.Low, "Processing route '{0}'", route);
            if (endpointsByRoute.TryGetValue(route, out var endpoints))
            {
                Log.LogMessage(MessageImportance.Low, "  Found endpoints for route '{0}'", route);
                foreach (var endpoint in endpoints)
                {
                    Log.LogMessage(MessageImportance.Low, "    Adding endpoint '{0}'", endpoint.AssetFile);
                    if (!HasVaryResponseHeaderWithAcceptEncoding(endpoint))
                    {
                        endpoint.ResponseHeaders = [
                            .. endpoint.ResponseHeaders,
                            new StaticWebAssetEndpointResponseHeader
                            {
                                Name = "Vary",
                                Value = "Accept-Encoding"
                            }
                        ];
                    }
                    additionalUpdatedEndpoints.Add(endpoint);
                }
            }
        }

        additionalUpdatedEndpoints.UnionWith(updatedEndpoints);

        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(additionalUpdatedEndpoints);

        return true;
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

    private static Dictionary<string, List<StaticWebAssetEndpoint>> GetEndpointsByRoute(
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset)
    {
        var result = new Dictionary<string, List<StaticWebAssetEndpoint>>(endpointsByAsset.Count);

        foreach (var endpointsList in endpointsByAsset.Values)
        {
            foreach (var endpoint in endpointsList)
            {
                if (!result.TryGetValue(endpoint.Route, out var routeEndpoints))
                {
                    routeEndpoints = new List<StaticWebAssetEndpoint>(5);
                    result[endpoint.Route] = routeEndpoints;
                }
                routeEndpoints.Add(endpoint);
            }
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
        StaticWebAssetEndpoint relatedEndpointCandidate)
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

        var endpointCopy = new StaticWebAssetEndpoint
        {
            AssetFile = compressedAsset.Identity,
            Route = relatedEndpointCandidate.Route,
            Selectors = [
                ..relatedEndpointCandidate.Selectors,
                encodingSelector
            ],
            EndpointProperties = [.. endpointProperties]
        };
        var headers = new List<StaticWebAssetEndpointResponseHeader>(7);
        ApplyCompressedEndpointHeaders(headers, compressedEndpoint, relatedEndpointCandidate.Route);
        ApplyRelatedEndpointCandidateHeaders(headers, relatedEndpointCandidate, compressedHeaders);
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

    private (List<StaticWebAssetEndpoint> compressedEndpoints, List<StaticWebAssetEndpoint> relatedAssetEndpoints) ResolveEndpoints(
        IDictionary<string, StaticWebAsset> assetsById,
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
        StaticWebAsset compressedAsset)
    {
        if (!assetsById.TryGetValue(compressedAsset.RelatedAsset, out var relatedAsset))
        {
            Log.LogWarning("Related asset not found for compressed asset: {0}", compressedAsset.Identity);
            throw new InvalidOperationException($"Related asset not found for compressed asset: {compressedAsset.Identity}");
        }

        if (!endpointsByAsset.TryGetValue(compressedAsset.Identity, out var compressedEndpoints))
        {
            Log.LogWarning("Endpoints not found for compressed asset: {0} {1}", compressedAsset.RelativePath, compressedAsset.Identity);
            throw new InvalidOperationException($"Endpoints not found for compressed asset: {compressedAsset.Identity}");
        }

        if (!endpointsByAsset.TryGetValue(relatedAsset.Identity, out var relatedAssetEndpoints))
        {
            Log.LogWarning("Endpoints not found for related asset: {0}", relatedAsset.Identity);
            throw new InvalidOperationException($"Endpoints not found for related asset: {relatedAsset.Identity}");
        }

        return (compressedEndpoints, relatedAssetEndpoints);
    }

    private static string ResolveQuality(StaticWebAsset compressedAsset) =>
        Math.Round(1.0 / (compressedAsset.FileLength + 1), 12).ToString("F12", CultureInfo.InvariantCulture);

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
}
