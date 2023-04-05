// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class AssetToCompress
{
    public static bool TryFindInputFilePath(ITaskItem assetToCompress, TaskLoggingHelper log, out string fullPath)
    {
        var originalAssetPath = assetToCompress.GetMetadata("OriginalAsset");
        if (File.Exists(originalAssetPath))
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at original asset path '{1}'.",
                assetToCompress.ItemSpec,
                originalAssetPath);
            fullPath = originalAssetPath;
            return true;
        }

        var relatedAssetPath = assetToCompress.GetMetadata("RelatedAsset");
        if (File.Exists(relatedAssetPath))
        {
            log.LogMessage(MessageImportance.Low, "Asset '{0}' found at related asset path '{1}'.",
                assetToCompress.ItemSpec,
                relatedAssetPath);
            fullPath = relatedAssetPath;
            return true;
        }

        log.LogError("The asset '{0}' can not be found at any of the searched locations '{1}' and '{2}'.",
            assetToCompress.ItemSpec,
            originalAssetPath,
            relatedAssetPath);
        fullPath = null;
        return false;
    }
}
