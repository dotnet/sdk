// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ApplyCompressionNegotiation : Task
{
    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    public Func<string, long> TestResolveFileLength;

    public override bool Execute()
    {
        var assetsById = CandidateAssets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity);

        var endpointsByAsset = CandidateEndpoints.Select(StaticWebAssetEndpoint.FromTaskItem)
            .GroupBy(e => e.AssetFile)
            .ToDictionary(g => g.Key, g => g.ToList());

        var compressedAssets = assetsById.Values.Where(a => a.AssetTraitName == "Content-Encoding").ToList();
        var updatedEndpoints = new HashSet<StaticWebAssetEndpoint>(StaticWebAssetEndpoint.RouteAndAssetComparer);

        var preservedEndpoints = new Dictionary<(string, string), StaticWebAssetEndpoint>();

        // Add response headers to compressed endpoints
        foreach (var compressedAsset in compressedAssets)
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

            Log.LogMessage("Processing compressed asset: {0}", compressedAsset.Identity);

            var length = TestResolveFileLength != null
                ? TestResolveFileLength(compressedAsset.Identity)
                : new FileInfo(compressedAsset.Identity).Length;
            StaticWebAssetEndpointResponseHeader[] compressionHeaders = [
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

            foreach (var compressedEndpoint in compressedEndpoints)
            {
                if (compressedEndpoint.Selectors.Any(s => string.Equals(s.Name,"Content-Encoding", StringComparison.Ordinal)))
                {
                    Log.LogMessage(MessageImportance.Low, $"  Skipping endpoint '{compressedEndpoint.Route}' since it already has a Content-Encoding selector");
                    continue;
                }
                if (!compressedEndpoint.ResponseHeaders.Any(s => string.Equals(s.Name, "Content-Encoding", StringComparison.Ordinal)))
                {
                    // Add the Content-Encoding and Vary headers
                    compressedEndpoint.ResponseHeaders = [
                        ..compressedEndpoint.ResponseHeaders,
                        ..compressionHeaders
                    ];
                }

                Log.LogMessage(MessageImportance.Low, "  Updated endpoint '{0}' with Content-Encoding and Vary headers", compressedEndpoint.Route);
                updatedEndpoints.Add(compressedEndpoint);

                foreach (var relatedEndpointCandidate in relatedAssetEndpoints)
                {
                    if (!IsCompatible(compressedEndpoint, relatedEndpointCandidate))
                    {
                        continue;
                    }
                    Log.LogMessage(MessageImportance.Low, "Processing related endpoint '{0}'", relatedEndpointCandidate.Route);
                    var encodingSelector = new StaticWebAssetEndpointSelector
                    {
                        Name = "Content-Encoding",
                        Value = compressedAsset.AssetTraitValue,
                        Quality = Math.Round(1.0 / (length + 1), 12).ToString("F12", CultureInfo.InvariantCulture)
                    };
                    Log.LogMessage(MessageImportance.Low, "  Created Content-Encoding selector for compressed asset '{0}' with size '{1}' is '{2}'", encodingSelector.Value, encodingSelector.Quality, relatedEndpointCandidate.Route);
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
                    ApplyCompressedEndpointHeaders(headers, compressedEndpoint, relatedEndpointCandidate.Route);
                    ApplyRelatedEndpointCandidateHeaders(headers, relatedEndpointCandidate, compressedHeaders);
                    endpointCopy.ResponseHeaders = [.. headers];

                    // Update the endpoint
                    Log.LogMessage(MessageImportance.Low, "  Updated related endpoint '{0}' with Content-Encoding selector '{1}={2}'", relatedEndpointCandidate.Route, encodingSelector.Value, encodingSelector.Quality);
                    updatedEndpoints.Add(endpointCopy);

                    // Since we are going to remove the endpoints from the associated item group and the route is
                    // the ItemSpec, we want to add the original as well so that it gets re-added.
                    // The endpoint pointing to the uncompressed asset doesn't have a Content-Encoding selector and
                    // will use the default "identity" encoding during content negotiation.
                    if (!preservedEndpoints.ContainsKey((relatedEndpointCandidate.Route, relatedEndpointCandidate.AssetFile)))
                    {
                        preservedEndpoints.Add(
                            (relatedEndpointCandidate.Route, relatedEndpointCandidate.AssetFile),
                            relatedEndpointCandidate);
                    }
                }
            }
        }

        // Add the preserved endpoints to the list of updated endpoints.
        foreach (var preservedEndpoint in preservedEndpoints.Values)
        {
            if (!updatedEndpoints.Contains(preservedEndpoint))
            {
                updatedEndpoints.Add(preservedEndpoint);
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
        var endpointsByRoute = endpointsByAsset.Values.SelectMany(e => e).GroupBy(e => e.Route).ToDictionary(g => g.Key, g => g.ToList());

        var updatedEndpointsByRoute = updatedEndpoints.Select(e => e.Route).ToArray();
        foreach (var route in updatedEndpointsByRoute)
        {
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
                    updatedEndpoints.Add(endpoint);
                }
            }
        }

        UpdatedEndpoints = updatedEndpoints.Distinct().Select(e => e.ToTaskItem()).ToArray();

        return true;
    }

    private static bool IsCompatible(StaticWebAssetEndpoint compressedEndpoint, StaticWebAssetEndpoint relatedEndpointCandidate)
    {
        var compressedFingerprint = compressedEndpoint.EndpointProperties.FirstOrDefault(ep => ep.Name == "fingerprint");
        var relatedFingerprint = relatedEndpointCandidate.EndpointProperties.FirstOrDefault(ep => ep.Name == "fingerprint");
        return string.Equals(compressedFingerprint?.Value, relatedFingerprint?.Value, StringComparison.Ordinal);
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
