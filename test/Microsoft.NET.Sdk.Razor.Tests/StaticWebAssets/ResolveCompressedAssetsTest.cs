// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.ContentModel;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class ResolveCompressedAssetsTest
{
    public string ItemSpec { get; }

    public string OriginalItemSpec { get; }

    public string OutputBasePath { get; }

    public ResolveCompressedAssetsTest()
    {
        OutputBasePath = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ResolveCompressedAssetsTest));
        ItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
        OriginalItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
    }

    [Fact]
    public void ResolvesExplicitlyProvidedAssets()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            Formats = "gzip;brotli",
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void ResolvesAssetsMatchingIncludePattern()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "gzip;brotli",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void ResolvesAssets_WithFingerprint_MatchingIncludePattern()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileNameWithoutExtension(ItemSpec)+"#[.{fingerprint}]" + Path.GetExtension(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "gzip;brotli",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
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
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        var task = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
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
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());

        var buildTask = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Formats = "gzip;brotli"
        };

        // Act
        var buildResult = buildTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(2);
        buildTask.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        buildTask.AssetsToCompress[1].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void IgnoresAssetsCompressedInPreviousTaskRun_Gzip()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        // Act/Assert
        var task1 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "gzip",
        };

        var result1 = task1.Execute();

        result1.Should().BeTrue();
        task1.AssetsToCompress.Should().HaveCount(1);
        task1.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
        task1.AssetsToCompress[0].SetMetadata("Fingerprint", "v1gz");
        task1.AssetsToCompress[0].SetMetadata("Integrity", "abcgz");

        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        brotliExplicitAsset.SetMetadata("Fingerprint", "v2");
        brotliExplicitAsset.SetMetadata("Integrity", "def");

        var task2 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset, task1.AssetsToCompress[0] },
            IncludePatterns = "**\\*.tmp",
            ExplicitAssets = new[] { brotliExplicitAsset },
            Formats = "gzip;brotli"
        };

        var result2 = task2.Execute();

        result2.Should().BeTrue();
        task2.AssetsToCompress.Should().HaveCount(1);
        task2.AssetsToCompress[0].ItemSpec.Should().EndWith(".br");
    }

    [Fact]
    public void IgnoresAssetsCompressedInPreviousTaskRun_Brotli()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = new StaticWebAsset()
        {
            Identity = ItemSpec,
            OriginalItemSpec = OriginalItemSpec,
            RelativePath = Path.GetFileName(ItemSpec),
            ContentRoot = Path.GetDirectoryName(ItemSpec),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "App",
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            Fingerprint = "v1",
            Integrity = "abc"
        }.ToTaskItem();

        // Act/Assert
        var task1 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            IncludePatterns = "**\\*.tmp",
            Formats = "brotli",
        };

        var result1 = task1.Execute();

        result1.Should().BeTrue();
        task1.AssetsToCompress.Should().HaveCount(1);
        task1.AssetsToCompress[0].ItemSpec.Should().EndWith(".br");
        task1.AssetsToCompress[0].SetMetadata("Fingerprint", "v1br");
        task1.AssetsToCompress[0].SetMetadata("Integrity", "abcbr");

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        gzipExplicitAsset.SetMetadata("Fingerprint", "v2");
        gzipExplicitAsset.SetMetadata("Integrity", "def");

        var task2 = new ResolveCompressedAssets()
        {
            OutputPath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset, task1.AssetsToCompress[0] },
            IncludePatterns = "**\\*.tmp",
            ExplicitAssets = new[] { gzipExplicitAsset },
            Formats = "gzip;brotli"
        };

        var result2 = task2.Execute();

        result2.Should().BeTrue();
        task2.AssetsToCompress.Should().HaveCount(1);
        task2.AssetsToCompress[0].ItemSpec.Should().EndWith(".gz");
    }
}
