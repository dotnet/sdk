// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class StaticWebAssetTest
{
    [Fact]
    public void ValidateAssetGroup_SingleAsset_ReturnsTrue()
    {
        var asset = CreateAsset("wwwroot/app.js", "app.js", "All", "All");
        var group = (asset, (StaticWebAsset)null, (IReadOnlyList<StaticWebAsset>)null);

        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void ValidateAssetGroup_TwoAssetsFromDifferentProjects_ReturnsFalse()
    {
        var asset1 = CreateAsset("wwwroot/app.js", "app.js", "All", "All", sourceId: "Project1");
        var asset2 = CreateAsset("wwwroot/app.js", "app.js", "All", "All", sourceId: "Project2");
        var group = (asset1, asset2, (IReadOnlyList<StaticWebAsset>)null);

        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason);

        Assert.False(result);
        Assert.Contains("different projects", reason);
    }

    [Fact]
    public void ValidateAssetGroup_TwoAllAssetsFromSameProject_ReturnsFalse()
    {
        var asset1 = CreateAsset("wwwroot/app.js", "app.js", "All", "All");
        var asset2 = CreateAsset("obj/app.js", "app.js", "All", "All");
        var group = (asset1, asset2, (IReadOnlyList<StaticWebAsset>)null);

        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason);

        Assert.False(result);
        Assert.Contains("'All' assets", reason);
    }

    [Fact]
    public void ValidateAssetGroup_BuildAndPublishAssetsFromSameProject_ReturnsTrue()
    {
        var buildAsset = CreateAsset("wwwroot/app.js", "app.js", "Build", "All");
        var publishAsset = CreateAsset("obj/app.js", "app.js", "Publish", "All");
        var group = (buildAsset, publishAsset, (IReadOnlyList<StaticWebAsset>)null);

        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void ComputeTargetPath_WithoutTokenResolver_KeepsTokensInPath()
    {
        var asset = CreateAsset(
            "wwwroot/MyApp.styles.css",
            "MyApp.styles#[.{fingerprint}]?.css",
            "All",
            "All");
        asset.Fingerprint = "abc123";

        var targetPath = asset.ComputeTargetPath("", '/');

        Assert.Equal("MyApp.styles#[.{fingerprint}]?.css", targetPath);
    }

    [Fact]
    public void ComputeTargetPath_WithTokenResolver_ReplacesOptionalTokens()
    {
        var asset = CreateAsset(
            "wwwroot/MyApp.styles.css",
            "MyApp.styles#[.{fingerprint}]?.css",
            "All",
            "All");
        asset.Fingerprint = "abc123";

        var targetPath = asset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);

        Assert.Equal("MyApp.styles.css", targetPath);
    }

    [Fact]
    public void TwoAssetsWithDifferentPatternsResolveToSameTargetPath_AfterTokenReplacement()
    {
        var discoveredAsset = CreateAsset(
            "wwwroot/MyApp.styles.css",
            "MyApp.styles#[.{fingerprint}]?.css",
            "All",
            "All");
        discoveredAsset.Fingerprint = "abc123";

        var computedAsset = CreateAsset(
            "obj/scopedcss/bundle/MyApp.styles.css",
            "MyApp#[.{fingerprint}]?.styles.css",
            "All",
            "CurrentProject");
        computedAsset.Fingerprint = "xyz789";

        var path1WithTokens = discoveredAsset.ComputeTargetPath("", '/');
        var path2WithTokens = computedAsset.ComputeTargetPath("", '/');

        Assert.NotEqual(path1WithTokens, path2WithTokens);
        Assert.Equal("MyApp.styles#[.{fingerprint}]?.css", path1WithTokens);
        Assert.Equal("MyApp#[.{fingerprint}]?.styles.css", path2WithTokens);

        var path1Resolved = discoveredAsset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);
        var path2Resolved = computedAsset.ComputeTargetPath("", '/', StaticWebAssetTokenResolver.Instance);

        Assert.Equal("MyApp.styles.css", path1Resolved);
        Assert.Equal("MyApp.styles.css", path2Resolved);
        Assert.Equal(path1Resolved, path2Resolved);
    }

    [Fact]
    public void ValidateAssetGroup_DetectsConflict_WhenAssetsHaveDifferentPatterns_ButSameResolvedPath()
    {
        var discoveredAsset = CreateAsset(
            "wwwroot/MyApp.styles.css",
            "MyApp.styles#[.{fingerprint}]?.css",
            "All",
            "All");
        discoveredAsset.Fingerprint = "abc123";

        var computedAsset = CreateAsset(
            "obj/scopedcss/bundle/MyApp.styles.css",
            "MyApp#[.{fingerprint}]?.styles.css",
            "All",
            "CurrentProject");
        computedAsset.Fingerprint = "xyz789";

        var group = (discoveredAsset, computedAsset, (IReadOnlyList<StaticWebAsset>)null);
        var result = StaticWebAsset.ValidateAssetGroup("MyApp.styles.css", group, out var reason);

        Assert.False(result);
        Assert.Contains("'All' assets", reason);
    }

    private static StaticWebAsset CreateAsset(
        string itemSpec,
        string relativePath,
        string assetKind,
        string assetMode,
        string sourceId = "MyProject",
        string sourceType = "Computed")
    {
        var result = new StaticWebAsset
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = "base",
            RelativePath = relativePath,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = "Primary",
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            OriginalItemSpec = itemSpec,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            LastWriteTime = new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero),
            FileLength = 10,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result;
    }
}
