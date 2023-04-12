// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests;

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
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            Format = "gzip",
            Stage = "Build",
        }.ToTaskItem();

        var brotliCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionBrotli",
            Format = "brotli",
            Stage = "Publish",
        }.ToTaskItem();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        gzipExplicitAsset.SetMetadata("ConfigurationName", "BuildCompressionGzip");

        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        brotliExplicitAsset.SetMetadata("ConfigurationName", "BuildCompressionBrotli");

        var buildTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Stage = "Build",
        };

        var publishTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Stage = "Publish",
        };

        // Act
        var buildResult = buildTask.Execute();
        var publishResult = publishTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(1);
        buildTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");

        publishResult.Should().BeTrue();
        publishTask.AssetsToCompress.Should().HaveCount(1);
        publishTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
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
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            IncludePattern = "**\\*.tmp",
            Format = "gzip",
            Stage = "Build",
        }.ToTaskItem();

        var brotliCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionBrotli",
            IncludePattern = "**\\*.tmp",
            Format = "brotli",
            Stage = "Publish",
        }.ToTaskItem();

        var buildTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            Stage = "Build",
        };

        var publishTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            Stage = "Publish",
        };

        // Act
        var buildResult = buildTask.Execute();
        var publishResult = publishTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(1);
        buildTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");

        publishResult.Should().BeTrue();
        publishTask.AssetsToCompress.Should().HaveCount(1);
        publishTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
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
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            IncludePattern = "**\\*.tmp",
            Format = "gzip",
            Stage = "Build",
        }.ToTaskItem();

        var brotliCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionBrotli",
            IncludePattern = "**\\*",
            ExcludePattern = "**\\*.tmp",
            Format = "brotli",
            Stage = "Publish",
        }.ToTaskItem();

        var buildTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            Stage = "Build",
        };

        var publishTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            Stage = "Publish",
        };

        // Act
        var buildResult = buildTask.Execute();
        var publishResult = publishTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(1);
        buildTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");

        publishResult.Should().BeTrue();
        publishTask.AssetsToCompress.Should().BeEmpty();
    }

    [Fact]
    public void ExcludesAssetsMatchingGlobalExcludePattern()
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
        }.ToTaskItem();

        var excludedAssetItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".exclude.tmp");
        var excludedAssetOriginalItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".exclude.tmp");

        var assetToExclude = new StaticWebAsset()
        {
            Identity = excludedAssetItemSpec,
            OriginalItemSpec = excludedAssetOriginalItemSpec,
            RelativePath = Path.GetFileName(excludedAssetItemSpec),
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            IncludePattern = "**\\*.tmp",
            Format = "gzip",
            Stage = "Build",
        }.ToTaskItem();

        var buildTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset, assetToExclude },
            CompressionConfigurations = new[] { gzipCompressionConfiguration },
            GlobalExcludePattern = "**\\*.exclude.tmp",
            Stage = "Build",
        };

        // Act
        var buildResult = buildTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(1);
        buildTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");
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
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            IncludePattern = "**\\*.tmp",
            Format = "gzip",
            Stage = "Build",
        }.ToTaskItem();

        var brotliCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionBrotli",
            IncludePattern = "**\\*.tmp",
            Format = "brotli",
            Stage = "Publish",
        }.ToTaskItem();

        var gzipExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        gzipExplicitAsset.SetMetadata("ConfigurationName", "BuildCompressionGzip");

        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        brotliExplicitAsset.SetMetadata("ConfigurationName", "BuildCompressionBrotli");

        var buildTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Stage = "Build",
        };

        var publishTask = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
            Stage = "Publish",
        };

        // Act
        var buildResult = buildTask.Execute();
        var publishResult = publishTask.Execute();

        // Assert
        buildResult.Should().BeTrue();
        buildTask.AssetsToCompress.Should().HaveCount(1);
        buildTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");

        publishResult.Should().BeTrue();
        publishTask.AssetsToCompress.Should().HaveCount(1);
        publishTask.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }

    [Fact]
    public void IgnoresAssetsCompressedInPreviousTaskRun()
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
        }.ToTaskItem();

        var gzipCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionGzip",
            IncludePattern = "**\\*.tmp",
            Format = "gzip",
            Stage = "All",
        }.ToTaskItem();

        var brotliCompressionConfiguration = new CompressionConfiguration()
        {
            ItemSpec = "BuildCompressionBrotli",
            Format = "brotli",
            Stage = "Publish",
        }.ToTaskItem();

        // Act/Assert
        var task1 = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            Stage = "Build",
        };

        var result1 = task1.Execute();

        result1.Should().BeTrue();
        task1.AssetsToCompress.Should().HaveCount(1);
        task1.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");

        var brotliExplicitAsset = new TaskItem(asset.ItemSpec, asset.CloneCustomMetadata());
        brotliExplicitAsset.SetMetadata("ConfigurationName", "BuildCompressionBrotli");

        var task2 = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset, task1.AssetsToCompress[0] },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { brotliExplicitAsset },
            Stage = "Publish",
        };

        var result2 = task2.Execute();

        result2.Should().BeTrue();
        task2.AssetsToCompress.Should().HaveCount(1);
        task2.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }
}
