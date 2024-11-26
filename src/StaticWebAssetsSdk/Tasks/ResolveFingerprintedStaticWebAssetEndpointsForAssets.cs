// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// There is a common operation that several targets need to perform when they want to reference endpoints
// inside the contents of an application. This can be done by a combination of tasks in the SDK, but it is
// cumbersome to do so, specially for third-party targets and SDKs. This task encapsulates the logic to
// resolve the preferrred set of endpoints for a given set of assets, taking into account the hosting model
// (Standalone or Hosted) and ensuring that fingerprinted endpoints are used when possible.
public class ResolveFingerprintedStaticWebAssetEndpointsForAssets : Task
{
    [Required] public ITaskItem[] CandidateEndpoints { get; set; }

    [Required] public ITaskItem[] CandidateAssets { get; set; }

    public bool IsStandalone { get; set; }

    [Output] public ITaskItem[] ResolvedEndpoints { get; set; }

    public override bool Execute()
    {
        var candidateEndpoints = StaticWebAssetEndpoint.FromItemGroup(CandidateEndpoints);
        var candidateAssets = CandidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray();
        var resolvedEndpoints = new List<StaticWebAssetEndpoint>();

        var endpointsByAsset = candidateEndpoints.GroupBy(e => e.AssetFile, OSPath.PathComparer)
            .ToDictionary(g => g.Key, g => g.ToArray(), OSPath.PathComparer);

        for (var i = 0; i < candidateAssets.Length; i++)
        {
            var asset = candidateAssets[i];
            if (!endpointsByAsset.TryGetValue(asset.Identity, out var endpoints))
            {
                Log.LogError($"No endpoint found for asset '{asset.Identity}'");
            }
            else
            {
                if (IsStandalone)
                {
                    var assetPath = asset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);
                    var foundMatchingEndpoint = false;
                    for (var j = 0; j < endpoints.Length; j++)
                    {
                        var endpoint = endpoints[j];
                        if (MatchesAssetPath(asset, assetPath, endpoint))
                        {
                            foundMatchingEndpoint = true;
                            resolvedEndpoints.Add(endpoint);
                            break;
                        }
                    }
                    if (!foundMatchingEndpoint)
                    {
                        Log.LogError($"No endpoint found for asset '{asset.Identity}' with path '{assetPath}' whose route matches its path.");
                        break;
                    }
                }
                else
                {
                    var foundFingerprintedEndpoint = false;
                    for (var j = 0; j < endpoints.Length; j++)
                    {
                        var endpoint = endpoints[j];
                        if (HasFingerprint(endpoint))
                        {
                            foundFingerprintedEndpoint = true;
                            var route = asset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);
                            Log.LogMessage(MessageImportance.Low, $"Selected endpoint '{endpoint.Route}' for asset '{asset.Identity}' because it has a fingerprinted route '{route}'.");
                            endpoint.Route = route;
                            resolvedEndpoints.Add(endpoint);
                            break;
                        }
                    }

                    if (!foundFingerprintedEndpoint)
                    {
                        // By definition there's always at least one endpoint for an asset, so we can safely
                        // assume that the first one is the one that should be used when no fingerprinted
                        // endpoint is found.
                        endpoints[0].Route = asset.ReplaceTokens(endpoints[0].Route, StaticWebAssetTokenResolver.Instance);
                        Log.LogMessage(MessageImportance.Low, $"Selected endpoint '{endpoints[0].Route}' for asset '{asset.Identity}' because no fingerprinted endpoint was found.");
                        resolvedEndpoints.Add(endpoints[0]);
                    }
                }
            }
        }

        ResolvedEndpoints = resolvedEndpoints.Select(e => e.ToTaskItem()).ToArray();

        return !Log.HasLoggedErrors;
    }

    private bool HasFingerprint(StaticWebAssetEndpoint endpoint)
    {
        for (var i = 0; i < endpoint.EndpointProperties.Length; i++)
        {
            var property = endpoint.EndpointProperties[i];
            if (string.Equals(property.Name, "fingerprint", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesAssetPath(StaticWebAsset asset, string assetPath, StaticWebAssetEndpoint endpoint)
    {
        var route = asset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);
        if (string.Equals(route, assetPath, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogMessage(MessageImportance.Low, $"Selected endpoint '{endpoint.Route}' for asset '{asset.Identity}' because '{assetPath}' matches resolved route '{route}'.");
            return true;
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, $"Skipping endpoint '{endpoint.Route}' for asset '{asset.Identity}' because '{assetPath}' does not match resolved route '{route}'.");
            return false;
        }
    }
}
