// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.Dotnet.Installation.Tests;

public class MuxerHandlerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _muxerPath;
    private readonly MuxerHandler _handler;

    public MuxerHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"muxer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _muxerPath = Path.Combine(_testDir, DotnetupUtilities.GetDotnetExeName());
        _handler = new MuxerHandler(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private void CreateRuntime(string version)
    {
        var runtimeDir = Path.Combine(_testDir, "shared", "Microsoft.NETCore.App", version);
        Directory.CreateDirectory(runtimeDir);
        File.WriteAllText(Path.Combine(runtimeDir, "marker.txt"), "test");
    }

    private void CreateExistingMuxer(string content = "existing")
    {
        File.WriteAllText(_muxerPath, content);
    }

    /// <summary>
    /// Simulates what the archive extractor does: calls GetMuxerExtractionPath() when
    /// it encounters the muxer entry, then writes to that path.
    /// </summary>
    private void SimulateMuxerExtraction(string content = "new")
    {
        var tempPath = _handler.GetMuxerExtractionPath();
        File.WriteAllText(tempPath, content);
    }

    [Fact]
    public void NoExistingMuxer_ExtractsNewMuxer()
    {
        // Arrange
        _handler.RecordPreExtractionState();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction("new-8.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert
        File.Exists(_muxerPath).Should().BeTrue();
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void ExistingMuxer_NewerRuntime_ReplacesMuxer()
    {
        // Arrange
        CreateRuntime("7.0.0");
        CreateExistingMuxer("old-7.0");
        _handler.RecordPreExtractionState();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction("new-8.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void ExistingMuxer_SameOrOlderRuntime_KeepsExisting()
    {
        // Arrange
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");
        _handler.RecordPreExtractionState();
        CreateRuntime("7.0.0"); // Older runtime
        SimulateMuxerExtraction("new-7.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - existing muxer unchanged, temp deleted
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void NoRuntimeExtracted_DeletesTempMuxer()
    {
        // Arrange - simulates WindowsDesktop which has no core runtime
        CreateExistingMuxer("existing");
        _handler.RecordPreExtractionState();
        // Muxer extracted but no runtime created
        SimulateMuxerExtraction("new");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - existing muxer unchanged, temp deleted
        File.ReadAllText(_muxerPath).Should().Be("existing");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void NoTempMuxerExtracted_NoOp()
    {
        // Arrange - archive didn't contain a muxer (GetMuxerExtractionPath never called)
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing");
        _handler.RecordPreExtractionState();
        CreateRuntime("9.0.0"); // New runtime but muxer not extracted

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - existing muxer unchanged
        File.ReadAllText(_muxerPath).Should().Be("existing");
    }

    [Fact]
    public void MultipleRuntimes_UsesHighestForComparison()
    {
        // Arrange - multiple existing runtimes, highest is 8.0.0
        CreateRuntime("6.0.0");
        CreateRuntime("7.0.0");
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");
        _handler.RecordPreExtractionState();

        // Install 9.0.0 (higher than existing highest)
        CreateRuntime("9.0.0");
        SimulateMuxerExtraction("new-9.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - should replace since 9.0.0 > 8.0.0
        File.ReadAllText(_muxerPath).Should().Be("new-9.0");
    }

    [Fact]
    public void NoPreExistingRuntime_ExtractsMuxer()
    {
        // Edge case: no runtime directories exist yet
        _handler.RecordPreExtractionState();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction("new-8.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - should install new muxer
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
    }

    [Fact]
    public void PreReleaseRuntimeVersion_IsRecognized()
    {
        // Arrange - only a preview runtime exists
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-10.0-preview");
        _handler.RecordPreExtractionState();

        // Install 9.0.x (lower major version)
        CreateRuntime("9.0.32");
        SimulateMuxerExtraction("new-9.0");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - muxer should NOT be downgraded since 10.0.0 > 9.0.32
        File.ReadAllText(_muxerPath).Should().Be("existing-10.0-preview");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void PreReleaseRuntimeVersion_UpgradeFromRelease()
    {
        // Arrange - release 9.0 runtime exists
        CreateRuntime("9.0.32");
        CreateExistingMuxer("existing-9.0");
        _handler.RecordPreExtractionState();

        // Install 10.0 preview (higher major version)
        CreateRuntime("10.0.0-preview.5.25280.5");
        SimulateMuxerExtraction("new-10.0-preview");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - muxer SHOULD be upgraded since 10.0.0 > 9.0.32
        File.ReadAllText(_muxerPath).Should().Be("new-10.0-preview");
    }

    [Fact]
    public void GetLatestRuntimeVersionFromInstallRoot_HandlesPreReleaseVersions()
    {
        // Arrange
        CreateRuntime("9.0.32");
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateRuntime("8.0.15");

        // Act
        var result = MuxerHandler.GetLatestRuntimeVersionFromInstallRoot(_testDir);

        // Assert - should return the full pre-release version
        result.Should().NotBeNull();
        result!.Major.Should().Be(10);
        result.Minor.Should().Be(0);
        result.Patch.Should().Be(0);
        result.Prerelease.Should().Be("preview.5.25280.5");
    }

    [Fact]
    public void PreReleaseRuntimeVersion_Preview5NotReplacedByPreview4()
    {
        // Arrange - preview 6 already installed
        CreateRuntime("10.0.0-preview.6.25300.1");
        CreateExistingMuxer("existing-preview6");
        _handler.RecordPreExtractionState();

        // Install preview 5 (older preview)
        CreateRuntime("10.0.0-preview.5.25280.5");
        SimulateMuxerExtraction("new-preview5");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - muxer should NOT be downgraded
        File.ReadAllText(_muxerPath).Should().Be("existing-preview6");
        File.Exists(_handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void PreReleaseRuntimeVersion_Preview5ReplacedByPreview6()
    {
        // Arrange - preview 5 already installed
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-preview5");
        _handler.RecordPreExtractionState();

        // Install preview 6 (newer preview)
        CreateRuntime("10.0.0-preview.6.25300.1");
        SimulateMuxerExtraction("new-preview6");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - muxer SHOULD be upgraded
        File.ReadAllText(_muxerPath).Should().Be("new-preview6");
    }

    [Fact]
    public void PreReleaseRuntimeVersion_ReplacedByReleaseOfSameVersion()
    {
        // Arrange - preview runtime exists
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-preview");
        _handler.RecordPreExtractionState();

        // Install GA release 10.0.0 (same major.minor.patch, no prerelease = higher precedence)
        CreateRuntime("10.0.0");
        SimulateMuxerExtraction("new-ga");

        // Act
        _handler.FinalizeAfterExtraction();

        // Assert - GA > preview per semver, so muxer should be upgraded
        File.ReadAllText(_muxerPath).Should().Be("new-ga");
    }

    [Fact]
    public void GetLatestRuntimeVersionFromInstallRoot_NonexistentPath_ReturnsNull()
    {
        var result = MuxerHandler.GetLatestRuntimeVersionFromInstallRoot(Path.Combine(_testDir, "nonexistent"));
        result.Should().BeNull();
    }

    [PlatformSpecificFact(TestPlatforms.Windows)] // File locking simulation only works on Windows; actual error handling is cross-platform
    public void MuxerInUse_RequireMuxerUpdateFalse_WarnsAndKeepsExisting()
    {
        // Arrange
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");

        var handler = new MuxerHandler(_testDir, requireMuxerUpdate: false);
        handler.RecordPreExtractionState();

        CreateRuntime("9.0.0");
        var tempPath = handler.GetMuxerExtractionPath();
        File.WriteAllText(tempPath, "new-9.0");

        // Lock the existing muxer to simulate it being in use
        using var fileLock = new FileStream(_muxerPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act - should not throw, should warn
        handler.FinalizeAfterExtraction();

        // Assert - existing muxer kept, warning emitted to stderr
        fileLock.Close();
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
        File.Exists(tempPath).Should().BeFalse("temp muxer should be cleaned up");
    }

    [PlatformSpecificFact(TestPlatforms.Windows)] // File locking simulation only works on Windows; actual error handling is cross-platform
    public void MuxerInUse_RequireMuxerUpdateTrue_ThrowsWithPath()
    {
        // Arrange
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");

        var handler = new MuxerHandler(_testDir, requireMuxerUpdate: true);
        handler.RecordPreExtractionState();

        CreateRuntime("9.0.0");
        var tempPath = handler.GetMuxerExtractionPath();
        File.WriteAllText(tempPath, "new-9.0");

        // Lock the existing muxer to simulate it being in use
        using var fileLock = new FileStream(_muxerPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act & Assert - should throw with path in message
        var ex = Assert.Throws<InvalidOperationException>(() => handler.FinalizeAfterExtraction());
        ex.Message.Should().Contain(_muxerPath);
        ex.Message.Should().Contain("in use");

        // Cleanup
        fileLock.Close();
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0", "existing muxer should be preserved");
        File.Exists(tempPath).Should().BeFalse("temp muxer should be cleaned up even on failure");
    }
}
