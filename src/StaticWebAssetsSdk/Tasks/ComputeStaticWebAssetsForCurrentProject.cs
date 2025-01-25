// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeStaticWebAssetsForCurrentProject : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public string ProjectMode { get; set; }

    [Required]
    public string AssetKind { get; set; }

    [Required]
    public string Source { get; set; }

    [Output]
    public ITaskItem[] StaticWebAssets { get; set; }

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
                if (!selected.IsForReferencedProjectsOnly())
                {
                    resultAssets.Add(selected);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' because it is for referenced projects only.", selected.Identity);
                }
            }

            var result = new List<ITaskItem>(resultAssets.Count);
            foreach (var asset in resultAssets)
            {
                result.Add(asset.ToTaskItem());
            }
            foreach (var asset in Assets)
            {
                if (!StaticWebAsset.HasSourceId(asset, Source))
                {
                    result.Add(asset);
                }
            }

            StaticWebAssets = [.. result];
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
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
}
