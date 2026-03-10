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
public class UpdateExternallyDefinedStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public ITaskItem[] Endpoints { get; set; }

    public ITaskItem[] FingerprintInferenceExpressions { get; set; }

    public ITaskItem[] StaticWebAssetGroups { get; set; }

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

        // Group filtering: exclude assets whose AssetGroups requirements are not satisfied
        // by the consumer's StaticWebAssetGroup declarations.
        var excludedAssetFiles = new HashSet<string>(OSPath.PathComparer);
        var filteredAssets = new List<StaticWebAsset>(assets.Length);
        foreach (var asset in assets)
        {
            if (!IsAssetIncludedByGroups(asset))
            {
                excludedAssetFiles.Add(asset.Identity);
                Log.LogMessage(MessageImportance.Low,
                    "Excluding project-reference asset '{0}' by group filtering.", asset.Identity);
            }
            else
            {
                filteredAssets.Add(asset);
            }
        }

        // Cascading exclusion: exclude related/alternative assets whose primary was excluded.
        if (excludedAssetFiles.Count > 0)
        {
            bool changed;
            do
            {
                changed = false;
                for (var i = filteredAssets.Count - 1; i >= 0; i--)
                {
                    var asset = filteredAssets[i];
                    if (!string.IsNullOrEmpty(asset.RelatedAsset) && excludedAssetFiles.Contains(asset.RelatedAsset))
                    {
                        excludedAssetFiles.Add(asset.Identity);
                        Log.LogMessage(MessageImportance.Low,
                            "Excluding related asset '{0}' because its primary '{1}' was excluded by group filtering.",
                            asset.Identity, asset.RelatedAsset);
                        filteredAssets.RemoveAt(i);
                        changed = true;
                    }
                }
            } while (changed);
        }

        UpdatedAssets = filteredAssets.Select(a => a.ToTaskItem()).ToArray();

        // Filter endpoints: exclude endpoints whose asset was excluded by group filtering.
        if (excludedAssetFiles.Count > 0)
        {
            var filteredEndpoints = new List<StaticWebAssetEndpoint>(endpoints.Length);
            foreach (var ep in endpoints)
            {
                if (!string.IsNullOrEmpty(ep.AssetFile) && excludedAssetFiles.Contains(ep.AssetFile))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Excluding endpoint '{0}' because its asset file '{1}' was excluded by group filtering.",
                        ep.Route, ep.AssetFile);
                    continue;
                }
                filteredEndpoints.Add(ep);
            }
            UpdatedEndpoints = filteredEndpoints.Select(e => e.ToTaskItem()).ToArray();
        }
        else
        {
            UpdatedEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray();
        }

        AssetsWithoutEndpoints = assetsWithoutEndpoints
            .Where(a => !excludedAssetFiles.Contains(a.Identity))
            .Select(a => a.ToTaskItem()).ToArray();

        return !Log.HasLoggedErrors;
    }

    private bool IsAssetIncludedByGroups(StaticWebAsset asset)
    {
        if (string.IsNullOrEmpty(asset.AssetGroups))
        {
            return true;
        }

        var groupEntries = asset.AssetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var assetGroupDict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in groupEntries)
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex > 0)
            {
                assetGroupDict[entry.Substring(0, eqIndex)] = entry.Substring(eqIndex + 1);
            }
        }

        var sourceId = asset.SourceId;

        foreach (var kvp in assetGroupDict)
        {
            var entryName = kvp.Key;
            var entryValue = kvp.Value;

            // If this requirement matches a deferred group, skip it during eager filtering.
            // The deferred group will be evaluated later by FilterDeferredStaticWebAssetGroups.
            if (IsDeferredGroup(entryName, sourceId))
            {
                continue;
            }

            var satisfied = false;

            if (StaticWebAssetGroups != null)
            {
                foreach (var group in StaticWebAssetGroups)
                {
                    var groupSourceId = group.GetMetadata("SourceId");
                    if (!string.IsNullOrEmpty(groupSourceId) &&
                        !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(group.ItemSpec, entryName, StringComparison.Ordinal) &&
                        string.Equals(group.GetMetadata("Value"), entryValue, StringComparison.Ordinal))
                    {
                        satisfied = true;
                        break;
                    }
                }
            }

            if (!satisfied)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsDeferredGroup(string groupName, string sourceId)
    {
        if (StaticWebAssetGroups == null)
        {
            return false;
        }

        foreach (var group in StaticWebAssetGroups)
        {
            if (!string.Equals(group.ItemSpec, groupName, StringComparison.Ordinal))
            {
                continue;
            }

            var groupSourceId = group.GetMetadata("SourceId");
            if (!string.IsNullOrEmpty(groupSourceId) &&
                !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(group.GetMetadata("Deferred"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
