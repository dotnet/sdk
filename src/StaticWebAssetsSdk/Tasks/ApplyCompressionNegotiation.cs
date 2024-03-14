// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public override bool Execute()
    {
        var assetsById = CandidateAssets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity);

        var endpointsByAsset = CandidateEndpoints.Select(StaticWebAssetEndpoint.FromTaskItem)
            .GroupBy(e => e.AssetFile)
            .ToDictionary(g => g.Key, g => g.ToList());

        var compressedAssets = assetsById.Values.Where(a => a.AssetTraitName == "Content-Encoding").ToList();
        var updatedEndpoints = new List<StaticWebAssetEndpoint>();

        // Add response headers to compressed endpoints
        foreach (var compressedAsset in compressedAssets)
        {
            var compressedEndpoints = endpointsByAsset[compressedAsset.Identity];
            var relatedAsset = assetsById[compressedAsset.RelatedAsset];
            var relatedAssetEndpoints = endpointsByAsset[relatedAsset.Identity];
            var length = new FileInfo(compressedAsset.Identity).Length;
            foreach (var endpoint in compressedEndpoints)
            {
                if (endpoint.Selectors.Any(s => s.Name == "Content-Encoding"))
                {
                    // The endpoint is already defined.
                    continue;
                }

                StaticWebAssetEndpointResponseHeader[] compressionHeaders = [
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
                // Add the Content-Encoding and Vary headers
                endpoint.ResponseHeaders = [
                    ..endpoint.ResponseHeaders,
                    ..compressionHeaders
                ];

                // We are done with the endpoint.
                updatedEndpoints.Add(endpoint);

                foreach (var relatedEndpointCandidate in relatedAssetEndpoints)
                {
                    var endpointCopy = new StaticWebAssetEndpoint
                    {
                        AssetFile = compressedAsset.Identity,
                        Route = relatedEndpointCandidate.Route,
                        Selectors = [
                            .. relatedEndpointCandidate.Selectors,
                            new StaticWebAssetEndpointSelector
                            {
                                Name = "Content-Encoding",
                                Value = compressedAsset.AssetTraitValue,
                                Quality = 1.0 / (length + 1)
                            }
                        ],
                        EndpointProperties = [.. relatedEndpointCandidate.EndpointProperties]
                    };

                    var headers = new List<StaticWebAssetEndpointResponseHeader>();
                    var compressedHeaders = new HashSet<string>(compressionHeaders.Select(h => h.Name));
                    foreach (var header in relatedEndpointCandidate.ResponseHeaders)
                    {
                        // We need to keep the headers that are specific to the compressed asset like Content-Length,
                        // Last-Modified and ETag. Any other header we should add it.
                        if (!compressedHeaders.Contains(header.Name))
                        {
                            headers.Add(header);
                        }
                        else if (header.Name == "ETag")
                        {
                            // A resource can have multiple ETags. Since the uncompressed resource has an ETag,
                            // and we are serving the compressed resource from the same URL, we need to update
                            // the ETag on the compressed resource to indicate that is dependent on the representation
                            // For example, a compressed resource has two ETags: W/"original-resource-etag" and
                            // "compressed-resource-etag".
                            // The browser will send both ETags in the If-None-Match header, and having the strong ETag
                            // allows the server to support conditional range requests.
                            headers.Add(new StaticWebAssetEndpointResponseHeader
                            {
                                Name = "ETag",
                                Value = $"W/\"{header.Value}\""
                            });
                        }
                        else if (header.Name == "Content-Type")
                        {
                            // Add the Content-Type to make sure it matches the original asset.
                            headers.Add(header);
                        }
                    }

                    foreach (var header in endpoint.ResponseHeaders)
                    {
                        if (header.Name == "Content-Type")
                        {
                            // Skip the content-type header since we are adding it from the original asset.
                            continue;
                        }
                        headers.Add(header);
                    }

                    // Update the endpoint
                    updatedEndpoints.Add(endpointCopy);
                    // Since we are going to remove the endpoints from the associated item group and the route is
                    // the ItemSpec, we want to add the original as well so that it gets re-added.
                    // The endpoint pointing to the uncompresset asset doesn't have a Content-Encoding selector and
                    // will use the default "identity" encoding during content negotiation.
                    updatedEndpoints.Add(relatedEndpointCandidate);
                }
            }
        }

        UpdatedEndpoints = updatedEndpoints.Select(e => e.ToTaskItem()).ToArray();

        return true;
    }
}
