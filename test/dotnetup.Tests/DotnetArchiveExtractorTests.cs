// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;
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
    public void Prepare_DownloadFailure_ThrowsException()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        var mockDownloader = new MockArchiveDownloader
        {
            ExceptionToThrow = new Exception("Network error: Unable to connect")
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => extractor.Prepare());
        _log.WriteLine($"Exception message: {ex.Message}");
        ex.Message.Should().Contain("Failed to download");
        ex.InnerException!.Message.Should().Contain("Network error");

        // Verify the download was attempted
        mockDownloader.DownloadCalls.Should().HaveCount(1);
        mockDownloader.DownloadCalls[0].Version.Should().Be(version);
    }

    [Fact]
    public void ExistingMuxer_IsPreserved_OnExtractionFailure()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create a fake existing muxer
        var muxerPath = Path.Combine(testEnv.InstallPath, DotnetupUtilities.GetDotnetExeName());
        var originalContent = "existing muxer content";
        File.WriteAllText(muxerPath, originalContent);
        _log.WriteLine($"Created fake muxer at: {muxerPath}");

        // Create a shared runtime directory to simulate existing installation
        var runtimeDir = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "8.0.0");
        Directory.CreateDirectory(runtimeDir);

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 0);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.Runtime,
            new InstallRequestOptions());

        // Create a mock downloader that creates an invalid/empty archive
        var mockDownloader = new MockArchiveDownloader
        {
            CreateFakeArchive = true,
            FakeArchiveContent = new byte[] { 0x00, 0x01, 0x02 } // Invalid archive content
        };

        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        // Prepare succeeds (download works)
        extractor.Prepare();
        _log.WriteLine("Prepare completed successfully");

        // Commit should fail due to invalid archive, but muxer should be restored
        var ex = Assert.ThrowsAny<Exception>(() => extractor.Commit());
        _log.WriteLine($"Commit failed as expected: {ex.Message}");

        // Verify the muxer was restored
        File.Exists(muxerPath).Should().BeTrue("muxer should be restored after extraction failure");
        File.ReadAllText(muxerPath).Should().Be(originalContent, "muxer content should be preserved");
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

        var mockDownloader = new MockArchiveDownloader();
        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        string scratchDir;
        {
            using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

            // Get the scratch directory path via the internal property
            scratchDir = extractor.ScratchDownloadDirectory;
            _log.WriteLine($"Scratch directory: {scratchDir}");

            // Verify it exists during operation
            Directory.Exists(scratchDir).Should().BeTrue("scratch directory should exist during operation");
        }
        // After dispose

        // Verify it was cleaned up
        Directory.Exists(scratchDir).Should().BeFalse("scratch directory should be deleted after Dispose");
        _log.WriteLine("Dispose cleaned up scratch directory successfully");
    }

    [Fact]
    public void Prepare_RecordsCorrectDownloadParameters()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        var version = new ReleaseVersion(9, 0, 12);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.ASPNETCore,
            new InstallRequestOptions());

        var mockDownloader = new MockArchiveDownloader();
        var releaseManifest = new ReleaseManifest();
        var progressTarget = new NullProgressTarget();

        using var extractor = new DotnetArchiveExtractor(request, version, releaseManifest, progressTarget, mockDownloader);

        extractor.Prepare();

        // Verify the correct parameters were passed to the downloader
        mockDownloader.DownloadCalls.Should().HaveCount(1);

        var call = mockDownloader.DownloadCalls[0];
        call.Request.Should().Be(request);
        call.Version.Should().Be(version);
        call.DestinationPath.Should().StartWith(extractor.ScratchDownloadDirectory);
        call.DestinationPath.Should().EndWith(DotnetupUtilities.GetArchiveFileExtensionForPlatform());

        _log.WriteLine($"Download was called with version {call.Version} to {call.DestinationPath}");
    }

    /// <summary>
    /// Creates a tar file with entries having specified Unix permissions.
    /// </summary>
    private static void CreateTarWithPermissions(string tarPath, params (string name, UnixFileMode mode, bool isDirectory)[] entries)
    {
        using var fs = File.Create(tarPath);
        using var writer = new TarWriter(fs);

        foreach (var (name, mode, isDirectory) in entries)
        {
            TarEntry entry;
            if (isDirectory)
            {
                entry = new PaxTarEntry(TarEntryType.Directory, name) { Mode = mode };
            }
            else
            {
                entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    Mode = mode,
                    DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"content-of-{name}"))
                };
            }

            writer.WriteEntry(entry);
        }
    }

    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX)]
    public void ExtractTarContents_PreservesExecutePermission()
    {
        // Arrange — create a tar with an executable (755) and a non-executable (644) entry
        var testDir = Path.Combine(Path.GetTempPath(), $"tar-perm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            var tarPath = Path.Combine(testDir, "test.tar");
            var extractDir = Path.Combine(testDir, "extracted");
            Directory.CreateDirectory(extractDir);

            var executableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                               | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                               | UnixFileMode.OtherRead | UnixFileMode.OtherExecute; // 755
            var readOnlyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                             | UnixFileMode.GroupRead
                             | UnixFileMode.OtherRead; // 644

            CreateTarWithPermissions(tarPath,
                ("bin/dotnet", executableMode, isDirectory: false),
                ("shared/readme.txt", readOnlyMode, isDirectory: false));

            // Act — ExtractToFile should apply Mode via UnixCreateMode
            DotnetArchiveExtractor.ExtractTarContents(tarPath, extractDir, installTask: null);

            // Assert
            var dotnetPath = Path.Combine(extractDir, "bin", "dotnet");
            var readmePath = Path.Combine(extractDir, "shared", "readme.txt");

            File.Exists(dotnetPath).Should().BeTrue();
            File.Exists(readmePath).Should().BeTrue();

#pragma warning disable CA1416 // Validate platform compatibility — test is gated by PlatformSpecificFact
            var dotnetMode = File.GetUnixFileMode(dotnetPath);
            var readmeMode = File.GetUnixFileMode(readmePath);
#pragma warning restore CA1416

            _log.WriteLine($"dotnet mode: {dotnetMode} ({(int)dotnetMode:o})");
            _log.WriteLine($"readme mode: {readmeMode} ({(int)readmeMode:o})");

            dotnetMode.Should().HaveFlag(UnixFileMode.UserExecute, "executable entry should preserve UserExecute");
            readmeMode.Should().NotHaveFlag(UnixFileMode.UserExecute, "non-executable entry should not have UserExecute");
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX)]
    public void ExtractTarContents_PreservesDirectoryPermissions()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"tar-dir-perm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            var tarPath = Path.Combine(testDir, "test.tar");
            var extractDir = Path.Combine(testDir, "extracted");
            Directory.CreateDirectory(extractDir);

            var dirMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute; // 755

            CreateTarWithPermissions(tarPath,
                ("mydir/", dirMode, isDirectory: true));

            // Act
            DotnetArchiveExtractor.ExtractTarContents(tarPath, extractDir, installTask: null);

            // Assert
            var dirPath = Path.Combine(extractDir, "mydir");
            Directory.Exists(dirPath).Should().BeTrue();

#pragma warning disable CA1416 // Validate platform compatibility — test is gated by PlatformSpecificFact
            var actualMode = File.GetUnixFileMode(dirPath);
#pragma warning restore CA1416
            _log.WriteLine($"directory mode: {actualMode} ({(int)actualMode:o})");

            actualMode.Should().HaveFlag(UnixFileMode.UserExecute, "directory should preserve UserExecute");
            actualMode.Should().HaveFlag(UnixFileMode.UserRead);
            actualMode.Should().HaveFlag(UnixFileMode.UserWrite);
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Fact]
    public void ExtractTarContents_ExtractsContentCorrectly()
    {
        // Cross-platform test for content correctness
        var testDir = Path.Combine(Path.GetTempPath(), $"tar-content-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            var tarPath = Path.Combine(testDir, "test.tar");
            var extractDir = Path.Combine(testDir, "extracted");
            Directory.CreateDirectory(extractDir);

            var defaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

            CreateTarWithPermissions(tarPath,
                ("hello.txt", defaultMode, isDirectory: false),
                ("sub/nested.txt", defaultMode, isDirectory: false));

            // Act
            DotnetArchiveExtractor.ExtractTarContents(tarPath, extractDir, installTask: null);

            // Assert
            var helloPath = Path.Combine(extractDir, "hello.txt");
            var nestedPath = Path.Combine(extractDir, "sub", "nested.txt");

            File.Exists(helloPath).Should().BeTrue();
            File.ReadAllText(helloPath).Should().Be("content-of-hello.txt");

            File.Exists(nestedPath).Should().BeTrue();
            File.ReadAllText(nestedPath).Should().Be("content-of-sub/nested.txt");
        }
        finally
        {
            Directory.Delete(testDir, recursive: true);
        }
    }
}

