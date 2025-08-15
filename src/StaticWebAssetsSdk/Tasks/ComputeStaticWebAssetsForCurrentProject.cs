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
            var currentProjectAssets = StaticWebAsset.AssetsByTargetPath(Assets, Source, AssetKind);

            var resultAssets = new List<StaticWebAsset>(currentProjectAssets.Count);
            foreach (var kvp in currentProjectAssets)
            {
                var targetPath = kvp.Key;
                var (selected, all) = kvp.Value;
                if (all != null)
                {
                    Log.LogError("More than one compatible asset found for target path '{0}' -> {1}.",
                        targetPath,
                        Environment.NewLine + string.Join(Environment.NewLine, all.Select(a => $"({a.Identity},{a.AssetKind})")));
                    return false;
                }

                if (!selected.IsForReferencedProjectsOnly())
                {
                    resultAssets.Add(selected);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' because it is for referenced projects only.", selected.Identity);
                }
            }

            StaticWebAssets = resultAssets
                .Select(a => a.ToTaskItem())
                .Concat(Assets.Where(asset => !StaticWebAsset.HasSourceId(asset, Source)))
                .ToArray();
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
        }

        return !Log.HasLoggedErrors;
    }
}
