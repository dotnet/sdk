// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Certain project types integrate with the static web asset protocol. As we evolve it and add new features
// either they have to update their SDKs to support the new features or we need to provide a way to update
// the assets from previous versions to the current version.
// For example, the JavaScript Project Tools SDK integrates with the static web asset protocol for SPA applications
// but it doesn't support integrity or fingerprinting, which causes issues when we reference the project and we try
// to further process the assets.
// Additionally, this task handles incoming framework assets from P2P references by materializing them
// (copying files to an intermediate directory and updating metadata) so they become local to the consuming project.
public class UpdateExternallyDefinedStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] FingerprintInferenceExpressions { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

    public string IntermediateOutputPath { get; set; }

    public string ProjectPackageId { get; set; }

    public string ProjectBasePath { get; set; }

    [Output]
    public ITaskItem[] UpdatedAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    [Output]
    public ITaskItem[] AssetsWithoutEndpoints { get; set; }

    [Output]
    public ITaskItem[] OriginalFrameworkAssets { get; set; }

    public override bool Execute()
    {
        var assets = Assets.Select(StaticWebAsset.FromV1TaskItem).ToArray();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);
        var groupLookup = StaticWebAssetGroup.FromItemGroup(StaticWebAssetGroups);
        var endpointByAsset = endpoints
            .GroupBy(e => e.AssetFile, OSPath.PathComparer)
            .ToDictionary(e => e.Key, e => e.ToArray(), OSPath.PathComparer);

        var fingerprintExpressions = CreateFingerprintExpressions(FingerprintInferenceExpressions);

        // Filter by group FIRST so that framework assets tagged with groups that the consuming
        // project doesn't accept are excluded before materialization.
        var (filteredAssets, excludedAssetFiles) = StaticWebAsset.FilterByGroup(assets, groupLookup, skipDeferred: true);

        // Rebuild the assets array from filtered results for subsequent processing.
        // Also build a set to identify which original input items survived filtering.
        var filteredSet = new HashSet<string>(filteredAssets.Select(a => a.Identity), OSPath.PathComparer);

        var assetsWithoutEndpoints = new List<StaticWebAsset>();
        var originalFrameworkAssetItems = new List<ITaskItem>();
        var assetMapping = new Dictionary<string, (string NewIdentity, string OldBasePath)>(OSPath.PathComparer);

        for (var i = 0; i < filteredAssets.Count; i++)
        {
            var asset = filteredAssets[i];

            // Materialize framework assets from P2P references.
            if (StaticWebAsset.SourceTypes.IsFramework(asset.SourceType))
            {
                // Find the original task item that corresponds to this filtered asset.
                var originalIndex = Array.FindIndex(assets, a => OSPath.PathComparer.Equals(a.Identity, asset.Identity));
                if (originalIndex >= 0)
                {
                    originalFrameworkAssetItems.Add(Assets[originalIndex]);
                }
                var (materialized, oldIdentity, oldBasePath) = StaticWebAsset.MaterializeFrameworkAsset(
                    asset, IntermediateOutputPath, ProjectPackageId, ProjectBasePath, Log);
                if (materialized != null)
                {
                    filteredAssets[i] = materialized;
                    assetMapping[oldIdentity] = (materialized.Identity, oldBasePath);
                }
                continue;
            }

            if (!endpointByAsset.TryGetValue(asset.Identity, out var endpoint))
            {
                Log.LogMessage($"Asset {asset.Identity} does not have an associated endpoint defined.");

                if (TryInferFingerprint(fingerprintExpressions, asset.RelativePath, out var fingerprint, out var newRelativePath))
                {
                    Log.LogMessage($"Inferred fingerprint {fingerprint} for asset {asset.Identity}. Relative path updated to {newRelativePath}.");
                    asset.RelativePath = newRelativePath;
                    asset.Fingerprint = fingerprint;
                }

                assetsWithoutEndpoints.Add(asset);
            }
        }

        // Update RelatedAsset on compressed/alternative assets that reference materialized framework assets.
        if (assetMapping.Count > 0)
        {
            for (var i = 0; i < filteredAssets.Count; i++)
            {
                var asset = filteredAssets[i];
                if (!string.IsNullOrEmpty(asset.RelatedAsset) &&
                    assetMapping.TryGetValue(asset.RelatedAsset, out var mapping))
                {
                    asset.RelatedAsset = mapping.NewIdentity;
                }
            }
        }

        UpdatedAssets = StaticWebAsset.ToTaskItems(filteredAssets);

        // Filter endpoints using the shared helper.
        var endpointGroups = StaticWebAssetEndpointGroup.CreateEndpointGroups(endpoints);
        var (_, survivingEndpoints) = StaticWebAssetEndpointGroup.ComputeFilteredEndpoints(endpointGroups, excludedAssetFiles);

        // Remap endpoints for materialized framework assets — update AssetFile and Route
        // to reflect the new materialized path and the consuming project's base path.
        if (assetMapping.Count > 0)
        {
            var routeSegments = new List<PathTokenizer.Segment>();
            var basePathSegments = new List<PathTokenizer.Segment>();

            foreach (var ep in survivingEndpoints)
            {
                if (assetMapping.TryGetValue(ep.AssetFile, out var info))
                {
                    ep.AssetFile = info.NewIdentity;
                    StaticWebAssetEndpoint.RemapEndpointRoute(ep, info.OldBasePath, ProjectBasePath, routeSegments, basePathSegments);
                }
            }
        }

        UpdatedEndpoints = StaticWebAssetEndpoint.ToTaskItems(survivingEndpoints);

        AssetsWithoutEndpoints = StaticWebAsset.ToTaskItems(
            assetsWithoutEndpoints.Where(a => !excludedAssetFiles.Contains(a.Identity)));

        OriginalFrameworkAssets = [.. originalFrameworkAssetItems];

        return !Log.HasLoggedErrors;
    }

    private bool TryInferFingerprint(Regex[] fingerprintExpressions, string relativePath, out string fingerprint, out string newRelativePath)
    {
        for (var i = 0; i < fingerprintExpressions.Length; i++)
        {
            var regex = fingerprintExpressions[i];
            var match = regex.Match(relativePath);
            if (match.Success)
            {
                var fingerprintGroup = match.Groups["fingerprint"];
                if (fingerprintGroup == null)
                {
                    Log.LogError($"The regular expression {regex} does not contain a 'fingerprint' group. Provide an expression in the form of (?<fingerprint>...).");
                    fingerprint = null;
                    newRelativePath = null;
                    return false;
                }

                fingerprint = fingerprintGroup.Value;
                newRelativePath = relativePath.Replace(fingerprintGroup.Value, "#[{fingerprint}]");
                return true;
            }
        }

        fingerprint = null;
        newRelativePath = null;
        return false;
    }

    private static Regex[] CreateFingerprintExpressions(ITaskItem[] fingerprintInferenceExpressions)
    {
        if (fingerprintInferenceExpressions == null || fingerprintInferenceExpressions.Length == 0)
        {
            return [];
        }

        var regexOptions = (OSPath.PathComparison == StringComparison.OrdinalIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant;

        var result = new Regex[fingerprintInferenceExpressions.Length];
        for (var i = 0; i < fingerprintInferenceExpressions.Length; i++)
        {
            var fingerprintExpression = fingerprintInferenceExpressions[i];
            var pattern = fingerprintExpression.GetMetadata("Pattern");
            var regex = new Regex(pattern, regexOptions);
            result[i] = regex;
        }

        return result;
    }
}
