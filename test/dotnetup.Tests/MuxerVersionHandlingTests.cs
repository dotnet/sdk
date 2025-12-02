// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation.Internal;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for muxer version handling during SDK installation.
/// </summary>
public class MuxerVersionHandlingTests
{
    private readonly ITestOutputHelper _log;

    public MuxerVersionHandlingTests(ITestOutputHelper log)
    {
        _log = log;
    }

    [Fact]
    public void ShouldUpdateMuxer_WhenExistingMuxerDoesNotExist_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"muxer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var newMuxerPath = Path.Combine(tempDir, "new_dotnet.exe");
            var existingMuxerPath = Path.Combine(tempDir, "existing_dotnet.exe");

            // Create a dummy new muxer file (doesn't need actual version info for this test)
            File.WriteAllText(newMuxerPath, "dummy content");

            // Act
            var result = DotnetArchiveExtractor.ShouldUpdateMuxer(newMuxerPath, existingMuxerPath);

            // Assert
            result.Should().BeTrue("when there is no existing muxer, we should install the new one");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ShouldUpdateMuxer_WhenNewMuxerDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"muxer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var newMuxerPath = Path.Combine(tempDir, "new_dotnet.exe");
            var existingMuxerPath = Path.Combine(tempDir, "existing_dotnet.exe");

            // Create a dummy existing muxer file
            File.WriteAllText(existingMuxerPath, "dummy content");
            // New muxer does not exist

            // Act
            var result = DotnetArchiveExtractor.ShouldUpdateMuxer(newMuxerPath, existingMuxerPath);

            // Assert
            result.Should().BeFalse("when the new muxer file doesn't exist, we should not attempt to update");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetMuxerFileVersion_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid():N}.exe");

        // Act
        var version = DotnetArchiveExtractor.GetMuxerFileVersion(nonExistentPath);

        // Assert
        version.Should().BeNull("non-existent files should return null version");
    }

    [Fact]
    public void GetMuxerFileVersion_WithCurrentDotnetExe_ReturnsValidVersion()
    {
        // Skip this test on non-Windows as FileVersionInfo may not work the same way
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _log.WriteLine("Skipping test on non-Windows platform");
            return;
        }

        // Arrange - get the path to the current dotnet executable
        var dotnetExePath = GetCurrentDotnetExePath();

        if (dotnetExePath is null || !File.Exists(dotnetExePath))
        {
            _log.WriteLine($"Could not find dotnet executable at: {dotnetExePath}");
            return;
        }

        _log.WriteLine($"Testing with dotnet at: {dotnetExePath}");

        // Act
        var version = DotnetArchiveExtractor.GetMuxerFileVersion(dotnetExePath);

        // Assert
        version.Should().NotBeNull("the dotnet executable should have version information");
        _log.WriteLine($"Detected version: {version}");
        version!.Major.Should().BeGreaterThan(0, "the major version should be a positive number");
    }

    [Fact]
    public void ShouldUpdateMuxer_WithRealMuxerVersionComparison_WorksCorrectly()
    {
        // Skip this test on non-Windows as FileVersionInfo may not work the same way
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _log.WriteLine("Skipping test on non-Windows platform");
            return;
        }

        // Arrange - get the path to the current dotnet executable
        var dotnetExePath = GetCurrentDotnetExePath();

        if (dotnetExePath is null || !File.Exists(dotnetExePath))
        {
            _log.WriteLine($"Could not find dotnet executable at: {dotnetExePath}");
            return;
        }

        // Act - compare the muxer with itself (should not update since versions are equal)
        var result = DotnetArchiveExtractor.ShouldUpdateMuxer(dotnetExePath, dotnetExePath);

        // Assert - same version should not trigger an update
        result.Should().BeFalse("comparing the same file should not trigger an update");
    }

    [Fact]
    public void GetMuxerFileVersion_WithDummyFile_ReturnsNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"muxer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var dummyFilePath = Path.Combine(tempDir, "dummy.exe");
            File.WriteAllText(dummyFilePath, "This is not a real executable");

            // Act
            var version = DotnetArchiveExtractor.GetMuxerFileVersion(dummyFilePath);

            // Assert
            version.Should().BeNull("a dummy file without version info should return null");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string? GetCurrentDotnetExePath()
    {
        // Try to find the dotnet executable in the current directory or PATH
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

        // First, try the .dotnet directory in the repo root
        var currentDir = AppContext.BaseDirectory;
        while (currentDir != null)
        {
            var repoDotnet = Path.Combine(currentDir, ".dotnet", exeName);
            if (File.Exists(repoDotnet))
            {
                return repoDotnet;
            }

            var parentDir = Directory.GetParent(currentDir);
            currentDir = parentDir?.FullName;
        }

        // Fallback to system dotnet
        var systemDotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(systemDotnet) && File.Exists(systemDotnet))
        {
            return systemDotnet;
        }

        return null;
    }
}
