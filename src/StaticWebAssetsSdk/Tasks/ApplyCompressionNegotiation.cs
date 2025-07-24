// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ApplyCompressionNegotiation : Task
{
    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    private readonly List<StaticWebAssetEndpointSelector> _selectorsList = [];
    private readonly List<StaticWebAssetEndpointResponseHeader> _headersList = [];
    private readonly List<StaticWebAssetEndpointResponseHeader> _tempHeadersList = [];
    private readonly List<StaticWebAssetEndpointProperty> _propertiesList = [];
    private const int ExpectedCompressionHeadersCount = 2;

    public override bool Execute()
    {
        var assetsById = StaticWebAsset.ToAssetDictionary(CandidateAssets);

        var endpointsByAsset = StaticWebAssetEndpoint.ToAssetFileDictionary(CandidateEndpoints);

        var updatedEndpoints = new HashSet<StaticWebAssetEndpoint>(CandidateEndpoints.Length, StaticWebAssetEndpoint.RouteAndAssetComparer);

        var compressionHeadersByEncoding = new Dictionary<string, StaticWebAssetEndpointResponseHeader[]>(ExpectedCompressionHeadersCount);

        using var jsonContext = new JsonWriterContext();

        ProcessCompressedAssets(assetsById, endpointsByAsset, updatedEndpoints, compressionHeadersByEncoding, jsonContext);
        AddRemainingEndpoints(endpointsByAsset, updatedEndpoints);
        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(updatedEndpoints);
        return true;
    }

    private void ProcessCompressedAssets(
        Dictionary<string, StaticWebAsset> assetsById,
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
        HashSet<StaticWebAssetEndpoint> updatedEndpoints,
        Dictionary<string, StaticWebAssetEndpointResponseHeader[]> compressionHeadersByEncoding,
        JsonWriterContext jsonContext)
    {
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
                    StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(compressedEndpoint.ResponseHeadersString, _headersList);
                    var currentCompressionHeaders = GetOrCreateCompressionHeaders(compressionHeadersByEncoding, compressedAsset);
                    _headersList.AddRange(currentCompressionHeaders);
                    var headersString = StaticWebAssetEndpointResponseHeader.ToMetadataValue(_headersList, jsonContext);
                    compressedEndpoint.SetResponseHeadersString(headersString);
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

                    var endpointCopy = CreateUpdatedEndpoint(compressedAsset, quality, compressedEndpoint, compressedHeaders, relatedEndpointCandidate, jsonContext);
                    updatedEndpoints.Add(endpointCopy);
                    // Since we are going to remove the endpoints from the associated item group and the route is
                    // the ItemSpec, we want to add the original as well so that it gets re-added.
                    // The endpoint pointing to the uncompressed asset doesn't have a Content-Encoding selector and
                    // will use the default "identity" encoding during content negotiation.
                    updatedEndpoints.Add(relatedEndpointCandidate);
                }
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
    private void AddRemainingEndpoints(
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
        HashSet<StaticWebAssetEndpoint> updatedEndpoints)
    {
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
                }
                foreach (var endpoint in endpoints)
                {
                    additionalUpdatedEndpoints.Add(endpoint);
                }
            }
        }

        updatedEndpoints.UnionWith(additionalUpdatedEndpoints);
    }

    private HashSet<string> GetCompressedHeaders(StaticWebAssetEndpoint compressedEndpoint)
    {
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(compressedEndpoint.ResponseHeadersString, _headersList);

        var result = new HashSet<string>(_headersList.Count, StringComparer.Ordinal);
        for (var i = 0; i < _headersList.Count; i++)
        {
            var responseHeader = _headersList[i];
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
                Value = "Content-Encoding"
            }
        ];

    private StaticWebAssetEndpoint CreateUpdatedEndpoint(
        StaticWebAsset compressedAsset,
        string quality,
        StaticWebAssetEndpoint compressedEndpoint,
        HashSet<string> compressedHeaders,
        StaticWebAssetEndpoint relatedEndpointCandidate,
        JsonWriterContext jsonContext)
    {
        Log.LogMessage(MessageImportance.Low, "Processing related endpoint '{0}'", relatedEndpointCandidate.Route);
        var encodingSelector = new StaticWebAssetEndpointSelector
        {
            Name = "Content-Encoding",
            Value = compressedAsset.AssetTraitValue,
            Quality = quality
        };
        Log.LogMessage(MessageImportance.Low, "  Created Content-Encoding selector for compressed asset '{0}' with size '{1}' is '{2}'", encodingSelector.Value, encodingSelector.Quality, relatedEndpointCandidate.Route);

        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(relatedEndpointCandidate.SelectorsString, _selectorsList);
        _selectorsList.Add(encodingSelector);
        var selectorsString = StaticWebAssetEndpointSelector.ToMetadataValue(_selectorsList, jsonContext);

        var endpointCopy = new StaticWebAssetEndpoint
        {
            AssetFile = compressedAsset.Identity,
            Route = relatedEndpointCandidate.Route,
        };

        endpointCopy.SetSelectorsString(selectorsString);
        endpointCopy.SetEndpointPropertiesString(relatedEndpointCandidate.EndpointPropertiesString);

        // Build headers using reusable list
        _headersList.Clear();
        ApplyCompressedEndpointHeaders(_headersList, compressedEndpoint, relatedEndpointCandidate.Route);
        ApplyRelatedEndpointCandidateHeaders(_headersList, relatedEndpointCandidate, compressedHeaders);
        var headersString = StaticWebAssetEndpointResponseHeader.ToMetadataValue(_headersList, jsonContext);
        endpointCopy.SetResponseHeadersString(headersString);

        // Update the endpoint
        Log.LogMessage(MessageImportance.Low, "  Updated related endpoint '{0}' with Content-Encoding selector '{1}={2}'", relatedEndpointCandidate.Route, encodingSelector.Value, encodingSelector.Quality);
        return endpointCopy;
    }

    private bool HasContentEncodingResponseHeader(StaticWebAssetEndpoint compressedEndpoint)
    {
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(compressedEndpoint.ResponseHeadersString, _headersList);

        for (var i = 0; i < _headersList.Count; i++)
        {
            var responseHeader = _headersList[i];
            if (string.Equals(responseHeader.Name, "Content-Encoding", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasContentEncodingSelector(StaticWebAssetEndpoint compressedEndpoint)
    {
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(compressedEndpoint.SelectorsString, _selectorsList);

        for (var i = 0; i < _selectorsList.Count; i++)
        {
            var selector = _selectorsList[i];
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

    private bool IsCompatible(StaticWebAssetEndpoint compressedEndpoint, StaticWebAssetEndpoint relatedEndpointCandidate)
    {
        var compressedFingerprint = ResolveFingerprint(compressedEndpoint, _propertiesList);
        var relatedFingerprint = ResolveFingerprint(relatedEndpointCandidate, _propertiesList);
        return string.Equals(compressedFingerprint.Value, relatedFingerprint.Value, StringComparison.Ordinal);
    }

    private static StaticWebAssetEndpointProperty ResolveFingerprint(StaticWebAssetEndpoint compressedEndpoint, List<StaticWebAssetEndpointProperty> tempList)
    {
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(compressedEndpoint.EndpointPropertiesString, tempList);

        foreach (var property in tempList)
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
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(compressedEndpoint.ResponseHeadersString, _tempHeadersList);

        foreach (var header in _tempHeadersList)
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
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(relatedEndpointCandidate.ResponseHeadersString, _tempHeadersList);

        foreach (var header in _tempHeadersList)
        {
            // We need to keep the headers that are specific to the compressed asset like Content-Length,
            // Last-Modified and ETag. Any other header we should add it.
            if (!compressedHeaders.Contains(header.Name))
            {
                Log.LogMessage(MessageImportance.Low, "  Adding header '{0}' to related endpoint '{1}'", header.Name, relatedEndpointCandidate.Route);
                headers.Add(header);
            }
            else if (string.Equals(header.Name, "ETag", StringComparison.Ordinal))
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
            }
            else if (string.Equals(header.Name, "Content-Type", StringComparison.Ordinal))
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
