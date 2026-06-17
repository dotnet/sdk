// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdatePackageStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    public string IntermediateOutputPath { get; set; }

    public string ProjectPackageId { get; set; }

    public string ProjectBasePath { get; set; }

    [Output]
    public ITaskItem[] UpdatedAssets { get; set; }

    [Output]
    public ITaskItem[] OriginalAssets { get; set; }

    [Output]
    public ITaskItem[] RemappedEndpoints { get; set; }

    [Output]
    public ITaskItem[] OriginalFrameworkEndpoints { get; set; }

    public ITaskItem[] Endpoints { get; set; }

    public override bool Execute()
    {
        try
        {
            var originalAssets = new List<ITaskItem>();
            var updatedAssets = new List<ITaskItem>();
            var assetMapping = new Dictionary<string, (string NewIdentity, string OldBasePath)>(OSPath.PathComparer);

            for (var i = 0; i < Assets.Length; i++)
            {
                var candidate = Assets[i];
                var sourceType = candidate.GetMetadata(nameof(StaticWebAsset.SourceType));

                if (StaticWebAsset.SourceTypes.IsPackage(sourceType))
                {
                    originalAssets.Add(candidate);
                    updatedAssets.Add(StaticWebAsset.FromV1TaskItem(candidate).ToTaskItem());
                }
                else if (StaticWebAsset.SourceTypes.IsFramework(sourceType))
                {
                    originalAssets.Add(candidate);
                    var asset = StaticWebAsset.FromV1TaskItem(candidate);
                    var (transformed, oldPath, oldBasePath) = StaticWebAsset.MaterializeFrameworkAsset(
                        asset, IntermediateOutputPath, ProjectPackageId, ProjectBasePath, Log);
                    if (transformed != null)
                    {
                        updatedAssets.Add(transformed.ToTaskItem());
                        assetMapping[oldPath] = (transformed.Identity, oldBasePath);
                    }
                }
            }

            OriginalAssets = [.. originalAssets];
            UpdatedAssets = [.. updatedAssets];

            if (Endpoints != null && assetMapping.Count > 0)
            {
                RemapEndpoints(assetMapping);
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
        }

        return !Log.HasLoggedErrors;
    }

    private void RemapEndpoints(Dictionary<string, (string NewIdentity, string OldBasePath)> assetMapping)
    {
        var remappedEndpoints = new List<StaticWebAssetEndpoint>();
        var originalEndpointItems = new List<ITaskItem>();
        var parsedEndpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);

        var endpointsByRoute = new Dictionary<string, List<(StaticWebAssetEndpoint Parsed, int OriginalIndex)>>(StringComparer.Ordinal);
        for (var i = 0; i < parsedEndpoints.Length; i++)
        {
            var endpoint = parsedEndpoints[i];
            if (!endpointsByRoute.TryGetValue(endpoint.Route, out var group))
            {
                group = [];
                endpointsByRoute[endpoint.Route] = group;
            }
            group.Add((endpoint, i));
        }

        var routeSegments = new List<PathTokenizer.Segment>();
        var basePathSegments = new List<PathTokenizer.Segment>();

        foreach (var kvp in endpointsByRoute)
        {
            var group = kvp.Value;
            var groupNeedsRemapping = false;
            foreach (var (endpoint, _) in group)
            {
                if (!string.IsNullOrEmpty(endpoint.AssetFile) && assetMapping.ContainsKey(endpoint.AssetFile))
                {
                    groupNeedsRemapping = true;
                    break;
                }
            }

            if (groupNeedsRemapping)
            {
                foreach (var (endpoint, originalIndex) in group)
                {
                    // Capture the original endpoint task item for removal.
                    originalEndpointItems.Add(Endpoints[originalIndex]);

                    if (!string.IsNullOrEmpty(endpoint.AssetFile) && assetMapping.TryGetValue(endpoint.AssetFile, out var info))
                    {
                        var oldAssetFile = endpoint.AssetFile;
                        endpoint.AssetFile = info.NewIdentity;

                        StaticWebAssetEndpoint.RemapEndpointRoute(endpoint, info.OldBasePath, ProjectBasePath, routeSegments, basePathSegments);

                        Log.LogMessage(MessageImportance.Low, "Remapped endpoint route from '{0}' to '{1}', AssetFile from '{2}' to '{3}'.",
                            kvp.Key, endpoint.Route, oldAssetFile, info.NewIdentity);
                    }
                    remappedEndpoints.Add(endpoint);
                }
            }
        }

        OriginalFrameworkEndpoints = [.. originalEndpointItems];
        RemappedEndpoints = StaticWebAssetEndpoint.ToTaskItems(remappedEndpoints);
    }

}
