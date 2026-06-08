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

    [Fact]
    public void Execute_MaterializesFrameworkAssetsFromP2PReferences()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var intermediateDir = Path.Combine(AppContext.BaseDirectory, "obj", "fxtest");
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "fxsource");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "framework.js");
        File.WriteAllText(sourceFile, "// framework");

        var asset = new TaskItem(
            sourceFile,
            new Dictionary<string, string>
            {
                ["RelativePath"] = "framework.js",
                ["BasePath"] = "_content/SourceLib",
                ["AssetMode"] = "All",
                ["AssetKind"] = "Build",
                ["SourceId"] = "SourceLib",
                ["CopyToOutputDirectory"] = "PreserveNewest",
                ["RelatedAsset"] = "",
                ["ContentRoot"] = sourceDir + Path.DirectorySeparatorChar,
                ["SourceType"] = "Framework",
                ["AssetRole"] = "Primary",
                ["AssetTraitValue"] = "",
                ["AssetTraitName"] = "",
                ["OriginalItemSpec"] = sourceFile,
                ["CopyToPublishDirectory"] = "PreserveNewest"
            });

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = new[] { asset },
            Endpoints = [],
            BuildEngine = buildEngine.Object,
            IntermediateOutputPath = intermediateDir,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/ConsumerApp"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        var materialized = task.UpdatedAssets[0];
        materialized.GetMetadata("SourceType").Should().Be("Discovered");
        materialized.GetMetadata("SourceId").Should().Be("ConsumerApp");
        materialized.GetMetadata("BasePath").Should().Be("_content/ConsumerApp");
        materialized.GetMetadata("AssetMode").Should().Be("CurrentProject");
        materialized.ItemSpec.Should().Contain(Path.Combine("fx", "SourceLib"));
        File.Exists(materialized.ItemSpec).Should().BeTrue();

        task.OriginalFrameworkAssets.Should().HaveCount(1);
        task.OriginalFrameworkAssets[0].GetMetadata("SourceType").Should().Be("Framework");
    }

    [Fact]
    public void Execute_RemapsEndpointRoutesForMaterializedFrameworkAssets()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var intermediateDir = Path.Combine(AppContext.BaseDirectory, "obj", "fxroute");
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "fxroutesource");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "framework.js");
        File.WriteAllText(sourceFile, "// framework route test");

        var asset = new TaskItem(
            sourceFile,
            new Dictionary<string, string>
            {
                ["RelativePath"] = "js/framework.js",
                ["BasePath"] = "_content/SourceLib",
                ["AssetMode"] = "All",
                ["AssetKind"] = "Build",
                ["SourceId"] = "SourceLib",
                ["CopyToOutputDirectory"] = "PreserveNewest",
                ["RelatedAsset"] = "",
                ["ContentRoot"] = sourceDir + Path.DirectorySeparatorChar,
                ["SourceType"] = "Framework",
                ["AssetRole"] = "Primary",
                ["AssetTraitValue"] = "",
                ["AssetTraitName"] = "",
                ["OriginalItemSpec"] = sourceFile,
                ["CopyToPublishDirectory"] = "PreserveNewest"
            });

        // Endpoint with the old base path baked into the route.
        var endpoint = new TaskItem(
            "_content/SourceLib/js/framework.js",
            new Dictionary<string, string>
            {
                ["Route"] = "_content/SourceLib/js/framework.js",
                ["AssetFile"] = sourceFile,
                ["Selectors"] = "[]",
                ["ResponseHeaders"] = "[]",
                ["EndpointProperties"] = """[{"Name":"label","Value":"_content/SourceLib/js/framework.js"}]"""
            });

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = [asset],
            Endpoints = [endpoint],
            BuildEngine = buildEngine.Object,
            IntermediateOutputPath = intermediateDir,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "/"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedEndpoints.Should().HaveCount(1);
        var updatedEndpoint = task.UpdatedEndpoints[0];

        // Route should have old base path stripped and new base path applied.
        // "/" base path means just the relative path remains.
        updatedEndpoint.ItemSpec.Should().Be("js/framework.js",
            "endpoint route should have old base path '_content/SourceLib' stripped");

        // AssetFile should point to the materialized path.
        updatedEndpoint.GetMetadata("AssetFile").Should().Contain(Path.Combine("fx", "SourceLib"));

        // Label endpoint property should also be remapped.
        var endpointProperties = updatedEndpoint.GetMetadata("EndpointProperties");
        endpointProperties.Should().Contain("js/framework.js");
        endpointProperties.Should().NotContain("_content/SourceLib");
    }

    [Fact]
    public void Execute_RemapsEndpointRoutesToConsumerBasePath()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var intermediateDir = Path.Combine(AppContext.BaseDirectory, "obj", "fxroutelib");
        var sourceDir = Path.Combine(AppContext.BaseDirectory, "fxroutelibsource");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "lib.js");
        File.WriteAllText(sourceFile, "// lib route test");

        var asset = new TaskItem(
            sourceFile,
            new Dictionary<string, string>
            {
                ["RelativePath"] = "js/lib.js",
                ["BasePath"] = "_content/SourceLib",
                ["AssetMode"] = "All",
                ["AssetKind"] = "Build",
                ["SourceId"] = "SourceLib",
                ["CopyToOutputDirectory"] = "PreserveNewest",
                ["RelatedAsset"] = "",
                ["ContentRoot"] = sourceDir + Path.DirectorySeparatorChar,
                ["SourceType"] = "Framework",
                ["AssetRole"] = "Primary",
                ["AssetTraitValue"] = "",
                ["AssetTraitName"] = "",
                ["OriginalItemSpec"] = sourceFile,
                ["CopyToPublishDirectory"] = "PreserveNewest"
            });

        var endpoint = new TaskItem(
            "_content/SourceLib/js/lib.js",
            new Dictionary<string, string>
            {
                ["Route"] = "_content/SourceLib/js/lib.js",
                ["AssetFile"] = sourceFile,
                ["Selectors"] = "[]",
                ["ResponseHeaders"] = "[]",
                ["EndpointProperties"] = """[{"Name":"label","Value":"_content/SourceLib/js/lib.js"}]"""
            });

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = [asset],
            Endpoints = [endpoint],
            BuildEngine = buildEngine.Object,
            IntermediateOutputPath = intermediateDir,
            ProjectPackageId = "ConsumerLib",
            ProjectBasePath = "_content/ConsumerLib"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedEndpoints.Should().HaveCount(1);
        var updatedEndpoint = task.UpdatedEndpoints[0];

        // Route should have old base path replaced with consumer's base path.
        updatedEndpoint.ItemSpec.Should().Be("_content/ConsumerLib/js/lib.js",
            "endpoint route should use consumer's base path");

        // Label should also reflect the new base path.
        var endpointProperties = updatedEndpoint.GetMetadata("EndpointProperties");
        endpointProperties.Should().Contain("_content/ConsumerLib/js/lib.js");
        endpointProperties.Should().NotContain("_content/SourceLib");
    }

    [Fact]
    public void Execute_PassesThroughNonFrameworkAssetsUnchanged()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var sourceDir = Path.Combine(AppContext.BaseDirectory, "normalsource");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "app.js");
        File.WriteAllText(sourceFile, "// app");

        var asset = new TaskItem(
            sourceFile,
            new Dictionary<string, string>
            {
                ["RelativePath"] = "app.js",
                ["BasePath"] = "",
                ["AssetMode"] = "All",
                ["AssetKind"] = "Build",
                ["SourceId"] = "OtherLib",
                ["CopyToOutputDirectory"] = "PreserveNewest",
                ["RelatedAsset"] = "",
                ["ContentRoot"] = sourceDir + Path.DirectorySeparatorChar,
                ["SourceType"] = "Discovered",
                ["AssetRole"] = "Primary",
                ["AssetTraitValue"] = "",
                ["AssetTraitName"] = "",
                ["OriginalItemSpec"] = sourceFile,
                ["CopyToPublishDirectory"] = "PreserveNewest"
            });

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            Assets = new[] { asset },
            Endpoints = [],
            BuildEngine = buildEngine.Object,
            IntermediateOutputPath = Path.Combine(AppContext.BaseDirectory, "obj", "normal"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/ConsumerApp"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        task.UpdatedAssets[0].GetMetadata("SourceType").Should().Be("Discovered");
        task.UpdatedAssets[0].GetMetadata("SourceId").Should().Be("OtherLib");
        task.OriginalFrameworkAssets.Should().BeEmpty();
    }
}
