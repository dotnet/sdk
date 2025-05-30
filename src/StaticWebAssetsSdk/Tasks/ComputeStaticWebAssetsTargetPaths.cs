// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeStaticWebAssetsTargetPaths : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    public string PathPrefix { get; set; }

    public bool UseAlternatePathDirectorySeparator { get; set; }

    public bool AdjustPathsForPack { get; set; }

    [Output]
    public ITaskItem[] AssetsWithTargetPath { get; set; }

    public override bool Execute()
    {
        try
        {
            Log.LogMessage(MessageImportance.Low, "Using path prefix '{0}'", PathPrefix);
            AssetsWithTargetPath = new ITaskItem[Assets.Length];

            for (var i = 0; i < Assets.Length; i++)
            {
                var staticWebAsset = StaticWebAsset.FromTaskItem(Assets[i]);
                var result = staticWebAsset.ToTaskItem();
                var targetPath = staticWebAsset.ComputeTargetPath(
                    PathPrefix,
                    UseAlternatePathDirectorySeparator ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar, StaticWebAssetTokenResolver.Instance);

                if (AdjustPathsForPack && string.IsNullOrEmpty(Path.GetExtension(targetPath)))
                {
                    targetPath = Path.GetDirectoryName(targetPath);
                }

                result.SetMetadata("TargetPath", targetPath);

                AssetsWithTargetPath[i] = result;
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex.Message);
        }

        return !Log.HasLoggedErrors;
    }
}
