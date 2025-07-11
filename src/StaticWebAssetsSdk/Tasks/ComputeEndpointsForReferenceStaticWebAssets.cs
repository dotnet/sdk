// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeEndpointsForReferenceStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        var assets = StaticWebAsset.ToAssetDictionary(Assets);

        var result = CandidateEndpoints;

        // Reusable list for optimized endpoint property parsing
        var endpointPropertiesList = new List<StaticWebAssetEndpointProperty>(4);

        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        for (var i = 0; i < CandidateEndpoints.Length; i++)
        {
            var candidateEndpoint = StaticWebAssetEndpoint.FromTaskItem(CandidateEndpoints[i]);
            if (assets.TryGetValue(candidateEndpoint.AssetFile, out var asset))
            {
                // We need to adjust the path to include the base path for the asset, since this is going to be used
                // as a reference.
                // Note that the caller is responsible for ensuring that only assets meant for the current project and
                // destined to be used as a reference by other project are passed to this task.

                var oldRoute = candidateEndpoint.Route;
                if (oldRoute.StartsWith(asset.BasePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping endpoint '{0}' because route '{1}' is already updated.", asset.Identity, oldRoute);
                }
                else
                {
                    candidateEndpoint.Route = StaticWebAsset.CombineNormalizedPaths("", asset.BasePath, candidateEndpoint.Route, '/');

                    // Use optimized property parsing to avoid allocations
                    var endpointPropertiesString = CandidateEndpoints[i].GetMetadata(nameof(StaticWebAssetEndpoint.EndpointProperties));
                    StaticWebAssetEndpointProperty.PopulateFromMetadataValue(endpointPropertiesString, endpointPropertiesList);

                    // Modify label properties in the reusable list
                    var propertiesModified = false;
                    for (var j = 0; j < endpointPropertiesList.Count; j++)
                    {
                        var property = endpointPropertiesList[j];
                        if (string.Equals(property.Name, "label", StringComparison.OrdinalIgnoreCase))
                        {
                            property.Value = StaticWebAsset.CombineNormalizedPaths("", asset.BasePath, property.Value, '/');
                            endpointPropertiesList[j] = property;
                            propertiesModified = true;
                        }
                    }

                    if (propertiesModified)
                    {
                        // Serialize modified properties back using optimized method
                        candidateEndpoint.SetEndpointPropertiesString(
                            StaticWebAssetEndpointProperty.ToMetadataValue(endpointPropertiesList, context));
                    }

                    Log.LogMessage(MessageImportance.Low, "Adding endpoint {0} for asset {1} with updated route {2}.", candidateEndpoint.Route, candidateEndpoint.AssetFile, candidateEndpoint.Route);

                    result[i] = candidateEndpoint.ToTaskItem();
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Skipping endpoint {0} because the asset {1} was not found.", candidateEndpoint.Route, candidateEndpoint.AssetFile);
                result[i] = null;
            }
        }

        Endpoints = result;

        return true;
    }
}
