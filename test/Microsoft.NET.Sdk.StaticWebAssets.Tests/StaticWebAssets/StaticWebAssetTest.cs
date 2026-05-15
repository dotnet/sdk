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

        var groupSet = new HashSet<string>(StringComparer.Ordinal);
        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason, groupSet);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void ValidateAssetGroup_TwoAssetsFromDifferentProjects_ReturnsFalse()
    {
        var asset1 = CreateAsset("wwwroot/app.js", "app.js", "All", "All", sourceId: "Project1");
        var asset2 = CreateAsset("wwwroot/app.js", "app.js", "All", "All", sourceId: "Project2");
        var group = (asset1, asset2, (IReadOnlyList<StaticWebAsset>)null);

        var groupSet2 = new HashSet<string>(StringComparer.Ordinal);
        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason, groupSet2);

        Assert.False(result);
        Assert.Contains("different projects", reason);
    }

    [Fact]
    public void ValidateAssetGroup_TwoAllAssetsFromSameProject_ReturnsFalse()
    {
        var asset1 = CreateAsset("wwwroot/app.js", "app.js", "All", "All");
        var asset2 = CreateAsset("obj/app.js", "app.js", "All", "All");
        var group = (asset1, asset2, (IReadOnlyList<StaticWebAsset>)null);

        var groupSet3 = new HashSet<string>(StringComparer.Ordinal);
        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason, groupSet3);

        Assert.False(result);
        Assert.Contains("'All' assets", reason);
    }

    [Fact]
    public void ValidateAssetGroup_BuildAndPublishAssetsFromSameProject_ReturnsTrue()
    {
        var buildAsset = CreateAsset("wwwroot/app.js", "app.js", "Build", "All");
        var publishAsset = CreateAsset("obj/app.js", "app.js", "Publish", "All");
        var group = (buildAsset, publishAsset, (IReadOnlyList<StaticWebAsset>)null);

        var groupSet4 = new HashSet<string>(StringComparer.Ordinal);
        var result = StaticWebAsset.ValidateAssetGroup("app.js", group, out var reason, groupSet4);

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
        var groupSet = new HashSet<string>(StringComparer.Ordinal);
        var result = StaticWebAsset.ValidateAssetGroup("MyApp.styles.css", group, out var reason, groupSet);

        Assert.False(result);
        Assert.Contains("'All' assets", reason);
    }

    // SortByRelatedAssetInPlace tests

    [Fact]
    public void SortByRelatedAssetInPlace_EmptyArray_DoesNothing()
    {
        var assets = Array.Empty<StaticWebAsset>();

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Empty(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_SingleElement_DoesNothing()
    {
        var a = CreateChainAsset("A");
        var assets = new[] { a };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Same(a, assets[0]);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_AllRoots_PreservesOrder()
    {
        var a = CreateChainAsset("A");
        var b = CreateChainAsset("B");
        var c = CreateChainAsset("C");
        var assets = new[] { a, b, c };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Same(a, assets[0]);
        Assert.Same(b, assets[1]);
        Assert.Same(c, assets[2]);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_AlreadySorted_Chain()
    {
        // D (root) → C → B → A  (parents before children)
        var d = CreateChainAsset("D");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { d, c, b, a };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_ReversedChain_WorstCase()
    {
        // Chain: A→B→C→D (D is root). Array in child-first order.
        var d = CreateChainAsset("D");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { a, b, c, d };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_LongReversedChain()
    {
        // A→B→C→D→E (E is root), worst order [A, B, C, D, E]
        var e = CreateChainAsset("E");
        var d = CreateChainAsset("D", relatedTo: "E");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { a, b, c, d, e };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_ShuffledChain()
    {
        // A→B→C→D→E (E is root), shuffled order [C, A, D, B, E]
        var e = CreateChainAsset("E");
        var d = CreateChainAsset("D", relatedTo: "E");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { c, a, d, b, e };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_MultipleIndependentChains()
    {
        // Chain 1: X→Y (Y root). Chain 2: P→Q→R (R root).
        var y = CreateChainAsset("Y");
        var x = CreateChainAsset("X", relatedTo: "Y");
        var r = CreateChainAsset("R");
        var q = CreateChainAsset("Q", relatedTo: "R");
        var p = CreateChainAsset("P", relatedTo: "Q");
        // Interleave: [X, P, Q, Y, R]
        var assets = new[] { x, p, q, y, r };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_Diamond_TwoChildrenOneParent()
    {
        // A→C, B→C, C is root. Order: [A, B, C]
        var c = CreateChainAsset("C");
        var a = CreateChainAsset("A", relatedTo: "C");
        var b = CreateChainAsset("B", relatedTo: "C");
        var assets = new[] { a, b, c };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_MixedRootsAndChain()
    {
        // Roots: R1, R2. Chain: A→B→C (C root). Order: [A, R1, B, R2, C]
        var r1 = CreateChainAsset("R1");
        var r2 = CreateChainAsset("R2");
        var c = CreateChainAsset("C");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { a, r1, b, r2, c };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);
        AssertParentsBeforeChildren(assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_OrphanAsset_PlacedAnyway()
    {
        // A→B but B is not in the array. A should still be placed.
        var a = CreateChainAsset("A", relatedTo: "NONEXISTENT");
        var r = CreateChainAsset("R");
        var assets = new[] { a, r };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Equal(2, assets.Length);
        Assert.Contains(a, assets);
        Assert.Contains(r, assets);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_TwoElements_ChildBeforeParent()
    {
        var parent = CreateChainAsset("Parent");
        var child = CreateChainAsset("Child", relatedTo: "Parent");
        var assets = new[] { child, parent };

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Same(parent, assets[0]);
        Assert.Same(child, assets[1]);
    }

    [Fact]
    public void SortByRelatedAssetInPlace_ProducesValidOrder_OnVariousInputs()
    {
        // Verify the in-place sort produces a valid topological ordering
        // for several shuffled inputs.
        var e = CreateChainAsset("E");
        var d = CreateChainAsset("D", relatedTo: "E");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");

        var orderings = new[]
        {
            new[] { a, b, c, d, e },
            new[] { e, d, c, b, a },
            new[] { c, a, e, b, d },
            new[] { b, d, a, e, c },
        };

        foreach (var order in orderings)
        {
            var copy = (StaticWebAsset[])order.Clone();
            StaticWebAsset.SortByRelatedAssetInPlace(copy);
            AssertParentsBeforeChildren(copy);
        }
    }

    [Fact]
    public void SortByRelatedAssetInPlace_PreservesAllElements()
    {
        var e = CreateChainAsset("E");
        var d = CreateChainAsset("D", relatedTo: "E");
        var c = CreateChainAsset("C", relatedTo: "D");
        var b = CreateChainAsset("B", relatedTo: "C");
        var a = CreateChainAsset("A", relatedTo: "B");
        var assets = new[] { a, b, c, d, e };
        var original = new HashSet<StaticWebAsset>(assets);

        StaticWebAsset.SortByRelatedAssetInPlace(assets);

        Assert.Equal(original, new HashSet<StaticWebAsset>(assets));
    }

    // Asserts that for every asset in the array, its RelatedAsset (parent)
    // appears at an earlier index.
    private static void AssertParentsBeforeChildren(StaticWebAsset[] assets)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            if (!string.IsNullOrEmpty(asset.RelatedAsset))
            {
                Assert.True(
                    seen.Contains(asset.RelatedAsset),
                    $"Asset '{Path.GetFileName(asset.Identity)}' appears before its parent '{Path.GetFileName(asset.RelatedAsset)}'");
            }
            seen.Add(asset.Identity);
        }
    }

    private static StaticWebAsset CreateChainAsset(string name, string relatedTo = null)
    {
        var result = new StaticWebAsset
        {
            Identity = Path.GetFullPath(Path.Combine("wwwroot", name)),
            SourceId = "MyProject",
            SourceType = "Computed",
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = "base",
            RelativePath = name,
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = string.IsNullOrEmpty(relatedTo) ? "Primary" : "Related",
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = relatedTo == null ? "" : Path.GetFullPath(Path.Combine("wwwroot", relatedTo)),
            AssetTraitName = string.IsNullOrEmpty(relatedTo) ? "" : "Content-Encoding",
            AssetTraitValue = string.IsNullOrEmpty(relatedTo) ? "" : "gzip",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            OriginalItemSpec = Path.Combine("wwwroot", name),
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            LastWriteTime = new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero),
            FileLength = 10,
        };

        return result;
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
