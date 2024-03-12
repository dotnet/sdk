// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeEndpointsForStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] CandidateEndpoints { get; set; }

    public bool OnlyStandaloneEndpoints { get; set; }

    public bool ApplyBasePath { get; set; }

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
                if (OnlyStandaloneEndpoints && !string.Equals(asset.ComputeTargetPath("", '/'), candidateEndpoint.Route, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipping endpoint {candidateEndpoint.Route} does match the asset path {asset.ComputeTargetPath("", '/')}.");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"Adding endpoint {candidateEndpoint.Route} for asset {candidateEndpoint.AssetFile}.");
                    endpoints.Add(candidateEndpoint);
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping endpoint {candidateEndpoint.Route} because the asset {candidateEndpoint.AssetFile} was not found.");
            }
        }

        Endpoints = StaticWebAssetEndpoint.ToTaskItems(endpoints);

        return true;
    }
}
