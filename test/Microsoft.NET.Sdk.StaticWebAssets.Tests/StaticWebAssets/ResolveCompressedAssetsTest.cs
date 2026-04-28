// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.ContentModel;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class ResolveCompressedAssetsTest
{
    private readonly List<string> _errorMessages;
    private readonly Mock<IBuildEngine> _buildEngine;

    public string ItemSpec { get; }

    public string OriginalItemSpec { get; }

    public string OutputBasePath { get; }

    public ResolveCompressedAssetsTest()
    {
        OutputBasePath = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, nameof(ResolveCompressedAssetsTest));
        ItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
        OriginalItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
        _errorMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
    }

    [Fact]
    public void ResolvesExplicitlyProvidedAssets()
    {
        // Arrange
        var asset = CreatePrimaryAsset();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset },
            Formats = "gzip;brotli",
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void InfersPreCompressedAssetsCorrectly()
    {

        var uncompressedCandidate = new StaticWebAsset
        {
            Identity = Path.Combine(Environment.CurrentDirectory, "wwwroot", "js", "site.js"),
            RelativePath = "js/site#[.{fingerprint}]?.js",
            BasePath = "_content/Test",
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMergeSource = string.Empty,
            SourceId = "Test",
            CopyToOutputDirectory = StaticWebAsset.AssetCopyOptions.Never,
            Fingerprint = "xtxxf3hu2r",
            RelatedAsset = string.Empty,
            ContentRoot = Path.Combine(Environment.CurrentDirectory,"wwwroot"),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            Integrity = "hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo=",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetMergeBehavior = string.Empty,
            AssetTraitValue = string.Empty,
            AssetTraitName = string.Empty,
            OriginalItemSpec = Path.Combine("wwwroot", "js", "site.js"),
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest
        };

        var compressedCandidate = new StaticWebAsset
        {
            Identity = Path.Combine(Environment.CurrentDirectory, "wwwroot", "js", "site.js.gz"),
            RelativePath = "js/site.js#[.{fingerprint}]?.gz",
            BasePath = "_content/Test",
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMergeSource = string.Empty,
            SourceId = "Test",
            CopyToOutputDirectory = StaticWebAsset.AssetCopyOptions.Never,
            Fingerprint = "es13vhk42b",
            RelatedAsset = string.Empty,
            ContentRoot = Path.Combine(Environment.CurrentDirectory, "wwwroot"),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            Integrity = "zs5Fd3XI6+g9f4N1SFLVdgghuiqdvq+nETAjTbvVxx4=",
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetMergeBehavior = string.Empty,
            AssetTraitValue = string.Empty,
            AssetTraitName = string.Empty,
            OriginalItemSpec = Path.Combine("wwwroot", "js", "site.js.gz"),
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow
        };

        var task = new ResolveCompressedAssets
        {
            OutputPath = OutputBasePath,
            CandidateAssets = [uncompressedCandidate.ToTaskItem(), compressedCandidate.ToTaskItem()],
            Formats = "gzip",
            BuildEngine = _buildEngine.Object
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(0);
    }

    [Fact]
    public void ResolvesAssetsMatchingIncludePattern()
    {
        // Arrange
        var asset = CreatePrimaryAsset();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "gzip;brotli",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void ResolvesAssets_WithFingerprint_MatchingIncludePattern()
    {
        // Arrange
        var asset = CreatePrimaryAsset(
            Path.GetFileNameWithoutExtension(ItemSpec) + "#[.{fingerprint}]" + Path.GetExtension(ItemSpec));

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "gzip;brotli",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        var relativePath = task.AssetsToCompress[0].GetMetadata("RelativePath");
        relativePath.Should().EndWith(".gz");
        relativePath = Path.GetFileNameWithoutExtension(relativePath);
        relativePath.Should().EndWith(".tmp");
        relativePath = Path.GetFileNameWithoutExtension(relativePath);
        relativePath.Should().EndWith("#[.{fingerprint=v1}]");
        task.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
        relativePath = task.AssetsToCompress[1].GetMetadata("RelativePath");
        relativePath.Should().EndWith(".br");
        relativePath = Path.GetFileNameWithoutExtension(relativePath);
        relativePath.Should().EndWith(".tmp");
        relativePath = Path.GetFileNameWithoutExtension(relativePath);
        relativePath.Should().EndWith("#[.{fingerprint=v1}]");
    }

    [Fact]
    public void ExcludesAssetsMatchingExcludePattern()
    {
        // Arrange
        var asset = CreatePrimaryAsset();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            IncludePatterns = "**\\*",
            ExcludePatterns = "**\\*.tmp",
            CandidateAssets = new[] { asset },
            Formats = "gzip;brotli"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(0);
    }

    [Fact]
    public void DeduplicatesAssetsResolvedBothExplicitlyAndFromPattern()
    {
        // Arrange
        var asset = CreatePrimaryAsset();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());

        var buildTask = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Formats = "gzip;brotli"
        };

        // Act
        var buildResult = buildTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(2);
        buildTask.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        buildTask.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Theory]
    [InlineData("gzip", ".gz", "brotli", ".br")]
    [InlineData("brotli", ".br", "gzip", ".gz")]
    public void IgnoresAssetsCompressedInPreviousTaskRun(
        string phase1Format, string phase1Ext, string _, string phase2Ext)
    {
        // Arrange
        var asset = CreatePrimaryAsset();

        // Act/Assert
        var task1 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = phase1Format,
        };

        var result1 = task1.Execute();

        result1.Should().BeTrue();
        task1.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(1);
        task1.AssetsToCompress[0].ItemSpec.Should().EndWith(phase1Ext);
        task1.AssetsToCompress[0].SetMetadata("Fingerprint", "v1" + phase1Ext.TrimStart('.'));
        task1.AssetsToCompress[0].SetMetadata("Integrity", "abc" + phase1Ext.TrimStart('.'));

        var explicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        explicitAsset.SetMetadata("Fingerprint", "v2");
        explicitAsset.SetMetadata("Integrity", "def");

        var task2 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { asset, task1.AssetsToCompress[0] },
            IncludePatterns = "**\\*.tmp",
            ExplicitAssets = new[] { explicitAsset },
            Formats = "gzip;brotli"
        };

        var result2 = task2.Execute();

        result2.Should().BeTrue();
        task2.AssetsToCompress.TakeWhile(a => a != null).Should().HaveCount(1);
        task2.AssetsToCompress[0].ItemSpec.Should().EndWith(phase2Ext);
    }

    [Fact]
    public void ProducesDistinctIdentities_ForGroupVariantsWithIdenticalContent()
    {
        // Arrange — two assets that differ only in AssetGroups but have the same
        // SourceId, BasePath, AssetKind, RelativePath (after token stripping) and
        // Fingerprint (identical file content). Before the fix, these would produce
        // the same compressed asset Identity and crash in ToAssetDictionary.
        var v4ItemSpec = Path.Combine(OutputBasePath, "staticwebassets", "V4", "css", "site.css");
        var v5ItemSpec = Path.Combine(OutputBasePath, "staticwebassets", "V5", "css", "site.css");

        var v4Asset = new StaticWebAsset()
        {
            Identity = v4ItemSpec,
            OriginalItemSpec = v4ItemSpec,
            RelativePath = "#[{BootstrapVersion}/]~css/site#[.{fingerprint}]?.css",
            ContentRoot = Path.Combine(OutputBasePath, "staticwebassets"),
            SourceType = StaticWebAsset.SourceTypes.Package,
            SourceId = "Microsoft.AspNetCore.Identity.UI",
            BasePath = "_content/Microsoft.AspNetCore.Identity.UI",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetGroups = "BootstrapVersion=V4",
            Fingerprint = "samehash123",
            Integrity = "sameintegrity",
            FileLength = 42,
            LastWriteTime = DateTime.UtcNow
        }.ToTaskItem();

        var v5Asset = new StaticWebAsset()
        {
            Identity = v5ItemSpec,
            OriginalItemSpec = v5ItemSpec,
            RelativePath = "#[{BootstrapVersion}/]~css/site#[.{fingerprint}]?.css",
            ContentRoot = Path.Combine(OutputBasePath, "staticwebassets"),
            SourceType = StaticWebAsset.SourceTypes.Package,
            SourceId = "Microsoft.AspNetCore.Identity.UI",
            BasePath = "_content/Microsoft.AspNetCore.Identity.UI",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetGroups = "BootstrapVersion=V5",
            Fingerprint = "samehash123",
            Integrity = "sameintegrity",
            FileLength = 42,
            LastWriteTime = DateTime.UtcNow
        }.ToTaskItem();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = _buildEngine.Object,
            CandidateAssets = new[] { v4Asset, v5Asset },
            IncludePatterns = "**/*.css",
            Formats = "gzip",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        var compressed = task.AssetsToCompress.TakeWhile(a => a != null).ToArray();
        compressed.Should().HaveCount(2);
        compressed[0].ItemSpec.Should().EndWith(".gz");
        compressed[1].ItemSpec.Should().EndWith(".gz");

        // The critical assertion: the two compressed assets must have different Identities
        // so they don't collide when added to a dictionary keyed by Identity.
        compressed[0].ItemSpec.Should().NotBe(compressed[1].ItemSpec,
            "group variants with identical content must produce distinct compressed asset identities");
    }

    private ITaskItem CreatePrimaryAsset(string relativePath = null)
    {
        return new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = relativePath ?? Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow
        }.ToTaskItem();
    }
}
