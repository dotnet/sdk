// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests;
public class ComputeStaticWebAssetsTargetPathsTest
{
    [Fact]
    public void IncludesFingerprintInFileWhenPreferred()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new ComputeStaticWebAssetsTargetPaths
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint}]!.js", "All", "All", fingerprint: "1234asdf")],
            PathPrefix = "wwwroot",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.AssetsWithTargetPath.Should().ContainSingle();
        var asset = task.AssetsWithTargetPath[0];
        asset.Should().NotBeNull();
        asset.GetMetadata("TargetPath").Should().Be(Path.Combine("wwwroot", "candidate.1234asdf.js"));
    }

    [Fact]
    public void IncludesFingerprintInFileWhenRequired()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new ComputeStaticWebAssetsTargetPaths
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint}].js", "All", "All", fingerprint: "1234asdf")],
            PathPrefix = "wwwroot",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.AssetsWithTargetPath.Should().ContainSingle();
        var asset = task.AssetsWithTargetPath[0];
        asset.Should().NotBeNull();
        asset.GetMetadata("TargetPath").Should().Be(Path.Combine("wwwroot", "candidate.1234asdf.js"));
    }

    [Fact]
    public void DoesNotIncludeFingerprintInFileWhenNotPreferred()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new ComputeStaticWebAssetsTargetPaths
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint}]?.js", "All", "All", fingerprint: "1234asdf")],
            PathPrefix = "wwwroot",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.AssetsWithTargetPath.Should().ContainSingle();
        var asset = task.AssetsWithTargetPath[0];
        asset.Should().NotBeNull();
        asset.GetMetadata("TargetPath").Should().Be(Path.Combine("wwwroot", "candidate.js"));
    }

    private ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string fingerprint = null)
    {
        var result = new StaticWebAsset()
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
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = fingerprint ?? "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }
}
