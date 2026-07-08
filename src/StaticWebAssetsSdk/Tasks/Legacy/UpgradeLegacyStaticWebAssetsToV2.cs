// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpgradeLegacyStaticWebAssetsToV2 : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Output]
    public ITaskItem[] UpgradedAssets { get; set; }

    public override bool Execute()
    {
        if (Assets != null && Assets.Length > 0)
        {
            // Set the upgraded assets
            UpgradedAssets = new ITaskItem[Assets.Length];
            for (var i = 0; i < Assets.Length; i++)
            {
                Log.LogMessage(MessageImportance.Low, $"Upgrading {Assets[i].ItemSpec}");
                var asset = StaticWebAsset.FromV1TaskItem(Assets[i]);
                UpgradedAssets[i] = asset.ToTaskItem();
            }
        }

        return !Log.HasLoggedErrors;
    }
}
