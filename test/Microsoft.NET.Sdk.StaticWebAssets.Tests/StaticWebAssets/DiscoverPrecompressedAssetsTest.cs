// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class DiscoverPrecompressedAssetsTest
{
    public string ItemSpec { get; }

    public string OriginalItemSpec { get; }

    public string OutputBasePath { get; }

    public DiscoverPrecompressedAssetsTest()
    {
        OutputBasePath = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, nameof(ResolveCompressedAssetsTest));
        ItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
        OriginalItemSpec = Path.Combine(OutputBasePath, Guid.NewGuid().ToString("N") + ".tmp");
    }

    [TestMethod]
    [DataRow(StaticWebAsset.AssetModes.All, ".gz", "gzip")]
    [DataRow(StaticWebAsset.AssetModes.All, ".br", "br")]
    [DataRow(StaticWebAsset.AssetModes.Local, ".gz", "gzip")]
    [DataRow(StaticWebAsset.AssetModes.Local, ".br", "br")]
    public void DiscoversPrecompressedAssetsCorrectly(string assetMode, string extension, string encoding)
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
            AssetMode = assetMode,
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
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow
        };

        var compressedCandidate = new StaticWebAsset
        {
            Identity = Path.Combine(Environment.CurrentDirectory, "wwwroot", "js", $"site.js{extension}"),
            RelativePath = $"js/site.js#[.{{fingerprint}}]?{extension}",
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
            OriginalItemSpec = Path.Combine("wwwroot", "js", $"site.js{extension}"),
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow
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
        asset.GetMetadata("OriginalItemSpec").Should().Be(assetMode == StaticWebAsset.AssetModes.Local
            ? Path.Combine("wwwroot", "js", $"site.js{extension}")
            : uncompressedCandidate.Identity);
        asset.GetMetadata("RelativePath").Should().Be($"js/site#[.{{fingerprint=uncompressed}}]?.js{extension}");
        asset.GetMetadata("AssetRole").Should().Be("Alternative");
        asset.GetMetadata("AssetTraitName").Should().Be("Content-Encoding");
        asset.GetMetadata("AssetTraitValue").Should().Be(encoding);
        asset.GetMetadata("Fingerprint").Should().Be("compressed");
        asset.GetMetadata("Integrity").Should().Be("compressed-integrity");
        asset.GetMetadata("CopyToPublishDirectory").Should().Be("PreserveNewest");
        asset.GetMetadata("CopyToOutputDirectory").Should().Be("Never");
        asset.GetMetadata("AssetMergeSource").Should().Be(string.Empty);
        asset.GetMetadata("AssetMergeBehavior").Should().Be(string.Empty);
        asset.GetMetadata("AssetKind").Should().Be("All");
        asset.GetMetadata("AssetMode").Should().Be(assetMode);
        asset.GetMetadata("SourceId").Should().Be("Test");
        asset.GetMetadata("SourceType").Should().Be("Discovered");
        asset.GetMetadata("ContentRoot").Should().Be(Path.Combine(Environment.CurrentDirectory, $"wwwroot{Path.DirectorySeparatorChar}"));
    }
}
