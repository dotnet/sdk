// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class AssetToCompressTest : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly TaskLoggingHelper _log;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public AssetToCompressTest()
    {
        _testDirectory = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(AssetToCompressTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "test-asset.js");
        File.WriteAllText(_testFilePath, "// test content");

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _logMessages.Add(args.Message));

        var dummyTask = new Mock<ITask>();
        dummyTask.Setup(t => t.BuildEngine).Returns(_buildEngine.Object);
        _log = new TaskLoggingHelper(dummyTask.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void TryFindInputFilePath_UsesRelatedAsset_WhenFileExists()
    {
        // Arrange
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", _testFilePath);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", "some-other-path.js");

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeTrue();
        fullPath.Should().Be(_testFilePath);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_FallsBackToRelatedAssetOriginalItemSpec_WhenRelatedAssetDoesNotExist()
    {
        // Arrange
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", Path.Combine(_testDirectory, "non-existent.js"));
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", _testFilePath);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeTrue();
        fullPath.Should().Be(_testFilePath);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_ReturnsError_WhenNeitherPathExists()
    {
        // Arrange
        var nonExistentPath1 = Path.Combine(_testDirectory, "non-existent1.js");
        var nonExistentPath2 = Path.Combine(_testDirectory, "non-existent2.js");
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", nonExistentPath1);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", nonExistentPath2);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeFalse();
        fullPath.Should().BeNull();
        _errorMessages.Should().ContainSingle();
        _errorMessages[0].Should().Contain("can not be found");
        _errorMessages[0].Should().Contain(nonExistentPath1);
        _errorMessages[0].Should().Contain(nonExistentPath2);
    }

    [Fact]
    public void TryFindInputFilePath_PrefersRelatedAsset_OverRelatedAssetOriginalItemSpec_WhenBothExist()
    {
        // Arrange - create two files to simulate the scenario where both metadata values point to existing files
        // Make RelatedAsset newer to ensure it's preferred in the normal case
        var relatedAssetPath = Path.Combine(_testDirectory, "correct-asset.js");
        var originalItemSpecPath = Path.Combine(_testDirectory, "project-file.esproj");

        // Create originalItemSpec first (older)
        File.WriteAllText(originalItemSpecPath, "<Project></Project>");
        File.SetLastWriteTimeUtc(originalItemSpecPath, DateTime.UtcNow.AddMinutes(-5));

        // Create RelatedAsset second (newer)
        File.WriteAllText(relatedAssetPath, "// correct JavaScript content");
        File.SetLastWriteTimeUtc(relatedAssetPath, DateTime.UtcNow);

        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", relatedAssetPath);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", originalItemSpecPath);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert - should prefer RelatedAsset (the actual JavaScript file) over RelatedAssetOriginalItemSpec (the esproj file)
        result.Should().BeTrue();
        fullPath.Should().Be(relatedAssetPath);
        fullPath.Should().NotBe(originalItemSpecPath);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_HandlesEmptyRelatedAsset_AndUsesRelatedAssetOriginalItemSpec()
    {
        // Arrange
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", "");
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", _testFilePath);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeTrue();
        fullPath.Should().Be(_testFilePath);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_HandlesEsprojScenario_WhereOriginalItemSpecPointsToProjectFile()
    {
        // Arrange - simulate the esproj bug scenario where RelatedAssetOriginalItemSpec
        // incorrectly points to the .esproj project file instead of the actual JS asset
        var esprojFile = Path.Combine(_testDirectory, "MyProject.esproj");
        var actualJsFile = Path.Combine(_testDirectory, "dist", "app.min.js");

        Directory.CreateDirectory(Path.GetDirectoryName(actualJsFile));

        // Create esproj first (older)
        File.WriteAllText(esprojFile, "<Project Sdk=\"Microsoft.VisualStudio.JavaScript.Sdk\"></Project>");
        File.SetLastWriteTimeUtc(esprojFile, DateTime.UtcNow.AddMinutes(-5));

        // Create actual JS file second (newer)
        File.WriteAllText(actualJsFile, "// actual JavaScript content");
        File.SetLastWriteTimeUtc(actualJsFile, DateTime.UtcNow);

        var assetToCompress = new TaskItem(Path.Combine(_testDirectory, "compressed", "app.min.js.gz"));
        // RelatedAsset should contain the correct path to the actual JS file
        assetToCompress.SetMetadata("RelatedAsset", actualJsFile);
        // RelatedAssetOriginalItemSpec may incorrectly point to .esproj due to esproj SDK bug
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", esprojFile);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert - should use RelatedAsset (correct JS file) not RelatedAssetOriginalItemSpec (esproj file)
        result.Should().BeTrue();
        fullPath.Should().Be(actualJsFile);
        fullPath.Should().NotBe(esprojFile);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_PrefersNewerFile_WhenBothFilesExistAndOriginalItemSpecIsNewer()
    {
        // Arrange - simulate incremental build scenario where source file (OriginalItemSpec)
        // is newer than destination file (RelatedAsset) because the copy hasn't happened yet.
        // This is the scenario that causes SRI integrity failures in Blazor WASM (issue #65271).
        var destinationFile = Path.Combine(_testDirectory, "wwwroot", "_framework", "dotnet.js");
        var sourceFile = Path.Combine(_testDirectory, "obj", "Debug", "net11.0", "dotnet.js");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));

        // Create destination file first (older)
        File.WriteAllText(destinationFile, "// old content with stale fingerprints");
        var oldTime = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(destinationFile, oldTime);

        // Create source file second (newer) - this simulates a rebuild
        File.WriteAllText(sourceFile, "// new content with updated fingerprints");
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow);

        var assetToCompress = new TaskItem(Path.Combine(_testDirectory, "compressed", "dotnet.js.gz"));
        assetToCompress.SetMetadata("RelatedAsset", destinationFile);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", sourceFile);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert - should use the NEWER source file, not the stale destination
        result.Should().BeTrue();
        fullPath.Should().Be(sourceFile);
        fullPath.Should().NotBe(destinationFile);
        _errorMessages.Should().BeEmpty();
        _logMessages.Should().Contain(m => m.Contains("newer"));
    }

    [Fact]
    public void TryFindInputFilePath_UsesRelatedAsset_WhenBothFilesExistButRelatedAssetIsNewer()
    {
        // Arrange - when destination file is newer (normal case after copy), use it
        var destinationFile = Path.Combine(_testDirectory, "wwwroot", "_framework", "script.js");
        var sourceFile = Path.Combine(_testDirectory, "obj", "Debug", "script.js");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));

        // Create source file first (older)
        File.WriteAllText(sourceFile, "// source content");
        var oldTime = DateTime.UtcNow.AddMinutes(-5);
        File.SetLastWriteTimeUtc(sourceFile, oldTime);

        // Create destination file second (newer) - this simulates normal post-copy state
        File.WriteAllText(destinationFile, "// destination content");
        File.SetLastWriteTimeUtc(destinationFile, DateTime.UtcNow);

        var assetToCompress = new TaskItem(Path.Combine(_testDirectory, "compressed", "script.js.gz"));
        assetToCompress.SetMetadata("RelatedAsset", destinationFile);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", sourceFile);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert - should use destination file since it's newer
        result.Should().BeTrue();
        fullPath.Should().Be(destinationFile);
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void TryFindInputFilePath_UsesRelatedAsset_WhenBothPathsPointToSameFile()
    {
        // Arrange - when both paths point to the same file (case-insensitive),
        // don't bother with timestamp comparison
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", _testFilePath);
        assetToCompress.SetMetadata("RelatedAssetOriginalItemSpec", _testFilePath.ToUpperInvariant());

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeTrue();
        fullPath.Should().Be(_testFilePath);
        _errorMessages.Should().BeEmpty();
        // Should NOT log the "newer" message since paths are the same
        _logMessages.Should().NotContain(m => m.Contains("newer"));
    }
}
