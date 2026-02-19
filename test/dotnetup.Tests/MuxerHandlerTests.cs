// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
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

    public MuxerHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"muxer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _muxerPath = Path.Combine(_testDir, DotnetupUtilities.GetDotnetExeName());
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
    /// Simulates what the archive extractor does: sets MuxerWasExtracted and writes
    /// to TempMuxerPath when it encounters the muxer entry.
    /// </summary>
    private static void SimulateMuxerExtraction(MuxerHandler handler, string content = "new")
    {
        handler.MuxerWasExtracted = true;
        File.WriteAllText(handler.TempMuxerPath, content);
    }

    private MuxerHandler CreateHandler(bool requireMuxerUpdate = false)
    {
        return new MuxerHandler(_testDir, requireMuxerUpdate);
    }

    [Fact]
    public void NoExistingMuxer_ExtractsNewMuxer()
    {
        // Arrange
        var handler = CreateHandler();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction(handler, "new-8.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert — when no existing muxer, TempMuxerPath == final path, so muxer is already in place
        File.Exists(_muxerPath).Should().BeTrue();
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
    }

    [Fact]
    public void ExistingMuxer_NewerRuntime_ReplacesMuxer()
    {
        // Arrange
        CreateRuntime("7.0.0");
        CreateExistingMuxer("old-7.0");
        var handler = CreateHandler();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction(handler, "new-8.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
        File.Exists(handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void ExistingMuxer_SameOrOlderRuntime_KeepsExisting()
    {
        // Arrange
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");
        var handler = CreateHandler();
        CreateRuntime("7.0.0"); // Older runtime
        SimulateMuxerExtraction(handler, "new-7.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - existing muxer unchanged, temp deleted
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
        File.Exists(handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void NoRuntimeExtracted_DeletesTempMuxer()
    {
        // Arrange - simulates WindowsDesktop which has no core runtime
        CreateExistingMuxer("existing");
        var handler = CreateHandler();
        // Muxer extracted but no runtime created
        SimulateMuxerExtraction(handler, "new");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - existing muxer unchanged, temp deleted
        File.ReadAllText(_muxerPath).Should().Be("existing");
        File.Exists(handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void NoTempMuxerExtracted_NoOp()
    {
        // Arrange - archive didn't contain a muxer (MuxerWasExtracted never set)
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing");
        var handler = CreateHandler();
        CreateRuntime("9.0.0"); // New runtime but muxer not extracted

        // Act
        handler.FinalizeAfterExtraction();

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
        var handler = CreateHandler();

        // Install 9.0.0 (higher than existing highest)
        CreateRuntime("9.0.0");
        SimulateMuxerExtraction(handler, "new-9.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - should replace since 9.0.0 > 8.0.0
        File.ReadAllText(_muxerPath).Should().Be("new-9.0");
    }

    [Fact]
    public void NoPreExistingRuntime_ExtractsMuxer()
    {
        // Edge case: no runtime directories exist yet
        var handler = CreateHandler();
        CreateRuntime("8.0.0");
        SimulateMuxerExtraction(handler, "new-8.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - should install new muxer
        File.ReadAllText(_muxerPath).Should().Be("new-8.0");
    }

    [Fact]
    public void PreReleaseRuntimeVersion_IsRecognized()
    {
        // Arrange - only a preview runtime exists
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-10.0-preview");
        var handler = CreateHandler();

        // Install 9.0.x (lower major version)
        CreateRuntime("9.0.32");
        SimulateMuxerExtraction(handler, "new-9.0");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - muxer should NOT be downgraded since 10.0.0 > 9.0.32
        File.ReadAllText(_muxerPath).Should().Be("existing-10.0-preview");
        File.Exists(handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void PreReleaseRuntimeVersion_UpgradeFromRelease()
    {
        // Arrange - release 9.0 runtime exists
        CreateRuntime("9.0.32");
        CreateExistingMuxer("existing-9.0");
        var handler = CreateHandler();

        // Install 10.0 preview (higher major version)
        CreateRuntime("10.0.0-preview.5.25280.5");
        SimulateMuxerExtraction(handler, "new-10.0-preview");

        // Act
        handler.FinalizeAfterExtraction();

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
        var handler = CreateHandler();

        // Install preview 5 (older preview)
        CreateRuntime("10.0.0-preview.5.25280.5");
        SimulateMuxerExtraction(handler, "new-preview5");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - muxer should NOT be downgraded
        File.ReadAllText(_muxerPath).Should().Be("existing-preview6");
        File.Exists(handler.TempMuxerPath).Should().BeFalse();
    }

    [Fact]
    public void PreReleaseRuntimeVersion_Preview5ReplacedByPreview6()
    {
        // Arrange - preview 5 already installed
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-preview5");
        var handler = CreateHandler();

        // Install preview 6 (newer preview)
        CreateRuntime("10.0.0-preview.6.25300.1");
        SimulateMuxerExtraction(handler, "new-preview6");

        // Act
        handler.FinalizeAfterExtraction();

        // Assert - muxer SHOULD be upgraded
        File.ReadAllText(_muxerPath).Should().Be("new-preview6");
    }

    [Fact]
    public void PreReleaseRuntimeVersion_ReplacedByReleaseOfSameVersion()
    {
        // Arrange - preview runtime exists
        CreateRuntime("10.0.0-preview.5.25280.5");
        CreateExistingMuxer("existing-preview");
        var handler = CreateHandler();

        // Install GA release 10.0.0 (same major.minor.patch, no prerelease = higher precedence)
        CreateRuntime("10.0.0");
        SimulateMuxerExtraction(handler, "new-ga");

        // Act
        handler.FinalizeAfterExtraction();

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
    public void EnsureMuxerIsWritable_ThrowsWhenMuxerIsLocked()
    {
        // Arrange
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");

        // Lock the existing muxer
        using var fileLock = new FileStream(_muxerPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => MuxerHandler.EnsureMuxerIsWritable(_testDir));
        ex.Message.Should().Contain(_muxerPath);
        ex.Message.Should().Contain("in use");

        // Existing muxer is untouched
        fileLock.Close();
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
    }

    [PlatformSpecificFact(TestPlatforms.Windows)]
    public void EnsureMuxerIsWritable_SucceedsWhenMuxerIsNotLocked()
    {
        // Arrange
        CreateExistingMuxer("existing");

        // Act & Assert — should not throw
        MuxerHandler.EnsureMuxerIsWritable(_testDir);
    }

    [Fact]
    public void EnsureMuxerIsWritable_NoOpWhenNoMuxer()
    {
        // No muxer exists — should not throw
        MuxerHandler.EnsureMuxerIsWritable(_testDir);
    }

    [PlatformSpecificFact(TestPlatforms.Windows)]
    public void MuxerBecomesInUseDuringExtraction_RequireUpdate_ThrowsAtFinalize()
    {
        // Arrange - muxer is NOT locked at construction time (passes the early check)
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");

        var handler = new MuxerHandler(_testDir, requireMuxerUpdate: true);

        // Simulate extraction
        CreateRuntime("9.0.0");
        handler.MuxerWasExtracted = true;
        File.WriteAllText(handler.TempMuxerPath, "new-9.0");

        // Lock the muxer AFTER construction (simulates another process starting during download/extraction)
        using var fileLock = new FileStream(_muxerPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act & Assert - should throw at FinalizeAfterExtraction, not silently succeed
        var ex = Assert.Throws<InvalidOperationException>(() => handler.FinalizeAfterExtraction());
        ex.Message.Should().Contain(_muxerPath);
        ex.Message.Should().Contain("in use");

        // Existing muxer is untouched, temp cleaned up
        fileLock.Close();
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
        File.Exists(handler.TempMuxerPath).Should().BeFalse("temp muxer should be cleaned up");
    }

    [PlatformSpecificFact(TestPlatforms.Windows)]
    public void MuxerBecomesInUseDuringExtraction_NoRequireUpdate_WarnsAndKeepsExisting()
    {
        // Arrange - muxer is NOT locked at construction time
        CreateRuntime("8.0.0");
        CreateExistingMuxer("existing-8.0");

        var handler = new MuxerHandler(_testDir, requireMuxerUpdate: false);

        // Simulate extraction
        CreateRuntime("9.0.0");
        handler.MuxerWasExtracted = true;
        File.WriteAllText(handler.TempMuxerPath, "new-9.0");

        // Lock the muxer AFTER construction
        using var fileLock = new FileStream(_muxerPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act - should not throw, should warn
        handler.FinalizeAfterExtraction();

        // Assert - existing muxer kept
        fileLock.Close();
        File.ReadAllText(_muxerPath).Should().Be("existing-8.0");
        File.Exists(handler.TempMuxerPath).Should().BeFalse("temp muxer should be cleaned up");
    }

    [Fact]
    public void GetDotnetProcessPidInfo_DoesNotKillProcess()
    {
        // Start a dotnet process we can look up by name.
        var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = "help";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        try
        {
            int pid = proc.Id;

            // Call the utility — this enumerates processes without disposing them
            string pidInfo = DotnetupUtilities.GetDotnetProcessPidInfo();
            pidInfo.Should().Contain(pid.ToString(), "our started process should appear in the PID list");

            // The actual process should still be running
            proc.HasExited.Should().BeFalse(
                "GetDotnetProcessPidInfo must not kill running dotnet processes");
        }
        finally
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
            }
            catch { }
            proc.Dispose();
        }
    }
}
