// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeReferenceStaticWebAssetItems : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    public ITaskItem[] Patterns { get; set; }

    [Required]
    public string AssetKind { get; set; }

    [Required]
    public string ProjectMode { get; set; }

    [Required]
    public string Source { get; set; }

    public bool UpdateSourceType { get; set; } = true;

    [Output]
    public ITaskItem[] StaticWebAssets { get; set; }

    [Output]
    public ITaskItem[] DiscoveryPatterns { get; set; }

    public override bool Execute()
    {
        try
        {
            var existingAssets = GroupAssetsByPath();

            var resultAssets = new List<StaticWebAsset>();
            foreach (var kvp in existingAssets)
            {
                var key = kvp.Key;
                var group = kvp.Value;
                switch (group.Count)
                {
                    case 0:
                        Log.LogMessage(MessageImportance.Low, "No compatible asset found for '{0}'", key);
                        continue;
                    case 1:
                        break;
                    default:
                        Log.LogError(@"More than one compatible asset found for '{0}'. Assets:
    {1}", key, string.Join($"{Environment.NewLine}    ", group.Select(a => a.Identity)));
                        return false;
                }

                var selected = group[0];
                if (ShouldIncludeAssetAsReference(selected, out var reason))
                {
                    selected.SourceType = UpdateSourceType ? StaticWebAsset.SourceTypes.Project : selected.SourceType;
                    resultAssets.Add(selected);
                }
                Log.LogMessage(MessageImportance.Low, reason);
            }

            var patterns = new List<StaticWebAssetsDiscoveryPattern>();
            if (Patterns != null)
            {
                foreach (var pattern in Patterns)
                {
                    if (!StaticWebAssetsDiscoveryPattern.HasSourceId(pattern, Source))
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipping pattern '{0}' because is not defined in the current project.", pattern.ItemSpec);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Including pattern '{0}' because is defined in the current project.", pattern.ToString());
                        patterns.Add(StaticWebAssetsDiscoveryPattern.FromTaskItem(pattern));
                    }
                }
            }

            StaticWebAssets = resultAssets.Select(a => a.ToTaskItem()).ToArray();
            DiscoveryPatterns = patterns.Select(p => p.ToTaskItem()).ToArray();
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
        }

        return !Log.HasLoggedErrors;
    }

    private IDictionary<string, List<StaticWebAsset>> GroupAssetsByPath()
    {
        var result = new Dictionary<string, List<StaticWebAsset>>();

        for (var i = 0; i < Assets.Length; i++)
        {
            var candidate = Assets[i];
            if (!StaticWebAsset.HasSourceId(candidate, Source))
            {
                continue;
            }

            var asset = StaticWebAsset.FromTaskItem(candidate);
            var key = asset.ComputeTargetPath("", '/');
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }

            list.Add(asset);
        }

        foreach (var kvp in result)
        {
            StaticWebAsset.ChooseNearestAssetKind(kvp.Value, AssetKind);
        }

        return result;
    }

    private bool ShouldIncludeAssetAsReference(StaticWebAsset candidate, out string reason)
    {
        if (!StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetAsReference(candidate, ProjectMode))
        {
            reason = string.Format(
                CultureInfo.InvariantCulture,
                "Skipping candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
                candidate.Identity,
                ProjectMode,
                candidate.AssetMode);
            return false;
        }

        reason = string.Format(
            CultureInfo.InvariantCulture,
            "Accepted candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
            candidate.Identity,
            ProjectMode,
            candidate.AssetMode);

        return true;
    }
}
