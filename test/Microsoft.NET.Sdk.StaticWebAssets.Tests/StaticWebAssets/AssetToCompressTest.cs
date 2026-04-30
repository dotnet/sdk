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
    public void TryFindInputFilePath_ReturnsError_WhenRelatedAssetDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent.js");
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", nonExistentPath);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeFalse();
        fullPath.Should().BeNull();
        _errorMessages.Should().ContainSingle();
        _errorMessages[0].Should().Contain("can not be found");
        _errorMessages[0].Should().Contain(nonExistentPath);
    }

    [Fact]
    public void TryFindInputFilePath_ReturnsError_WhenRelatedAssetIsEmpty()
    {
        // Arrange
        var assetToCompress = new TaskItem("test.js.gz");
        assetToCompress.SetMetadata("RelatedAsset", "");

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert
        result.Should().BeFalse();
        fullPath.Should().BeNull();
        _errorMessages.Should().ContainSingle();
    }

    [Fact]
    public void TryFindInputFilePath_HandlesEsprojScenario_WhereRelatedAssetPointsToActualFile()
    {
        // Arrange - simulate the esproj scenario where RelatedAsset points to the actual JS file
        var actualJsFile = Path.Combine(_testDirectory, "dist", "app.min.js");

        Directory.CreateDirectory(Path.GetDirectoryName(actualJsFile));
        File.WriteAllText(actualJsFile, "// actual JavaScript content");

        var assetToCompress = new TaskItem(Path.Combine(_testDirectory, "compressed", "app.min.js.gz"));
        assetToCompress.SetMetadata("RelatedAsset", actualJsFile);

        // Act
        var result = AssetToCompress.TryFindInputFilePath(assetToCompress, _log, out var fullPath);

        // Assert - should use RelatedAsset (the actual JavaScript file)
        result.Should().BeTrue();
        fullPath.Should().Be(actualJsFile);
        _errorMessages.Should().BeEmpty();
    }
}
