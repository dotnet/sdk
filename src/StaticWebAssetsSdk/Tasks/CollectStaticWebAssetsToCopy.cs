// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class CollectStaticWebAssetsToCopy : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    [Required]
    public string OutputPath { get; set; }

    [Output]
    public ITaskItem[] AssetsToCopy { get; set; }

    public override bool Execute()
    {
        var copyToOutputFolder = new List<ITaskItem>();
        var normalizedOutputPath = StaticWebAsset.NormalizeContentRootPath(Path.GetFullPath(OutputPath));
        try
        {
            foreach (var asset in StaticWebAsset.FromTaskItemGroup(Assets))
            {
                string fileOutputPath = null;
                if (!(asset.IsDiscovered() || asset.IsComputed()))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since source type is '{1}'", asset.Identity, asset.SourceType);
                    continue;
                }

                if (asset.IsForReferencedProjectsOnly())
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since asset mode is '{1}'", asset.Identity, asset.AssetMode);
                }

                if (asset.ShouldCopyToOutputDirectory())
                {
                    // We have an asset we want to copy to the output folder.
                    fileOutputPath = Path.Combine(normalizedOutputPath, asset.ComputeTargetPath("", Path.DirectorySeparatorChar, StaticWebAssetTokenResolver.Instance));

                    copyToOutputFolder.Add(new TaskItem(asset.Identity, new Dictionary<string, string>
                    {
                        ["OriginalItemSpec"] = asset.Identity,
                        ["TargetPath"] = fileOutputPath,
                        ["CopyToOutputDirectory"] = asset.CopyToOutputDirectory
                    }));
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since copy to output directory option is '{1}'", asset.Identity, asset.CopyToOutputDirectory);
                }
            }

            AssetsToCopy = [.. copyToOutputFolder];
        }
        catch (Exception ex)
        {
            Log.LogError(ex.ToString());
        }

        return !Log.HasLoggedErrors;
    }
}
