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
        _testDirectory = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, nameof(AssetToCompressTest), Guid.NewGuid().ToString("N"));
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
        var relatedAssetPath = Path.Combine(_testDirectory, "correct-asset.js");
        var originalItemSpecPath = Path.Combine(_testDirectory, "project-file.esproj");
        File.WriteAllText(relatedAssetPath, "// correct JavaScript content");
        File.WriteAllText(originalItemSpecPath, "<Project></Project>");

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
        File.WriteAllText(esprojFile, "<Project Sdk=\"Microsoft.VisualStudio.JavaScript.Sdk\"></Project>");
        File.WriteAllText(actualJsFile, "// actual JavaScript content");

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
}
