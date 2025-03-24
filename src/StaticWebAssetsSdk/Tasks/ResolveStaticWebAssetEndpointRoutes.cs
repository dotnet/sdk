// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveStaticWebAssetEndpointRoutes : Task
{
    [Required] public ITaskItem[] Endpoints { get; set; } = [];

    [Required] public ITaskItem[] Assets { get; set; } = [];

    [Output] public ITaskItem[] ResolvedEndpoints { get; set; } = [];

    public override bool Execute()
    {
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);
        var assets = Assets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity, a => a);

        foreach (var endpoint in endpoints)
        {
            if (!assets.TryGetValue(endpoint.AssetFile, out var asset))
            {
                Log.LogError($"The asset file '{endpoint.AssetFile}' for endpoint '{endpoint.Route}' was not found.");
                return false;
            }
            var route = asset.ReplaceTokens(endpoint.Route, StaticWebAssetTokenResolver.Instance);
            endpoint.Route = route;
        }

        ResolvedEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray();

        return !Log.HasLoggedErrors;
    }
}
