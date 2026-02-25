// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdatePackageStaticWebAssetsTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public UpdatePackageStaticWebAssetsTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UpdatePackageSWA_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _logMessages.Add(args.Message));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Execute_PackageAssets_ArePassedThrough()
    {
        // Arrange
        var sourceFile = CreateTempFile("pkg", "content.js", "console.log('pkg');");
        var asset = CreatePackageAsset(sourceFile, "MyLib", "_content/mylib", "content.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        task.OriginalAssets.Should().HaveCount(1);
        task.UpdatedAssets[0].GetMetadata("SourceType").Should().Be("Package");
    }

    [Fact]
    public void Execute_FrameworkAssets_AreMaterialized()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "js", "framework.js", "console.log('framework');");
        var asset = CreateFrameworkAsset(sourceFile, "FrameworkLib", "_content/frameworklib", "js/framework.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        task.OriginalAssets.Should().HaveCount(1);

        var updated = task.UpdatedAssets[0];

        // The materialized file should exist in the fx directory
        var expectedDir = Path.Combine(intermediateOutput, "fx", "FrameworkLib");
        var expectedPath = Path.GetFullPath(Path.Combine(expectedDir, "js", "framework.js"));
        updated.ItemSpec.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        File.ReadAllText(expectedPath).Should().Be("console.log('framework');");
    }

    [Fact]
    public void Execute_FrameworkAssets_SourceTypeChangedToDiscovered()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FrameworkLib", "_content/frameworklib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        var updated = task.UpdatedAssets[0];
        updated.GetMetadata("SourceType").Should().Be("Discovered");
        updated.GetMetadata("SourceId").Should().Be("ConsumerApp");
        updated.GetMetadata("BasePath").Should().Be("_content/consumerapp");
        updated.GetMetadata("AssetMode").Should().Be("CurrentProject");
    }

    [Fact]
    public void Execute_FrameworkAssets_ContentRootPointsToFxDirectory()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        var updated = task.UpdatedAssets[0];
        var expectedContentRoot = Path.Combine(intermediateOutput, "fx", "FxLib") + Path.DirectorySeparatorChar;
        updated.GetMetadata("ContentRoot").Should().Be(expectedContentRoot);
    }

    [Fact]
    public void Execute_FrameworkAssets_MissingSourceFile_LogsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDir, "does_not_exist.js");
        var asset = CreateFrameworkAsset(nonExistentFile, "FxLib", "_content/fxlib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(e => e.Contains("does not exist") && e.Contains("does_not_exist.js"));
    }

    [Fact]
    public void Execute_MixedAssets_ProcessesBothTypes()
    {
        // Arrange
        var pkgFile = CreateTempFile("pkg", "package.js", "console.log('pkg');");
        var fxFile = CreateTempFile("source", "framework.js", "console.log('fx');");

        var pkgAsset = CreatePackageAsset(pkgFile, "MyLib", "_content/mylib", "package.js");
        var fxAsset = CreateFrameworkAsset(fxFile, "MyLib", "_content/mylib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { pkgAsset, fxAsset },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(2);
        task.OriginalAssets.Should().HaveCount(2);

        // Package asset stays as Package
        task.UpdatedAssets[0].GetMetadata("SourceType").Should().Be("Package");
        // Framework asset is converted to Discovered
        task.UpdatedAssets[1].GetMetadata("SourceType").Should().Be("Discovered");
    }

    [Fact]
    public void Execute_FrameworkAssets_PreservesOriginalFingerprintAndIntegrity()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");

        // Get the fingerprint/integrity that were computed by FromV1TaskItem in CreateFrameworkAsset
        var originalFingerprint = asset.GetMetadata("Fingerprint");
        var originalIntegrity = asset.GetMetadata("Integrity");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        var updated = task.UpdatedAssets[0];
        // Fingerprint and integrity should be preserved from the original file
        updated.GetMetadata("Fingerprint").Should().Be(originalFingerprint);
        updated.GetMetadata("Integrity").Should().Be(originalIntegrity);
    }

    [Fact]
    public void Execute_FrameworkAssets_IncrementalSkipsCopy_WhenUpToDate()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var intermediateOutput = Path.Combine(_tempDir, "obj");
        var fxDir = Path.Combine(intermediateOutput, "fx", "FxLib");
        var destPath = Path.Combine(fxDir, "framework.js");

        // Pre-create the destination so it's already up-to-date
        Directory.CreateDirectory(fxDir);
        File.Copy(sourceFile, destPath);
        // Make the dest file newer than the source
        File.SetLastWriteTimeUtc(destPath, DateTime.UtcNow.AddMinutes(1));

        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        // Should log the "already up to date" message
        _logMessages.Should().Contain(m => m.Contains("already up to date"));
    }

    [Fact]
    public void Execute_FrameworkAssets_OverwritesStaleDestination()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "new content");
        var intermediateOutput = Path.Combine(_tempDir, "obj");
        var fxDir = Path.Combine(intermediateOutput, "fx", "FxLib");
        var destPath = Path.Combine(fxDir, "framework.js");

        // Pre-create the destination with old content and older timestamp
        Directory.CreateDirectory(fxDir);
        File.WriteAllText(destPath, "old content");
        File.SetLastWriteTimeUtc(destPath, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow);

        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        File.ReadAllText(destPath).Should().Be("new content");
        _logMessages.Should().Contain(m => m.Contains("Materialized framework asset"));
    }

    [Fact]
    public void Execute_NoFrameworkAssets_EndpointsNotRemapped()
    {
        // Arrange
        var sourceFile = CreateTempFile("pkg", "content.js", "console.log('pkg');");
        var pkgAsset = CreatePackageAsset(sourceFile, "MyLib", "_content/mylib", "content.js");

        var endpoint = new TaskItem("content.js", new Dictionary<string, string>
        {
            ["Route"] = "/content.js",
            ["AssetFile"] = sourceFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { pkgAsset },
            Endpoints = new ITaskItem[] { endpoint },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        // No framework assets => no remapping done
        task.RemappedEndpoints.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Execute_FrameworkAssets_EndpointsAreRemapped()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var endpoint = new TaskItem("framework.js", new Dictionary<string, string>
        {
            ["Route"] = "/_content/fxlib/framework.js",
            ["AssetFile"] = sourceFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new ITaskItem[] { endpoint },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.RemappedEndpoints.Should().HaveCount(1);

        var remapped = task.RemappedEndpoints[0];
        var expectedPath = Path.GetFullPath(Path.Combine(intermediateOutput, "fx", "FxLib", "framework.js"));
        remapped.GetMetadata("AssetFile").Should().Be(expectedPath);
    }

    [Fact]
    public void Execute_MultipleEndpoints_SameIdentity_AllRemapped()
    {
        // Arrange — two endpoints share the same Identity (e.g. same route, different selectors)
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var endpoint1 = new TaskItem("framework.js", new Dictionary<string, string>
        {
            ["Route"] = "/_content/fxlib/framework.js",
            ["AssetFile"] = sourceFile,
            ["Selectors"] = "[{\"Name\":\"Content-Encoding\",\"Value\":\"gzip\"}]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var endpoint2 = new TaskItem("framework.js", new Dictionary<string, string>
        {
            ["Route"] = "/_content/fxlib/framework.js",
            ["AssetFile"] = sourceFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new ITaskItem[] { endpoint1, endpoint2 },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.RemappedEndpoints.Should().HaveCount(2);

        var expectedPath = Path.GetFullPath(Path.Combine(intermediateOutput, "fx", "FxLib", "framework.js"));
        task.RemappedEndpoints[0].GetMetadata("AssetFile").Should().Be(expectedPath);
        task.RemappedEndpoints[1].GetMetadata("AssetFile").Should().Be(expectedPath);
    }

    [Fact]
    public void Execute_EndpointsNotMatchingFramework_AreNotRemapped()
    {
        // Arrange — endpoint pointing to a file that is NOT a framework asset
        var fxFile = CreateTempFile("source", "framework.js", "fx");
        var pkgFile = CreateTempFile("pkg", "package.js", "pkg");
        var fxAsset = CreateFrameworkAsset(fxFile, "FxLib", "_content/fxlib", "framework.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var fxEndpoint = new TaskItem("framework.js", new Dictionary<string, string>
        {
            ["Route"] = "/_content/fxlib/framework.js",
            ["AssetFile"] = fxFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var pkgEndpoint = new TaskItem("package.js", new Dictionary<string, string>
        {
            ["Route"] = "/_content/fxlib/package.js",
            ["AssetFile"] = pkgFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { fxAsset },
            Endpoints = new ITaskItem[] { fxEndpoint, pkgEndpoint },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        // Only the framework endpoint should be remapped
        task.RemappedEndpoints.Should().HaveCount(1);
        task.RemappedEndpoints[0].ItemSpec.Should().Be("framework.js");
    }

    [Fact]
    public void Execute_NullEndpoints_DoesNotRemapAndSucceeds()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "framework.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "framework.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = null,
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
        task.RemappedEndpoints.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Execute_EmptyAssetsArray_Succeeds()
    {
        // Arrange
        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = Array.Empty<ITaskItem>(),
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.UpdatedAssets.Should().BeEmpty();
        task.OriginalAssets.Should().BeEmpty();
    }

    [Fact]
    public void Execute_FrameworkAssets_SubdirectoriesArePreserved()
    {
        // Arrange
        var sourceFile = CreateTempFile("source", "lib", "deep", "nested", "component.js", "content");
        var asset = CreateFrameworkAsset(sourceFile, "FxLib", "_content/fxlib", "lib/deep/nested/component.js");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        var expectedPath = Path.GetFullPath(Path.Combine(intermediateOutput, "fx", "FxLib", "lib", "deep", "nested", "component.js"));
        task.UpdatedAssets[0].ItemSpec.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
    }

    // Helpers

    private string CreateTempFile(params string[] pathParts)
    {
        // Last part is the content, everything before is path segments
        var content = pathParts[^1];
        var segments = pathParts[..^1];

        var dir = Path.Combine(new[] { _tempDir }.Concat(segments[..^1]).ToArray());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, segments[^1]);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private ITaskItem CreatePackageAsset(string filePath, string sourceId, string basePath, string relativePath)
    {
        var contentRoot = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        return new TaskItem(filePath, new Dictionary<string, string>
        {
            ["SourceType"] = "Package",
            ["SourceId"] = sourceId,
            ["ContentRoot"] = contentRoot,
            ["BasePath"] = basePath,
            ["RelativePath"] = relativePath,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Primary",
            ["RelatedAsset"] = "",
            ["AssetTraitName"] = "",
            ["AssetTraitValue"] = "",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
            ["Fingerprint"] = "test-fingerprint",
            ["Integrity"] = "test-integrity",
            ["FileLength"] = "10",
            ["LastWriteTime"] = new DateTimeOffset(new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc)).ToString(StaticWebAsset.DateTimeAssetFormat),
        });
    }

    private ITaskItem CreateFrameworkAsset(string filePath, string sourceId, string basePath, string relativePath)
    {
        var contentRoot = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        return new TaskItem(filePath, new Dictionary<string, string>
        {
            ["SourceType"] = "Framework",
            ["SourceId"] = sourceId,
            ["ContentRoot"] = contentRoot,
            ["BasePath"] = basePath,
            ["RelativePath"] = relativePath,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Primary",
            ["RelatedAsset"] = "",
            ["AssetTraitName"] = "",
            ["AssetTraitValue"] = "",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
            ["Fingerprint"] = "test-fingerprint",
            ["Integrity"] = "test-integrity",
            ["FileLength"] = "10",
            ["LastWriteTime"] = new DateTimeOffset(new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc)).ToString(StaticWebAsset.DateTimeAssetFormat),
        });
    }
}
