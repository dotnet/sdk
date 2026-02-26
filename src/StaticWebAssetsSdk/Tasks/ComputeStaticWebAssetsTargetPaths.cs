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

    public ITaskItem[] StaticWebAssetGroupDefinitions { get; set; }

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

                var effectivePrefix = PathPrefix;
                var groupPrefix = ComputeGroupPathPrefix(Assets[i]);
                if (!string.IsNullOrEmpty(groupPrefix))
                {
                    effectivePrefix = PathPrefix + Path.DirectorySeparatorChar + groupPrefix;
                }

                var targetPath = staticWebAsset.ComputeTargetPath(
                    effectivePrefix,
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

    private string ComputeGroupPathPrefix(ITaskItem asset)
    {
        if (StaticWebAssetGroupDefinitions == null || StaticWebAssetGroupDefinitions.Length == 0)
        {
            return "";
        }

        var assetGroups = asset.GetMetadata("AssetGroups") ?? "";
        if (string.IsNullOrEmpty(assetGroups))
        {
            return "";
        }

        var groupEntries = assetGroups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var groupValues = new List<(int Order, string Value)>();

        foreach (var entry in groupEntries)
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }
            var groupName = entry.Substring(0, eqIndex);
            var groupValue = entry.Substring(eqIndex + 1);

            foreach (var def in StaticWebAssetGroupDefinitions)
            {
                var defName = def.ItemSpec;
                var defValue = def.GetMetadata("Value");
                var includeInPath = def.GetMetadata("IncludeGroupValueInPackagePath");

                if (string.Equals(defName, groupName, StringComparison.Ordinal) &&
                    string.Equals(defValue, groupValue, StringComparison.Ordinal) &&
                    string.Equals(includeInPath, "true", StringComparison.OrdinalIgnoreCase))
                {
                    var orderStr = def.GetMetadata("Order");
                    if (!int.TryParse(orderStr, out var order))
                    {
                        order = 0;
                    }
                    groupValues.Add((order, groupValue));
                    break;
                }
            }
        }

        if (groupValues.Count == 0)
        {
            return "";
        }

        groupValues.Sort((a, b) => a.Order.CompareTo(b.Order));
        return string.Join(Path.DirectorySeparatorChar.ToString(), groupValues.Select(g => g.Value));
    }
}
