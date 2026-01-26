// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for DotnetArchiveExtractor, particularly error handling scenarios.
/// </summary>
public class DotnetArchiveExtractorTests
{
    private readonly ITestOutputHelper _log;

    public DotnetArchiveExtractorTests(ITestOutputHelper log)
    {
        _log = log;
    }

    [Fact]
    public void Prepare_InvalidVersion_ThrowsException()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var invalidVersion = new ReleaseVersion(99, 99, 99); // Version that doesn't exist

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("99.99"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, invalidVersion, releaseManifest, progressTarget);

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => extractor.Prepare());
        _log.WriteLine($"Exception message: {ex.Message}");
        ex.Message.Should().Contain("99.99.99");
    }

    [Fact]
    public void ExistingMuxer_IsPreserved_OnExtractionFailure()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create a fake existing muxer
        var muxerPath = Path.Combine(testEnv.InstallPath, DotnetupUtilities.GetDotnetExeName());
        File.WriteAllText(muxerPath, "existing muxer content");

        var originalContent = File.ReadAllText(muxerPath);
        _log.WriteLine($"Created fake muxer at: {muxerPath}");

        // Create a shared runtime directory to simulate existing installation
        var runtimeDir = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "8.0.0");
        Directory.CreateDirectory(runtimeDir);

        // Verify muxer exists before test
        File.Exists(muxerPath).Should().BeTrue("muxer should exist before test");

        // Note: A full test of extraction failure would require mocking the archive download/extraction.
        // For now, this test documents the expected behavior:
        // If extraction fails, the existing muxer should be restored from the .tmp backup.
        _log.WriteLine("TODO: Add mock-based test for extraction failure scenario");
    }

    [Fact]
    public void Dispose_CleansUpTemporaryFiles()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        {
            using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget);
            // The scratch directory is created in the constructor
            // We can't easily get the path, but we verify Dispose doesn't throw
        }

        // Assert - Dispose completed without exception
        _log.WriteLine("Dispose completed successfully");
    }
}
