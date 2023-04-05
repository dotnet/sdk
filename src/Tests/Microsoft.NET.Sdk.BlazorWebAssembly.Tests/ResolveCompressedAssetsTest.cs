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
        Directory.CreateDirectory(Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ResolveCompressedAssetsTest)));
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

        File.Create(ItemSpec);
        File.Create(OriginalItemSpec);

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

        var task = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }

    [Fact]
    public void ResolvesAssetsMatchingIncludePattern()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        File.Create(ItemSpec);
        File.Create(OriginalItemSpec);

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

        var task = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }

    [Fact]
    public void ExcludesAssetsMatchingExcludePattern()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        File.Create(ItemSpec);
        File.Create(OriginalItemSpec);

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

        var task = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(1);
        task.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");
    }

    [Fact]
    public void DeduplicatesAssetsResolvedBothExplicitlyAndFromPattern()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        File.Create(ItemSpec);
        File.Create(OriginalItemSpec);

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

        var task = new ResolveCompressedAssets()
        {
            OutputBasePath = OutputBasePath,
            BuildEngine = buildEngine.Object,
            CandidateAssets = new[] { asset },
            CompressionConfigurations = new[] { gzipCompressionConfiguration, brotliCompressionConfiguration },
            ExplicitAssets = new[] { gzipExplicitAsset, brotliExplicitAsset },
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.AssetsToCompress.Should().HaveCount(2);
        task.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "build-gz")).And.EndWith(".gz");
        task.AssetsToCompress[1].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }

    [Fact]
    public void IgnoresAssetsCompressedInPreviousTaskRun()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        File.Create(ItemSpec);
        File.Create(OriginalItemSpec);

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
        };

        var result2 = task2.Execute();

        result2.Should().BeTrue();
        task2.AssetsToCompress.Should().HaveCount(1);
        task2.AssetsToCompress[0].ItemSpec.Should().StartWith(Path.Combine(OutputBasePath, "compress")).And.EndWith(".br");
    }
}
