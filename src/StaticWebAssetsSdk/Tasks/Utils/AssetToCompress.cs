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
        if (File.Exists(relatedAsset))
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at path '{1}'.",
                assetToCompress.ItemSpec,
                relatedAsset);
            fullPath = relatedAsset;
            return true;
        }

        log.LogError("The asset '{0}' can not be found at the searched location '{1}'.",
            assetToCompress.ItemSpec,
            relatedAsset);
        fullPath = null;
        return false;
    }
}
