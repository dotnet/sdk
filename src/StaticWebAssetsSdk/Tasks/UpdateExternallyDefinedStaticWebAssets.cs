// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
public class UpdateExternallyDefinedStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] FingerprintInferenceExpressions { get; set; }

    [Output]
    public ITaskItem[] UpdatedAssets { get; set; }

    [Output]
    public ITaskItem[] UpdatedEndpoints { get; set; }

    [Output]
    public ITaskItem[] AssetsWithoutEndpoints { get; set; }

    public override bool Execute()
    {
        var assets = Assets.Select(StaticWebAsset.FromV1TaskItem).ToArray();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints);
        var endpointByAsset = endpoints
            .GroupBy(e => e.AssetFile, OSPath.PathComparer)
            .ToDictionary(e => e.Key, e => e.ToArray(), OSPath.PathComparer);

        var fingerprintExpressions = CreateFingerprintExpressions(FingerprintInferenceExpressions);

        var assetsWithoutEndpoints = new List<StaticWebAsset>();

        for (var i = 0; i < assets.Length; i++)
        {
            var asset = assets[i];
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

        UpdatedAssets = assets.Select(a => a.ToTaskItem()).ToArray();
        UpdatedEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray();
        AssetsWithoutEndpoints = assetsWithoutEndpoints.Select(a => a.ToTaskItem()).ToArray();

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
