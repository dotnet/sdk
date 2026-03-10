// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

// Shared group-filtering logic used by ReadPackageAssetsManifest,
// UpdatePackageStaticWebAssets, UpdateExternallyDefinedStaticWebAssets,
// and FilterDeferredStaticWebAssetGroups.
internal static class StaticWebAssetGroupFilter
{
    // Returns true if the asset's group requirements are satisfied by the declared groups.
    // During eager filtering, deferred groups are skipped (treated as provisionally satisfied).
    public static bool IsAssetIncludedByGroups(string assetGroups, string sourceId, ITaskItem[] staticWebAssetGroups)
    {
        if (string.IsNullOrEmpty(assetGroups))
        {
            return true;
        }

        var requirements = assetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var requirement in requirements)
        {
            var eqIdx = requirement.IndexOf('=');
            if (eqIdx < 0)
            {
                continue;
            }

            var reqName = requirement.Substring(0, eqIdx);
            var reqValue = requirement.Substring(eqIdx + 1);

            if (IsDeferredGroup(reqName, sourceId, staticWebAssetGroups))
            {
                continue;
            }

            if (!IsGroupRequirementSatisfied(reqName, reqValue, sourceId, staticWebAssetGroups))
            {
                return false;
            }
        }

        return true;
    }

    // Returns true if the asset should be excluded by deferred group evaluation.
    // Only evaluates requirements whose group name is in deferredGroupNames.
    public static bool IsExcludedByDeferredGroups(string assetGroups, string sourceId, HashSet<string> deferredGroupNames, ITaskItem[] staticWebAssetGroups)
    {
        if (string.IsNullOrEmpty(assetGroups))
        {
            return false;
        }

        var requirements = assetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var requirement in requirements)
        {
            var eqIdx = requirement.IndexOf('=');
            if (eqIdx < 0)
            {
                continue;
            }

            var reqName = requirement.Substring(0, eqIdx);
            var reqValue = requirement.Substring(eqIdx + 1);

            if (!deferredGroupNames.Contains(reqName))
            {
                continue;
            }

            if (!IsGroupRequirementSatisfied(reqName, reqValue, sourceId, staticWebAssetGroups))
            {
                return true;
            }
        }

        return false;
    }

    // Cascading exclusion: removes related/alternative assets whose primary was excluded.
    // Repeats until no new exclusions are found.
    public static void CascadeExclusions<T>(List<T> items, HashSet<string> excludedAssetFiles, Func<T, string> getIdentity, Func<T, string> getRelatedAsset)
    {
        if (excludedAssetFiles.Count == 0)
        {
            return;
        }

        bool changed;
        do
        {
            changed = false;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var relatedAsset = getRelatedAsset(items[i]);
                if (!string.IsNullOrEmpty(relatedAsset) && excludedAssetFiles.Contains(relatedAsset))
                {
                    var identity = getIdentity(items[i]);
                    if (!string.IsNullOrEmpty(identity))
                    {
                        excludedAssetFiles.Add(identity);
                    }
                    items.RemoveAt(i);
                    changed = true;
                }
            }
        } while (changed);
    }

    private static bool IsGroupRequirementSatisfied(string reqName, string reqValue, string sourceId, ITaskItem[] staticWebAssetGroups)
    {
        if (staticWebAssetGroups == null)
        {
            return false;
        }

        foreach (var group in staticWebAssetGroups)
        {
            var groupSourceId = group.GetMetadata("SourceId");
            if (!string.IsNullOrEmpty(groupSourceId) &&
                !string.Equals(groupSourceId, sourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(group.ItemSpec, reqName, StringComparison.Ordinal) &&
                string.Equals(group.GetMetadata("Value"), reqValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeferredGroup(string groupName, string sourceId, ITaskItem[] staticWebAssetGroups)
    {
        if (staticWebAssetGroups == null)
        {
            return false;
        }

        foreach (var group in staticWebAssetGroups)
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
}
