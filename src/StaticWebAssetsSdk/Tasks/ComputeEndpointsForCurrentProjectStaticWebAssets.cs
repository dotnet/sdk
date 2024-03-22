// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeEndpointsForCurrentProjectStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    [Output]
    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        var assets = Assets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity, a => a);
        var candidateEndpoints = StaticWebAssetEndpoint.FromItemGroup(CandidateEndpoints);

        var endpoints = new List<StaticWebAssetEndpoint>();

        foreach (var candidateEndpoint in candidateEndpoints)
        {
            if (assets.TryGetValue(candidateEndpoint.AssetFile, out var asset))
            {
                Log.LogMessage(MessageImportance.Low, "Adding endpoint {0} for asset {1} with route {2}.", candidateEndpoint.Route, candidateEndpoint.AssetFile, candidateEndpoint.Route);

                endpoints.Add(candidateEndpoint);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Skipping endpoint {0} because the asset {1} was not found.", candidateEndpoint.Route, candidateEndpoint.AssetFile);
            }
        }

        Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

        return true;
    }
}
