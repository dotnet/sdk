// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
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
    [InlineData("9")]
    [InlineData("latest")]
    [InlineData("sts")]
    [InlineData("lts")]
    [InlineData("preview")]
    public void LatestVersionForChannelCanBeInstalled(string channel)
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, channel);
        Log.WriteLine($"Latest version for channel '{channel}' is '{latestVersion}'");

        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
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
        // Skip test on non-Windows as FileVersionInfo doesn't work with ELF binaries
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log.WriteLine("Skipping test on non-Windows platform (FileVersionInfo doesn't work with ELF binaries)");
            return;
        }

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
        var versionAfterSdk9 = DotnetArchiveExtractor.GetMuxerFileVersion(muxerPath);
        Log.WriteLine($"Muxer version after SDK 9.0 install: {versionAfterSdk9}");
        versionAfterSdk9.Should().NotBeNull("muxer should exist after SDK 9.0 installation");

        // Install .NET SDK 10.0 second
        var sdk10Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "10.0");
        Log.WriteLine($"Installing .NET SDK 10.0: {sdk10Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk10Version!);

        var versionAfterSdk10 = DotnetArchiveExtractor.GetMuxerFileVersion(muxerPath);
        Log.WriteLine($"Muxer version after SDK 10.0 install: {versionAfterSdk10}");
        versionAfterSdk10.Should().NotBeNull("muxer should exist after SDK 10.0 installation");

        // Verify muxer was updated to newer version
        versionAfterSdk10.Should().BeGreaterThan(versionAfterSdk9!, "muxer should be updated when installing newer SDK");
    }

    [Fact]
    public void MuxerIsNotDowngraded_WhenInstallingOlderSdk()
    {
        // Skip test on non-Windows as FileVersionInfo doesn't work with ELF binaries
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log.WriteLine("Skipping test on non-Windows platform (FileVersionInfo doesn't work with ELF binaries)");
            return;
        }

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
        var versionAfterSdk10 = DotnetArchiveExtractor.GetMuxerFileVersion(muxerPath);
        Log.WriteLine($"Muxer version after SDK 10.0 install: {versionAfterSdk10}");
        versionAfterSdk10.Should().NotBeNull("muxer should exist after SDK 10.0 installation");

        // Install .NET SDK 9.0 second
        var sdk9Version = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "9.0");
        Log.WriteLine($"Installing .NET SDK 9.0: {sdk9Version}");
        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            sdk9Version!);

        var versionAfterSdk9 = DotnetArchiveExtractor.GetMuxerFileVersion(muxerPath);
        Log.WriteLine($"Muxer version after SDK 9.0 install: {versionAfterSdk9}");
        versionAfterSdk9.Should().NotBeNull("muxer should exist after SDK 9.0 installation");

        // Verify muxer was NOT downgraded
        versionAfterSdk9.Should().Be(versionAfterSdk10, "muxer should not be downgraded when installing older SDK");
    }
}
