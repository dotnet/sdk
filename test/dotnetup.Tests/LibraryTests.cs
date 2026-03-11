// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class LibraryTests
{
    ITestOutputHelper Log { get; }

    public LibraryTests(ITestOutputHelper log)
    {
        Log = log;
    }

    [Theory]
    [InlineData("9", InstallComponent.SDK)]
    [InlineData("latest", InstallComponent.SDK)]
    [InlineData("sts", InstallComponent.SDK)]
    [InlineData("lts", InstallComponent.SDK)]
    [InlineData("preview", InstallComponent.SDK)]
    [InlineData("9", InstallComponent.Runtime)]
    [InlineData("latest", InstallComponent.Runtime)]
    [InlineData("9", InstallComponent.ASPNETCore)]
    [InlineData("latest", InstallComponent.ASPNETCore)]
    public void LatestVersionForChannelCanBeInstalled(string channel, InstallComponent component)
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        var latestVersion = releaseInfoProvider.GetLatestVersion(component, channel);
        Log.WriteLine($"Latest {component} version for channel '{channel}' is '{latestVersion}'");

        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            component,
            latestVersion!);
    }

    [Fact]
    public void TestGetSupportedChannels()
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var channels = releaseInfoProvider.GetSupportedChannels();

        channels.Should().Contain(new[] { "latest", "lts", "sts", "preview" });

        //  This will need to be updated every few years as versions go out of support
        channels.Should().Contain(new[] { "10.0", "10.0.1xx" });
        channels.Should().NotContain("10");

        channels.Should().NotContain("7.0");
        channels.Should().NotContain("7.0.1xx");

    }

    [Fact]
    public void MuxerIsUpdated_WhenInstallingNewerSdk()
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        // Install .NET SDK 9.0 first
        var sdk9Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "9.0");
        Log.WriteLine($"Installing .NET SDK 9.0: {sdk9Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk9Version!);

        var muxerPath = Path.Combine(testEnv.InstallPath, DotnetupUtilities.GetDotnetExeName());
        File.Exists(muxerPath).Should().BeTrue("muxer should exist after SDK 9.0 installation");

        var muxerHashAfterSdk9 = GetFileHash(muxerPath);
        var muxerSizeAfterSdk9 = new FileInfo(muxerPath).Length;
        Log.WriteLine($"Muxer after SDK 9.0 install - Size: {muxerSizeAfterSdk9}, Hash: {muxerHashAfterSdk9}");

        Version? fileVersionAfterSdk9 = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk9 = GetMuxerFileVersion(muxerPath);
            Log.WriteLine($"Muxer FileVersion after SDK 9.0 install: {fileVersionAfterSdk9}");
        }

        // Install .NET SDK 10.0 second
        var sdk10Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "10.0");
        Log.WriteLine($"Installing .NET SDK 10.0: {sdk10Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk10Version!);

        var muxerHashAfterSdk10 = GetFileHash(muxerPath);
        var muxerSizeAfterSdk10 = new FileInfo(muxerPath).Length;
        Log.WriteLine($"Muxer after SDK 10.0 install - Size: {muxerSizeAfterSdk10}, Hash: {muxerHashAfterSdk10}");

        Version? fileVersionAfterSdk10 = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk10 = GetMuxerFileVersion(muxerPath);
            Log.WriteLine($"Muxer FileVersion after SDK 10.0 install: {fileVersionAfterSdk10}");
        }

        // Verify muxer was updated (file changed)
        muxerHashAfterSdk10.Should().NotBe(muxerHashAfterSdk9, "muxer file should be updated when installing newer SDK");

        // On Windows, also verify FileVersion was upgraded
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk9.Should().NotBeNull("file version should be readable on Windows after SDK 9.0 install");
            fileVersionAfterSdk10.Should().NotBeNull("file version should be readable on Windows after SDK 10.0 install");
            fileVersionAfterSdk10.Should().BeGreaterThan(fileVersionAfterSdk9, "muxer FileVersion should be upgraded when installing newer SDK");
        }
    }

    [Fact]
    public void MuxerIsNotDowngraded_WhenInstallingOlderSdk()
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        // Install .NET SDK 10.0 first
        var sdk10Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "10.0");
        Log.WriteLine($"Installing .NET SDK 10.0: {sdk10Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk10Version!);

        var muxerPath = Path.Combine(testEnv.InstallPath, DotnetupUtilities.GetDotnetExeName());
        File.Exists(muxerPath).Should().BeTrue("muxer should exist after SDK 10.0 installation");

        var muxerHashAfterSdk10 = GetFileHash(muxerPath);
        var muxerSizeAfterSdk10 = new FileInfo(muxerPath).Length;
        Log.WriteLine($"Muxer after SDK 10.0 install - Size: {muxerSizeAfterSdk10}, Hash: {muxerHashAfterSdk10}");

        Version? fileVersionAfterSdk10 = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk10 = GetMuxerFileVersion(muxerPath);
            Log.WriteLine($"Muxer FileVersion after SDK 10.0 install: {fileVersionAfterSdk10}");
        }

        // Install .NET SDK 9.0 second
        var sdk9Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "9.0");
        Log.WriteLine($"Installing .NET SDK 9.0: {sdk9Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk9Version!);

        var muxerHashAfterSdk9 = GetFileHash(muxerPath);
        var muxerSizeAfterSdk9 = new FileInfo(muxerPath).Length;
        Log.WriteLine($"Muxer after SDK 9.0 install - Size: {muxerSizeAfterSdk9}, Hash: {muxerHashAfterSdk9}");

        Version? fileVersionAfterSdk9 = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk9 = GetMuxerFileVersion(muxerPath);
            Log.WriteLine($"Muxer FileVersion after SDK 9.0 install: {fileVersionAfterSdk9}");
        }

        // Verify muxer was NOT downgraded (file unchanged)
        muxerHashAfterSdk9.Should().Be(muxerHashAfterSdk10, "muxer file should not be downgraded when installing older SDK");
        muxerSizeAfterSdk9.Should().Be(muxerSizeAfterSdk10, "muxer file size should not change when installing older SDK");

        // On Windows, also verify FileVersion was not downgraded
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileVersionAfterSdk10.Should().NotBeNull("file version should be readable on Windows after SDK 10.0 install");
            fileVersionAfterSdk9.Should().NotBeNull("file version should be readable on Windows after SDK 9.0 install");
            fileVersionAfterSdk9.Should().Be(fileVersionAfterSdk10, "muxer FileVersion should not be downgraded when installing older SDK");
        }
    }

    private static string GetFileHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    private static Version? GetMuxerFileVersion(string muxerPath)
    {
        if (!File.Exists(muxerPath))
        {
            return null;
        }

        try
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(muxerPath);
            if (versionInfo.FileVersion is not null && Version.TryParse(versionInfo.FileVersion, out Version? version))
            {
                return version;
            }

            // Fallback to constructing version from individual parts
            if (versionInfo.FileMajorPart != 0 || versionInfo.FileMinorPart != 0 ||
                versionInfo.FileBuildPart != 0 || versionInfo.FilePrivatePart != 0)
            {
                return new Version(
                    versionInfo.FileMajorPart,
                    versionInfo.FileMinorPart,
                    versionInfo.FileBuildPart,
                    versionInfo.FilePrivatePart);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    [Fact]
    public void GlobalJsonInfo_SdkPath_ResolvesRelativeToDirectory()
    {
        // Regression test: SdkPath should resolve relative paths using the directory
        // containing global.json, not the file path itself.
        // Bug: Path.GetFullPath(".", "D:\sdk\global.json") incorrectly treats global.json as a directory.
        // Fix: Use Path.GetDirectoryName(GlobalJsonPath) as the base path.

        // Use a cross-platform absolute path (temp directory is always fully qualified)
        var repoDir = Path.Combine(Path.GetTempPath(), "test-repo");
        var globalJsonPath = Path.Combine(repoDir, "global.json");
        var globalJsonInfo = new GlobalJsonInfo
        {
            GlobalJsonPath = globalJsonPath,
            GlobalJsonContents = new GlobalJsonContents
            {
                Sdk = new GlobalJsonContents.SdkSection
                {
                    Paths = new[] { ".dotnet" }
                }
            }
        };

        var sdkPath = globalJsonInfo.SdkPath;

        // Should resolve to <repoDir>\.dotnet, NOT <repoDir>\global.json\.dotnet
        sdkPath.Should().Be(Path.Combine(repoDir, ".dotnet"));
        sdkPath.Should().NotContain("global.json");
    }

    [Fact]
    public void GlobalJsonInfo_SdkPath_ReturnsNullWhenNoPathsConfigured()
    {
        // Use a cross-platform absolute path (temp directory is always fully qualified)
        var repoDir = Path.Combine(Path.GetTempPath(), "test-repo");
        var globalJsonInfo = new GlobalJsonInfo
        {
            GlobalJsonPath = Path.Combine(repoDir, "global.json"),
            GlobalJsonContents = new GlobalJsonContents
            {
                Sdk = new GlobalJsonContents.SdkSection
                {
                    Version = "9.0.100"
                    // No Paths configured
                }
            }
        };

        globalJsonInfo.SdkPath.Should().BeNull();
    }
}
