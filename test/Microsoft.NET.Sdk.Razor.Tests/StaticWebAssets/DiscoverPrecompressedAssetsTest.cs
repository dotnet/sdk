// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class DiscoverPrecompressedAssetsTest
{
    public string ItemSpec { get; }

    public string OriginalItemSpec { get; }

    public string OutputBasePath { get; }

    public DiscoverPrecompressedAssetsTest()
    {
        OutputBasePath = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ResolveCompressedAssetsTest));
        ItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
        OriginalItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
    }

    [Fact]
    public void DiscoversPrecompressedAssetsCorrectly()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

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
            Fingerprint = "uncompressed",
            RelatedAsset = string.Empty,
            ContentRoot = Path.Combine(Environment.CurrentDirectory,"wwwroot"),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            Integrity = "uncompressed-integrity",
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
            Fingerprint = "compressed",
            RelatedAsset = string.Empty,
            ContentRoot = Path.Combine(Environment.CurrentDirectory, "wwwroot"),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            Integrity = "compressed-integrity",
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetMergeBehavior = string.Empty,
            AssetTraitValue = string.Empty,
            AssetTraitName = string.Empty,
            OriginalItemSpec = Path.Combine("wwwroot", "js", "site.js.gz"),
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest
        };

        var task = new DiscoverPrecompressedAssets
        {
            CandidateAssets = [uncompressedCandidate.ToTaskItem(), compressedCandidate.ToTaskItem()],
            BuildEngine = buildEngine.Object
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DiscoveredCompressedAssets.Should().ContainSingle();
        var asset = task.DiscoveredCompressedAssets[0];
        asset.ItemSpec.Should().Be(compressedCandidate.Identity);
        asset.GetMetadata("RelatedAsset").Should().Be(uncompressedCandidate.Identity);
        asset.GetMetadata("OriginalItemSpec").Should().Be(uncompressedCandidate.Identity);
        asset.GetMetadata("RelativePath").Should().Be("js/site#[.{fingerprint=uncompressed}]?.js.gz");
        asset.GetMetadata("AssetRole").Should().Be("Alternative");
        asset.GetMetadata("AssetTraitName").Should().Be("Content-Encoding");
        asset.GetMetadata("AssetTraitValue").Should().Be("gzip");
        asset.GetMetadata("Fingerprint").Should().Be("compressed");
        asset.GetMetadata("Integrity").Should().Be("compressed-integrity");
        asset.GetMetadata("CopyToPublishDirectory").Should().Be("PreserveNewest");
        asset.GetMetadata("CopyToOutputDirectory").Should().Be("Never");
        asset.GetMetadata("AssetMergeSource").Should().Be(string.Empty);
        asset.GetMetadata("AssetMergeBehavior").Should().Be(string.Empty);
        asset.GetMetadata("AssetKind").Should().Be("All");
        asset.GetMetadata("AssetMode").Should().Be("All");
        asset.GetMetadata("SourceId").Should().Be("Test");
        asset.GetMetadata("SourceType").Should().Be("Discovered");
        asset.GetMetadata("ContentRoot").Should().Be(Path.Combine(Environment.CurrentDirectory, $"wwwroot{Path.DirectorySeparatorChar}"));
    }
}
