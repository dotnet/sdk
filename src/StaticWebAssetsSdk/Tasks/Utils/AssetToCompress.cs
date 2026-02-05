// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

internal static class AssetToCompress
{
    public static bool TryFindInputFilePath(ITaskItem assetToCompress, TaskLoggingHelper log, out string fullPath)
    {
        var relatedAsset = assetToCompress.GetMetadata("RelatedAsset");
        var relatedAssetOriginalItemSpec = assetToCompress.GetMetadata("RelatedAssetOriginalItemSpec");

        var relatedAssetExists = File.Exists(relatedAsset);
        var originalItemSpecExists = File.Exists(relatedAssetOriginalItemSpec);

        // When both paths exist and point to different files, prefer the newer one.
        // This handles incremental builds where the source file (OriginalItemSpec) may be
        // newer than the destination (RelatedAsset), which hasn't been copied yet.
        if (relatedAssetExists && originalItemSpecExists &&
            !string.Equals(relatedAsset, relatedAssetOriginalItemSpec, StringComparison.OrdinalIgnoreCase))
        {
            var relatedAssetTime = File.GetLastWriteTimeUtc(relatedAsset);
            var originalItemSpecTime = File.GetLastWriteTimeUtc(relatedAssetOriginalItemSpec);

            if (originalItemSpecTime > relatedAssetTime)
            {
                log.LogMessage(MessageImportance.Low, "Asset '{0}' using original item spec '{1}' because it is newer than '{2}'.",
                    assetToCompress.ItemSpec,
                    relatedAssetOriginalItemSpec,
                    relatedAsset);
                fullPath = relatedAssetOriginalItemSpec;
                return true;
            }
        }

        // Check RelatedAsset first (the asset's Identity path) as it's more reliable.
        // RelatedAssetOriginalItemSpec may point to a project file (e.g., .esproj) rather than the actual asset.
        if (relatedAssetExists)
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at path '{1}'.",
                assetToCompress.ItemSpec,
                relatedAsset);
            fullPath = relatedAsset;
            return true;
        }

        if (originalItemSpecExists)
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at original item spec '{1}'.",
                assetToCompress.ItemSpec,
                relatedAssetOriginalItemSpec);
            fullPath = relatedAssetOriginalItemSpec;
            return true;
        }

        log.LogError("The asset '{0}' can not be found at any of the searched locations '{1}' and '{2}'.",
            assetToCompress.ItemSpec,
            relatedAsset,
            relatedAssetOriginalItemSpec);
        fullPath = null;
        return false;
    }
}
