// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdateExternallyDefinedStaticWebAssetsTest
{
    [Fact]
    public void Execute_UpdatesAssetsWithoutFingerprint()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "dist", "assets"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "index.html"), "<html><body></body></html>");
        var assets = new ITaskItem [] {
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, @"dist\assets\index-C5tBAdQX.css"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "assets/index-C5tBAdQX.css",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, @"dist\assets\index-C5tBAdQX.css"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                }),
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, @"dist\index.html"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "index.html",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, @"dist\index.html"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                })
        };

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = assets,
            Endpoints = [],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();

        task.UpdatedAssets.Should().HaveCount(2);
        task.AssetsWithoutEndpoints.Should().HaveCount(2);
        task.UpdatedAssets[0].GetMetadata("Fingerprint").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[1].GetMetadata("Fingerprint").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[0].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[1].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_DoesNotAddAssets_WithEndpointsTo_AssetsWithoutEndpoints()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "dist", "assets"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "index.html"), "<html><body></body></html>");
        var assets = new ITaskItem[] {
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "assets/index-C5tBAdQX.css",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                }),
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, "dist", "index.html"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "index.html",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, "dist", "index.html"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                })
        };

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = assets,
            Endpoints = [
                new TaskItem(
                    "index.html",
                    new Dictionary<string, string>
                    {
                        ["Route"] = "/index.html",
                        ["AssetFile"] = Path.Combine(AppContext.BaseDirectory, "dist", "index.html"),
                        ["Selectors"] = "[]",
                        ["ResponseHeaders"] = "[]",
                        ["EndpointProperties"] = "[]"
                    })
                ],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();

        task.UpdatedAssets.Should().HaveCount(2);
        task.AssetsWithoutEndpoints.Should().HaveCount(1);
        task.AssetsWithoutEndpoints[0].ItemSpec.Should().Be(Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"));
        task.UpdatedAssets[0].GetMetadata("Fingerprint").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[1].GetMetadata("Fingerprint").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[0].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[1].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Execute_InfersFingerprint_ForMatchingAssets()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "dist", "assets"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dist", "index.html"), "<html><body></body></html>");
        var assets = new ITaskItem[] {
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "assets/index-C5tBAdQX.css",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                }),
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, "dist", "index.html"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "index.html",
                    ["BasePath"] = "",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Publish",
                    ["SourceId"] = "MyProject",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["RelatedAsset"] = "",
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                    ["SourceType"] = "Discovered",
                    ["AssetRole"] = "Primary",
                    ["AssetTraitValue"] = "",
                    ["AssetTraitName"] = "",
                    ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, "dist", "index.html"),
                    ["CopyToPublishDirectory"] = "PreserveNewest"
                })
        };

        var fingerprintExpressions = new TaskItem[]
        {
            new TaskItem(
                "React",
                new Dictionary<string, string>
                {
                    ["Pattern"] = "assets/.*-(?<fingerprint>.+)\\..*",
                })
        };

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            FingerprintInferenceExpressions = fingerprintExpressions,
            Assets = assets,
            Endpoints = [],
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(2);
        task.UpdatedAssets[0].GetMetadata("Fingerprint").Should().Be("C5tBAdQX");
        task.UpdatedAssets[0].GetMetadata("RelativePath").Should().Be("assets/index-#[{fingerprint}].css");
        task.UpdatedAssets[1].GetMetadata("Fingerprint").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[0].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
        task.UpdatedAssets[1].GetMetadata("Integrity").Should().NotBeNullOrEmpty();
    }
}
