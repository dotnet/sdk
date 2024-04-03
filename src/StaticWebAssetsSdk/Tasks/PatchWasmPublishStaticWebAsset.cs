using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.WebAssembly;

public class PatchWasmPublishStaticWebAsset : Task
{
    [Required]
    public ITaskItem[] StaticWebAssets { get; set; }

    [Output]
    public ITaskItem[] AssetsToRemove { get; set; }

    public override bool Execute()
    {
        var assetDictionary = StaticWebAssets.ToDictionary(a => a.ItemSpec);
        var assetsToRemove = new List<ITaskItem>();

        foreach (ITaskItem asset in StaticWebAssets)
        {
            string assetKey = asset.ItemSpec;
            assetDictionary[assetKey] = asset;
            string relatedAsset = asset.GetMetadata("RelatedAsset");
            if (!string.IsNullOrEmpty(relatedAsset) && !assetDictionary.ContainsKey(relatedAsset))
            {
                Log.LogMessage($"Asset '{asset.ItemSpec}' has a missing related asset '{relatedAsset}'.");
                assetsToRemove.Add(asset);
            }
        }

        AssetsToRemove = [.. assetsToRemove];

        return true;
    }
}
