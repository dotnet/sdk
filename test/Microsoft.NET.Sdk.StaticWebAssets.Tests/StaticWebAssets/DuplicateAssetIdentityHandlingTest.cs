// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

/// <summary>
/// Tests that tasks handle duplicate asset identities gracefully (first occurrence wins)
/// instead of throwing ArgumentException from Dictionary operations.
/// Regression tests for https://github.com/dotnet/sdk/issues/52089
/// </summary>
public class DuplicateAssetIdentityHandlingTest
{
    [Fact]
    public void ToAssetDictionary_WithDuplicateIdentities_KeepsFirstOccurrence()
    {
        // Arrange — two assets with the same Identity but different SourceId
        var firstAsset = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");
        var secondAsset = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject2");

        var taskItems = new ITaskItem[] { firstAsset.ToTaskItem(), secondAsset.ToTaskItem() };

        // Act — should not throw ArgumentException
        var dictionary = StaticWebAsset.ToAssetDictionary(taskItems);

        // Assert — first occurrence wins
        dictionary.Should().ContainSingle();
        dictionary.Values.Single().SourceId.Should().Be("WasmProject1");
    }

    [Fact]
    public void ToAssetDictionary_WithUniqueIdentities_ReturnsAll()
    {
        // Arrange
        var asset1 = CreateAsset("wwwroot/app.js", sourceId: "Project1");
        var asset2 = CreateAsset("wwwroot/site.css", sourceId: "Project2");

        var taskItems = new ITaskItem[] { asset1.ToTaskItem(), asset2.ToTaskItem() };

        // Act
        var dictionary = StaticWebAsset.ToAssetDictionary(taskItems);

        // Assert
        dictionary.Should().HaveCount(2);
    }

    [Fact]
    public void DiscoverPrecompressedAssets_WithIdenticalDuplicates_LogsLowMessage()
    {
        // Arrange — truly identical duplicates (same SourceId) → Low-priority message
        var messages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

        var asset1 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");
        var asset2 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");

        var task = new DiscoverPrecompressedAssets
        {
            CandidateAssets = [asset1.ToTaskItem(), asset2.ToTaskItem()],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert — task succeeds, logs at low importance
        result.Should().BeTrue();
        messages.Should().Contain(m => m.Contains("Assets are identical"));
    }

    [Fact]
    public void DiscoverPrecompressedAssets_WithMismatchedDuplicates_LogsWarning()
    {
        // Arrange — duplicates with different SourceId → Warning
        var warnings = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(args => warnings.Add(args.Message));

        var asset1 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");
        var asset2 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject2");

        var task = new DiscoverPrecompressedAssets
        {
            CandidateAssets = [asset1.ToTaskItem(), asset2.ToTaskItem()],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert — task succeeds but emits a warning about mismatched metadata
        result.Should().BeTrue();
        warnings.Should().Contain(m => m.Contains("differing metadata"));
    }

    [Fact]
    public void DiscoverPrecompressedAssets_WithDuplicateCandidates_StillFindsCompressedPairs()
    {
        // Arrange — duplicate uncompressed + one compressed
        var buildEngine = new Mock<IBuildEngine>();

        var uncompressed1 = CreateAsset("wwwroot/site.js", sourceId: "Project1",
            relativePath: "site#[.{fingerprint}]?.js");
        var uncompressed2 = CreateAsset("wwwroot/site.js", sourceId: "Project1",
            relativePath: "site#[.{fingerprint}]?.js");
        var compressed = CreateAsset("wwwroot/site.js.gz", sourceId: "Project1",
            relativePath: "site.js#[.{fingerprint}]?.gz");

        var task = new DiscoverPrecompressedAssets
        {
            CandidateAssets = [
                uncompressed1.ToTaskItem(),
                uncompressed2.ToTaskItem(),
                compressed.ToTaskItem()
            ],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert — task succeeds and compressed asset is discovered
        result.Should().BeTrue();
        task.DiscoveredCompressedAssets.Should().ContainSingle();
    }

    [Fact]
    public void GenerateStaticWebAssetsManifest_PublishMode_WithDuplicateIdentities_DoesNotThrow()
    {
        // Arrange — the Publish path in FilterPublishEndpointsIfNeeded builds an identity dictionary
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var tempPath = Path.Combine(
            AppContext.BaseDirectory,
            Guid.NewGuid().ToString("N") + ".json");

        var asset1 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");
        var asset2 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject2");
        var endpoint = CreateEndpoint(asset1);

        var task = new GenerateStaticWebAssetsManifest
        {
            BuildEngine = buildEngine.Object,
            Assets = [asset1.ToTaskItem(), asset2.ToTaskItem()],
            Endpoints = [endpoint.ToTaskItem()],
            ReferencedProjectsConfigurations = [],
            DiscoveryPatterns = [],
            BasePath = "/",
            Source = "TestProject",
            ManifestType = "Publish",
            Mode = "Default",
            ManifestPath = tempPath,
        };

        // Act — should not throw ArgumentException
        var result = task.Execute();

        // Assert — task completes (may still report "conflicting assets" but no crash)
        // The key assertion is that we don't get an unhandled ArgumentException.
        // Whether result is true/false depends on downstream validation, not on our fix.
        errorMessages.Should().NotContain(m => m.Contains("An item with the same key has already been added"));
    }

    [Fact]
    public void GenerateStaticWebAssetEndpointsManifest_WithDuplicateIdentities_DoesNotThrow()
    {
        // Arrange
        var buildEngine = new Mock<IBuildEngine>();

        var tempPath = Path.Combine(
            AppContext.BaseDirectory,
            Guid.NewGuid().ToString("N") + "endpoints.json");

        var asset1 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject1");
        var asset2 = CreateAsset("wwwroot/dotnet.js.map", sourceId: "WasmProject2");
        var endpoint = CreateEndpoint(asset1);

        var task = new GenerateStaticWebAssetEndpointsManifest
        {
            Assets = [asset1.ToTaskItem(), asset2.ToTaskItem()],
            Endpoints = [endpoint.ToTaskItem()],
            ManifestType = "Build",
            Source = "TestProject",
            ManifestPath = tempPath,
            BuildEngine = buildEngine.Object
        };

        // Act — should not throw ArgumentException
        var act = () => task.Execute();

        // Assert — task completes without ArgumentException crash.
        // The return value may be false due to downstream validation (e.g., conflicting assets),
        // but the critical thing is no unhandled dictionary exception.
        act.Should().NotThrow<ArgumentException>();
    }

    private static StaticWebAssetEndpoint CreateEndpoint(StaticWebAsset asset)
    {
        return new StaticWebAssetEndpoint
        {
            Route = asset.ComputeTargetPath("", '/'),
            AssetFile = asset.Identity,
            Selectors = [],
            EndpointProperties = [],
            ResponseHeaders =
            [
                new() { Name = "Content-Type", Value = "application/javascript" },
                new() { Name = "Content-Length", Value = "10" },
                new() { Name = "ETag", Value = "\"integrity\"" },
                new() { Name = "Last-Modified", Value = "Sat, 01 Jan 2000 00:00:01 GMT" }
            ]
        };
    }

    private static StaticWebAsset CreateAsset(
        string itemSpec,
        string sourceId = "TestProject",
        string sourceType = "Discovered",
        string relativePath = null,
        string assetKind = "All",
        string assetMode = "All",
        string basePath = "base",
        string assetRole = "Primary",
        string relatedAsset = "",
        string assetTraitName = "",
        string assetTraitValue = "",
        string copyToOutputDirectory = "Never",
        string copyToPublishDirectory = "PreserveNewest")
    {
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = basePath,
            RelativePath = relativePath ?? itemSpec,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = assetRole,
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            CopyToOutputDirectory = copyToOutputDirectory,
            CopyToPublishDirectory = copyToPublishDirectory,
            OriginalItemSpec = itemSpec,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = new DateTime(2000, 1, 1, 0, 0, 1)
        };

        result.ApplyDefaults();
        result.Normalize();

        return result;
    }
}
