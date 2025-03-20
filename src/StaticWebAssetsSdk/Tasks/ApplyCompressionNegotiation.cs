// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ApplyCompressionNegotiation : Task
{
    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    public override bool Execute()
    {
        var assetsById = StaticWebAsset.ToDictionaryFromItemGroup(CandidateAssets);

        var endpointsByAsset = StaticWebAssetEndpoint.ToAssetFileDictionary(CandidateEndpoints);

        var updatedEndpoints = new HashSet<StaticWebAssetEndpoint>(StaticWebAssetEndpoint.RouteAndAssetComparer);

        ProcessCompressedAssets(assetsById, endpointsByAsset, updatedEndpoints);

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
        var additionalUpdatedEndpoints = new HashSet<StaticWebAssetEndpoint>(StaticWebAssetEndpoint.RouteAndAssetComparer);
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

        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(updatedEndpoints);

        return true;
    }

    private class ApplyCompressionNegotiationParallelWorker
    {
        private readonly TaskLoggingHelper _log;
        private readonly IDictionary<string, StaticWebAsset> _assetsById;
        private readonly IDictionary<string, List<StaticWebAssetEndpoint>> _endpointsByAsset;
        private readonly HashSet<StaticWebAssetEndpoint> _localUpdatedEndpoints;
        private readonly HashSet<StaticWebAssetEndpoint> _updatedEndpoints;
        private readonly Dictionary<string, StaticWebAssetEndpointResponseHeader[]> _compressionHeadersByEncoding;

        public ApplyCompressionNegotiationParallelWorker(
            TaskLoggingHelper Log,
            IDictionary<string, StaticWebAsset> assetsById,
            IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
            HashSet<StaticWebAssetEndpoint> updatedEndpoints)
        {
            _log = Log;
            _assetsById = assetsById;
            _endpointsByAsset = endpointsByAsset;
            _localUpdatedEndpoints = new(StaticWebAssetEndpoint.RouteAndAssetComparer);
            _updatedEndpoints = updatedEndpoints;
            _compressionHeadersByEncoding = [];
        }

        public ApplyCompressionNegotiationParallelWorker Process(StaticWebAsset compressedAsset)
        {
            if (!string.Equals(compressedAsset.AssetTraitName, "Content-Encoding", StringComparison.Ordinal))
            {
                return this;
            }

            var (compressedEndpoints, relatedAssetEndpoints) = ResolveEndpoints(_log, _assetsById, _endpointsByAsset, compressedAsset);

            _log.LogMessage("Processing compressed asset: {0}", compressedAsset.Identity);
            var compressionHeaders = GetOrCreateCompressionHeaders(_compressionHeadersByEncoding, compressedAsset);

            var quality = ResolveQuality(compressedAsset);
            foreach (var compressedEndpoint in compressedEndpoints)
            {
                if (HasContentEncodingSelector(compressedEndpoint))
                {
                    _log.LogMessage(MessageImportance.Low, $"  Skipping endpoint '{compressedEndpoint.Route}' since it already has a Content-Encoding selector");
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

                _log.LogMessage(MessageImportance.Low, "  Updated endpoint '{0}' with Content-Encoding and Vary headers", compressedEndpoint.Route);
                _localUpdatedEndpoints.Add(compressedEndpoint);

                foreach (var relatedEndpointCandidate in relatedAssetEndpoints)
                {
                    if (!IsCompatible(compressedEndpoint, relatedEndpointCandidate))
                    {
                        continue;
                    }

                    var updatedEndpoint = CreateUpdatedEndpoint(_log, compressedAsset, quality, compressedEndpoint, relatedEndpointCandidate);
                    _localUpdatedEndpoints.Add(updatedEndpoint);
                    _localUpdatedEndpoints.Add(relatedEndpointCandidate);
                }
            }

            return this;
        }

        internal void Finally()
        {
            lock (_updatedEndpoints)
            {
                _updatedEndpoints.UnionWith(_localUpdatedEndpoints);
            }
        }
    }

    private void ProcessCompressedAssets(
        IDictionary<string, StaticWebAsset> assetsById,
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
        HashSet<StaticWebAssetEndpoint> updatedEndpoints)
    {
        Parallel.ForEach(assetsById.Values,
            localInit: () => new ApplyCompressionNegotiationParallelWorker(
                Log,
                assetsById,
                endpointsByAsset,
                updatedEndpoints),
            body: (asset, ls, worker) => worker.Process(asset),
            worker => worker.Finally());
    }

    private static Dictionary<string, List<StaticWebAssetEndpoint>> GetEndpointsByRoute(
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset)
    {
        var result = new Dictionary<string, List<StaticWebAssetEndpoint>>();

        foreach (var endpointsList in endpointsByAsset.Values)
        {
            foreach (var endpoint in endpointsList)
            {
                if (!result.TryGetValue(endpoint.Route, out var routeEndpoints))
                {
                    routeEndpoints = [];
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

    private static StaticWebAssetEndpoint CreateUpdatedEndpoint(
        TaskLoggingHelper log,
        StaticWebAsset compressedAsset,
        string quality,
        StaticWebAssetEndpoint compressedEndpoint,
        StaticWebAssetEndpoint relatedEndpointCandidate)
    {
        log.LogMessage(MessageImportance.Low, "Processing related endpoint '{0}'", relatedEndpointCandidate.Route);
        var encodingSelector = new StaticWebAssetEndpointSelector
        {
            Name = "Content-Encoding",
            Value = compressedAsset.AssetTraitValue,
            Quality = quality
        };
        log.LogMessage(MessageImportance.Low, "  Created Content-Encoding selector for compressed asset '{0}' with size '{1}' is '{2}'", encodingSelector.Value, encodingSelector.Quality, relatedEndpointCandidate.Route);
        var endpointCopy = new StaticWebAssetEndpoint
        {
            AssetFile = compressedAsset.Identity,
            Route = relatedEndpointCandidate.Route,
            Selectors = [
                ..relatedEndpointCandidate.Selectors,
                            encodingSelector
            ],
            EndpointProperties = [.. relatedEndpointCandidate.EndpointProperties]
        };
        var headers = new List<StaticWebAssetEndpointResponseHeader>();
        var compressedHeaders = new HashSet<string>(compressedEndpoint.ResponseHeaders.Select(h => h.Name), StringComparer.Ordinal);
        ApplyCompressedEndpointHeaders(log, headers, compressedEndpoint, relatedEndpointCandidate.Route);
        ApplyRelatedEndpointCandidateHeaders(log, headers, relatedEndpointCandidate, compressedHeaders);
        endpointCopy.ResponseHeaders = [.. headers];

        // Update the endpoint
        log.LogMessage(MessageImportance.Low, "  Updated related endpoint '{0}' with Content-Encoding selector '{1}={2}'", relatedEndpointCandidate.Route, encodingSelector.Value, encodingSelector.Quality);
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

    private static (List<StaticWebAssetEndpoint> compressedEndpoints, List<StaticWebAssetEndpoint> relatedAssetEndpoints) ResolveEndpoints(
        TaskLoggingHelper log,
        IDictionary<string, StaticWebAsset> assetsById,
        IDictionary<string, List<StaticWebAssetEndpoint>> endpointsByAsset,
        StaticWebAsset compressedAsset)
    {
        if (!assetsById.TryGetValue(compressedAsset.RelatedAsset, out var relatedAsset))
        {
            log.LogWarning("Related asset not found for compressed asset: {0}", compressedAsset.Identity);
            throw new InvalidOperationException($"Related asset not found for compressed asset: {compressedAsset.Identity}");
        }

        if (!endpointsByAsset.TryGetValue(compressedAsset.Identity, out var compressedEndpoints))
        {
            log.LogWarning("Endpoints not found for compressed asset: {0} {1}", compressedAsset.RelativePath, compressedAsset.Identity);
            throw new InvalidOperationException($"Endpoints not found for compressed asset: {compressedAsset.Identity}");
        }

        if (!endpointsByAsset.TryGetValue(relatedAsset.Identity, out var relatedAssetEndpoints))
        {
            log.LogWarning("Endpoints not found for related asset: {0}", relatedAsset.Identity);
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
        return string.Equals(compressedFingerprint?.Value, relatedFingerprint?.Value, StringComparison.Ordinal);
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
        return null;
    }

    private static void ApplyCompressedEndpointHeaders(
        TaskLoggingHelper log,
        List<StaticWebAssetEndpointResponseHeader> headers,
        StaticWebAssetEndpoint compressedEndpoint,
        string relatedEndpointCandidateRoute)
    {
        foreach (var header in compressedEndpoint.ResponseHeaders)
        {
            if (string.Equals(header.Name, "Content-Type", StringComparison.Ordinal))
            {
                log.LogMessage(MessageImportance.Low, "  Skipping Content-Type header for related endpoint '{0}'", relatedEndpointCandidateRoute);
                // Skip the content-type header since we are adding it from the original asset.
                continue;
            }
            else
            {
                log.LogMessage(MessageImportance.Low, "  Adding header '{0}' to related endpoint '{1}'", header.Name, relatedEndpointCandidateRoute);
                headers.Add(header);
            }
        }
    }

    private static void ApplyRelatedEndpointCandidateHeaders(
        TaskLoggingHelper log,
        List<StaticWebAssetEndpointResponseHeader> headers,
        StaticWebAssetEndpoint relatedEndpointCandidate,
        HashSet<string> compressedHeaders)
    {
        foreach (var header in relatedEndpointCandidate.ResponseHeaders)
        {
            // We need to keep the headers that are specific to the compressed asset like Content-Length,
            // Last-Modified and ETag. Any other header we should add it.
            if (!compressedHeaders.Contains(header.Name))
            {
                log.LogMessage(MessageImportance.Low, "  Adding header '{0}' to related endpoint '{1}'", header.Name, relatedEndpointCandidate.Route);
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
                log.LogMessage(MessageImportance.Low, "  Updating ETag header for related endpoint '{0}'", relatedEndpointCandidate.Route);
                headers.Add(new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = $"W/{header.Value}"
                });
            }
            else if (string.Equals(header.Name, "Content-Type", StringComparison.Ordinal))
            {
                log.LogMessage(MessageImportance.Low, "Adding Content-Type '{1}' header to related endpoint '{0}'", relatedEndpointCandidate.Route, header.Value);
                // Add the Content-Type to make sure it matches the original asset.
                headers.Add(header);
            }
            else
            {
                log.LogMessage(MessageImportance.Low, "  Skipping header '{0}' for related endpoint '{1}'", header.Name, relatedEndpointCandidate.Route);
            }
        }
    }
}
