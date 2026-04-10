// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class GenerateModuleWorkerAssetsManifestTest
{
    [Fact]
    public void GeneratesRouterModule_ThatCachesAssetsByHash()
    {
        var file = Path.GetTempFileName();
        try
        {
            var task = new GenerateModuleWorkerAssetsManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                Assets =
                [
                    CreateAsset(Path.GetFullPath(Path.Combine("wwwroot", "index.html")), "index.html", "index-hash"),
                    CreateAsset(Path.GetFullPath(Path.Combine("wwwroot", "css", "site.css")), "css/site.css", "css-hash")
                ],
                OutputPath = file
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.CalculatedVersion.Should().NotBeNullOrEmpty();

            var contents = File.ReadAllText(file);
            contents.Should().Contain("export const assetsManifest = ");
            contents.Should().Contain("export const router = ");
            contents.Should().Contain("createAssetCacheKey");
            contents.Should().Contain("blazor-service-worker-assets");
            contents.Should().Contain("\"url\": \"index.html\"");
            contents.Should().Contain("\"url\": \"css/site.css\"");
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public void MapsCompressedAssets_WhenEnabled()
    {
        var file = Path.GetTempFileName();
        var primaryAssetPath = Path.GetFullPath(Path.Combine("wwwroot", "app.js"));

        try
        {
            var task = new GenerateModuleWorkerAssetsManifest
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                Assets =
                [
                    CreateAsset(primaryAssetPath, "app.js", "primary-hash"),
                    CreateAsset(Path.GetFullPath(Path.Combine("wwwroot", "app.js.gz")), "app.js.gz", "gzip-hash", StaticWebAsset.AssetRoles.Alternative, primaryAssetPath, "Content-Encoding", "gzip"),
                    CreateAsset(Path.GetFullPath(Path.Combine("wwwroot", "app.js.br")), "app.js.br", "brotli-hash", StaticWebAsset.AssetRoles.Alternative, primaryAssetPath, "Content-Encoding", "br")
                ],
                OutputPath = file,
                MapCompressedAssets = true,
            };

            var result = task.Execute();

            result.Should().BeTrue();
            var contents = File.ReadAllText(file);
            contents.Should().Contain("\"url\": \"app.js\"");
            contents.Should().Contain("\"resolvedUrl\": \"app.js.br\"");
            contents.Should().Contain("\"contentEncoding\": \"br\"");
            contents.Should().Contain("DecompressionStream");
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private static ITaskItem CreateAsset(
        string itemSpec,
        string assetUrl,
        string integrity,
        string assetRole = null,
        string relatedAsset = null,
        string assetTraitName = null,
        string assetTraitValue = null)
    {
        var item = new TaskItem(itemSpec);
        item.SetMetadata("AssetUrl", assetUrl);
        item.SetMetadata(nameof(StaticWebAsset.Integrity), integrity);
        item.SetMetadata(nameof(StaticWebAsset.AssetRole), assetRole ?? StaticWebAsset.AssetRoles.Primary);
        item.SetMetadata(nameof(StaticWebAsset.OriginalItemSpec), itemSpec);

        if (!string.IsNullOrEmpty(relatedAsset))
        {
            item.SetMetadata(nameof(StaticWebAsset.RelatedAsset), relatedAsset);
        }

        if (!string.IsNullOrEmpty(assetTraitName))
        {
            item.SetMetadata(nameof(StaticWebAsset.AssetTraitName), assetTraitName);
        }

        if (!string.IsNullOrEmpty(assetTraitValue))
        {
            item.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), assetTraitValue);
        }

        return item;
    }
}
