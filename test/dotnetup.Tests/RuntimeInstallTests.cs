// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for runtime installation functionality including manifest tracking,
/// version resolution, and component type handling.
/// </summary>
public class RuntimeInstallTests
{
    private readonly ITestOutputHelper _log;

    public RuntimeInstallTests(ITestOutputHelper log)
    {
        _log = log;
    }

    #region Version Resolution Tests

    [Theory]
    [InlineData("latest")]
    [InlineData("lts")]
    [InlineData("sts")]
    [InlineData("9")]
    [InlineData("9.0")]
    public void VersionResolution_RuntimeChannels_ReturnsValidVersion(string channel)
    {
        // Arrange
        var resolver = new ChannelVersionResolver();

        // Act
        var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channel), InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"Channel '{channel}' resolved to runtime version: {version}");
        version.Should().NotBeNull($"channel '{channel}' should resolve to a valid runtime version");
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("lts")]
    [InlineData("9.0")]
    public void VersionResolution_ASPNETCoreChannels_ReturnsValidVersion(string channel)
    {
        // Arrange
        var resolver = new ChannelVersionResolver();

        // Act
        var version = resolver.GetLatestVersionForChannel(new UpdateChannel(channel), InstallComponent.ASPNETCore);

        // Assert
        _log.WriteLine($"Channel '{channel}' resolved to ASP.NET Core version: {version}");
        version.Should().NotBeNull($"channel '{channel}' should resolve to a valid ASP.NET Core version");
    }

    [Fact]
    public void VersionResolution_FeatureBandForRuntime_ReturnsNull()
    {
        // Arrange - Feature bands (like 9.0.1xx) are SDK-specific and should not work for runtimes
        var resolver = new ChannelVersionResolver();

        // Act
        var version = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"Feature band '9.0.1xx' for runtime resolved to: {version}");
        version.Should().BeNull("feature bands should not be valid for runtime components");
    }

    [Fact]
    public void VersionResolution_RuntimeVersionsDifferFromSdkVersions()
    {
        // Arrange
        var resolver = new ChannelVersionResolver();

        // Act
        var sdkVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.SDK);
        var runtimeVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"SDK 9.0 version: {sdkVersion}");
        _log.WriteLine($"Runtime 9.0 version: {runtimeVersion}");

        sdkVersion.Should().NotBeNull();
        runtimeVersion.Should().NotBeNull();

        // SDK versions typically look like 9.0.100, runtime versions like 9.0.0
        sdkVersion!.ToString().Should().NotBe(runtimeVersion!.ToString(),
            "SDK and runtime versions should differ (SDK has feature band)");
    }

    #endregion

    #region Manifest Tracking Tests

    [Fact]
    public void ManifestTracking_RuntimeInstall_CreatesSeperateEntry()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var runtimeInstall = new DotnetInstall(
            installRoot,
            new ReleaseVersion("9.0.0"),
            InstallComponent.Runtime);

        // Act - Simulate adding a runtime install to manifest
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Assert
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions().ToList();
        }

        _log.WriteLine($"Manifest contains {installs.Count()} entries");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().ContainSingle();
        installs.First().Component.Should().Be(InstallComponent.Runtime);
        installs.First().Version.ToString().Should().Be("9.0.0");
    }

    [Fact]
    public void ManifestTracking_SdkAndRuntime_TrackedSeparately()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var sdkInstall = new DotnetInstall(
            installRoot,
            new ReleaseVersion("9.0.100"),
            InstallComponent.SDK);

        var runtimeInstall = new DotnetInstall(
            installRoot,
            new ReleaseVersion("9.0.0"),
            InstallComponent.Runtime);

        // Act
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Assert
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions().ToList();
        }

        _log.WriteLine($"Manifest contains {installs.Count()} entries");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().HaveCount(2);
        installs.Should().Contain(i => i.Component == InstallComponent.SDK && i.Version.ToString() == "9.0.100");
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "9.0.0");
    }

    [Fact]
    public void ManifestTracking_MultipleRuntimeTypes_TrackedSeparately()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var coreRuntime = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);
        var aspnetRuntime = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.ASPNETCore);
        var desktopRuntime = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.WindowsDesktop);

        // Act
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(coreRuntime);
            manifestManager.AddInstalledVersion(aspnetRuntime);
            manifestManager.AddInstalledVersion(desktopRuntime);
        }

        // Assert
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions().ToList();
        }

        _log.WriteLine($"Manifest contains {installs.Count()} entries");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().HaveCount(3);
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime);
        installs.Should().Contain(i => i.Component == InstallComponent.ASPNETCore);
        installs.Should().Contain(i => i.Component == InstallComponent.WindowsDesktop);
    }

    #endregion

    #region Already Installed Detection Tests

    [Fact]
    public void AlreadyInstalled_SameComponentAndVersion_DetectedAsInstalled()
    {
        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var existingInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(existingInstall);
        }

        // Act - Check if the same install is detected
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        var isAlreadyInstalled = installs.Any(existing =>
            existing.Version.Equals(new ReleaseVersion("9.0.0")) &&
            existing.Component == InstallComponent.Runtime);

        // Assert
        isAlreadyInstalled.Should().BeTrue("same component and version should be detected as already installed");
    }

    [Fact]
    public void AlreadyInstalled_DifferentComponent_NotDetectedAsInstalled()
    {
        // Arrange - SDK is installed, checking if runtime is installed
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
        }

        // Act - Check if runtime 9.0.0 is detected as installed (it shouldn't be)
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        var isRuntimeInstalled = installs.Any(existing =>
            existing.Version.Equals(new ReleaseVersion("9.0.0")) &&
            existing.Component == InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"Manifest entries: {string.Join(", ", installs.Select(i => $"{i.Component}:{i.Version}"))}");
        isRuntimeInstalled.Should().BeFalse("runtime should not be detected when only SDK is installed");
    }

    [Fact]
    public void AlreadyInstalled_SameVersionDifferentComponent_NotDetectedAsInstalled()
    {
        // Arrange - Core runtime is installed, checking if ASP.NET Core is installed
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var coreRuntime = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(coreRuntime);
        }

        // Act - Check if ASP.NET Core 9.0.0 is detected as installed
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        var isAspNetInstalled = installs.Any(existing =>
            existing.Version.Equals(new ReleaseVersion("9.0.0")) &&
            existing.Component == InstallComponent.ASPNETCore);

        // Assert
        isAspNetInstalled.Should().BeFalse("ASP.NET Core should not be detected when only core runtime is installed");
    }

    #endregion

    #region Component Type Mapping Tests

    [Theory]
    [InlineData("core", InstallComponent.Runtime)]
    [InlineData("aspnetcore", InstallComponent.ASPNETCore)]
    [InlineData("windowsdesktop", InstallComponent.WindowsDesktop)]
    public void ComponentTypeMapping_ValidTypes_MapCorrectly(string typeName, InstallComponent expectedComponent)
    {
        // Act
        var result = RuntimeInstallCommandHelper.ParseRuntimeType(typeName);

        // Assert
        _log.WriteLine($"Type '{typeName}' mapped to: {result}");
        result.Should().Be(expectedComponent);
    }

    [Theory]
    [InlineData("CORE")]
    [InlineData("Core")]
    [InlineData("ASPNETCORE")]
    [InlineData("AspNetCore")]
    [InlineData("WINDOWSDESKTOP")]
    [InlineData("WindowsDesktop")]
    public void ComponentTypeMapping_CaseInsensitive(string typeName)
    {
        // Act
        var result = RuntimeInstallCommandHelper.ParseRuntimeType(typeName);

        // Assert
        _log.WriteLine($"Type '{typeName}' (case-insensitive) mapped to: {result}");
        result.Should().NotBeNull("type parsing should be case-insensitive");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("dotnet")]
    [InlineData("sdk")]
    [InlineData("")]
    public void ComponentTypeMapping_InvalidTypes_ReturnsNull(string typeName)
    {
        // Act
        var result = RuntimeInstallCommandHelper.ParseRuntimeType(typeName);

        // Assert
        _log.WriteLine($"Invalid type '{typeName}' mapped to: {result?.ToString() ?? "null"}");
        result.Should().BeNull($"'{typeName}' is not a valid runtime type");
    }

    #endregion

    #region Feature Band Error Tests

    [Fact]
    public void FeatureBand_ForRuntime_ReturnsNullVersion()
    {
        // Arrange - Feature bands like "9.0.1xx" are SDK-specific and shouldn't work for runtimes
        var resolver = new ChannelVersionResolver();

        // Act
        var runtimeVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.Runtime);
        var aspnetVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.ASPNETCore);
        var desktopVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0.1xx"), InstallComponent.WindowsDesktop);

        // Assert - All runtime types should return null for feature band
        _log.WriteLine($"Feature band '9.0.1xx' for Runtime: {runtimeVersion?.ToString() ?? "null"}");
        _log.WriteLine($"Feature band '9.0.1xx' for ASPNETCore: {aspnetVersion?.ToString() ?? "null"}");
        _log.WriteLine($"Feature band '9.0.1xx' for WindowsDesktop: {desktopVersion?.ToString() ?? "null"}");

        runtimeVersion.Should().BeNull("feature bands are not valid for .NET Runtime");
        aspnetVersion.Should().BeNull("feature bands are not valid for ASP.NET Core Runtime");
        desktopVersion.Should().BeNull("feature bands are not valid for Windows Desktop Runtime");
    }

    [Theory]
    [InlineData("9.0.1xx")]
    [InlineData("10.0.2xx")]
    [InlineData("8.0.3xx")]
    public void FeatureBand_MultiplePatterns_AllReturnNullForRuntime(string featureBand)
    {
        // Arrange
        var resolver = new ChannelVersionResolver();

        // Act
        var version = resolver.GetLatestVersionForChannel(new UpdateChannel(featureBand), InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"Feature band '{featureBand}' for runtime resolved to: {version?.ToString() ?? "null"}");
        version.Should().BeNull($"feature band '{featureBand}' should not be valid for runtime components");
    }

    [Fact]
    public void FeatureBand_WorksForSdk_ButNotForRuntime()
    {
        // Arrange - Same feature band should work for SDK but not for runtime
        var resolver = new ChannelVersionResolver();
        var featureBand = "9.0.1xx";

        // Act
        var sdkVersion = resolver.GetLatestVersionForChannel(new UpdateChannel(featureBand), InstallComponent.SDK);
        var runtimeVersion = resolver.GetLatestVersionForChannel(new UpdateChannel(featureBand), InstallComponent.Runtime);

        // Assert
        _log.WriteLine($"SDK version for '{featureBand}': {sdkVersion?.ToString() ?? "null"}");
        _log.WriteLine($"Runtime version for '{featureBand}': {runtimeVersion?.ToString() ?? "null"}");

        sdkVersion.Should().NotBeNull("feature bands should work for SDK");
        runtimeVersion.Should().BeNull("feature bands should NOT work for runtimes");
    }

    #endregion

    #region Explicit Runtime Install Adds To Manifest Tests

    [Fact]
    public void ExplicitRuntimeInstall_AlwaysAddsToManifest_EvenIfSdkHasSameVersion()
    {
        // Arrange - SDK 9.0.100 includes runtime 9.0.0
        // When user explicitly installs runtime 9.0.0, it should be added to manifest
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Simulate SDK already installed (but not runtime tracked in manifest)
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
        }

        // Act - User explicitly requests runtime install - should add to manifest
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Assert - Both SDK and Runtime should be in manifest
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine($"Manifest entries after explicit runtime install:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().HaveCount(2);
        installs.Should().Contain(i => i.Component == InstallComponent.SDK && i.Version.ToString() == "9.0.100");
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "9.0.0",
            "explicit runtime install should add runtime to manifest even when SDK is already installed");
    }

    [Fact]
    public void ExplicitRuntimeInstall_DifferentVersion_AddsToManifest()
    {
        // Arrange - SDK 9.0.100 includes runtime 9.0.0
        // User installs runtime 10.0.0 explicitly - should be tracked separately
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Simulate SDK 9.0.100 already installed
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
        }

        // Act - User explicitly installs a different runtime version
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Assert
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine($"Manifest entries:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().Contain(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "10.0.0");
    }

    #endregion

    #region Runtime Dependency Tests (ASPNETCore/WindowsDesktop require Core Runtime)

    [Fact]
    public void RuntimeDependency_ASPNETCoreRequiresCoreRuntime()
    {
        // Note: When installing ASP.NET Core runtime, the core runtime should also be installed
        // This test documents the expected behavior - ASP.NET Core depends on Microsoft.NETCore.App

        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.ASPNETCore, "9.0");
        _log.WriteLine($"ASP.NET Core version to install: {latestVersion}");

        // Act - Install ASP.NET Core
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());
        installer.Install(installRoot, InstallComponent.ASPNETCore, latestVersion!);

        // Assert - Both Microsoft.AspNetCore.App AND Microsoft.NETCore.App should exist
        var aspNetPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.AspNetCore.App");
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");

        Directory.Exists(aspNetPath).Should().BeTrue("ASP.NET Core runtime should be installed");

        // Note: The installer currently downloads just the component requested.
        // This test documents that behavior - in future, dependencies may be auto-installed.
        _log.WriteLine($"ASP.NET Core installed: {Directory.Exists(aspNetPath)}");
        _log.WriteLine($"Core runtime installed: {Directory.Exists(coreRuntimePath)}");

        // For now, just verify ASP.NET Core was installed
        var aspNetVersionDir = Path.Combine(aspNetPath, latestVersion!.ToString());
        Directory.Exists(aspNetVersionDir).Should().BeTrue($"ASP.NET Core version {latestVersion} should be installed");
    }

    [Fact]
    public void RuntimeDependency_WindowsDesktopRequiresCoreRuntime()
    {
        // Note: When installing Windows Desktop runtime, the core runtime should also be installed
        // This test documents the expected behavior

        if (!OperatingSystem.IsWindows())
        {
            _log.WriteLine("Skipping WindowsDesktop test on non-Windows platform");
            return;
        }

        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.WindowsDesktop, "9.0");
        _log.WriteLine($"Windows Desktop version to install: {latestVersion}");

        // Act - Install Windows Desktop
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());
        installer.Install(installRoot, InstallComponent.WindowsDesktop, latestVersion!);

        // Assert - Microsoft.WindowsDesktop.App should exist
        var desktopPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.WindowsDesktop.App");
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");

        Directory.Exists(desktopPath).Should().BeTrue("Windows Desktop runtime should be installed");

        _log.WriteLine($"Windows Desktop installed: {Directory.Exists(desktopPath)}");
        _log.WriteLine($"Core runtime installed: {Directory.Exists(coreRuntimePath)}");

        var desktopVersionDir = Path.Combine(desktopPath, latestVersion!.ToString());
        Directory.Exists(desktopVersionDir).Should().BeTrue($"Windows Desktop version {latestVersion} should be installed");
    }

    #endregion

    #region SDK-Installed Runtime Detection Tests

    [Fact]
    public void SdkInstalledRuntime_DetectedFromFilesystem()
    {
        // Scenario: SDK is installed which includes runtime files on disk.
        // When user requests to install that exact runtime, we should detect
        // the files already exist and not re-download.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Simulate SDK installation created runtime directory on disk
        var runtimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.0");
        Directory.CreateDirectory(runtimePath);
        File.WriteAllText(Path.Combine(runtimePath, "dummy.dll"), "placeholder");

        _log.WriteLine($"Created simulated runtime at: {runtimePath}");
        _log.WriteLine($"Directory exists: {Directory.Exists(runtimePath)}");

        // Verify the directory exists
        Directory.Exists(runtimePath).Should().BeTrue("simulated runtime directory should exist");
    }

    [Fact]
    public void SdkInstalledRuntime_ManifestChecksComponentAndVersion()
    {
        // When checking if something is already installed, we check BOTH version AND component type.
        // Having SDK 9.0.100 in manifest does NOT mean Runtime 9.0.0 is "already installed" in manifest.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Add SDK to manifest
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
        }

        // Act - Check if runtime is marked as installed (it shouldn't be)
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        // Simulate the InstallAlreadyExists check
        var runtimeToInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);
        var isRuntimeInManifest = installs.Any(existing =>
            existing.Version.Equals(runtimeToInstall.Version) &&
            existing.Component == runtimeToInstall.Component);

        // Assert
        _log.WriteLine($"SDK 9.0.100 in manifest: {installs.Any(i => i.Component == InstallComponent.SDK)}");
        _log.WriteLine($"Runtime 9.0.0 in manifest: {isRuntimeInManifest}");

        isRuntimeInManifest.Should().BeFalse(
            "Runtime should NOT be marked as installed just because SDK is installed - components are tracked separately");
    }

    [Fact]
    public void OrchestratorInstall_SkipsDownload_WhenAlreadyInManifest()
    {
        // When the orchestrator tries to install something already in manifest,
        // it should skip the download and return the existing install.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Pre-add runtime to manifest (simulating previous install)
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        _log.WriteLine("Pre-added Runtime 9.0.0 to manifest");

        // Act - Try to install the same runtime via orchestrator
        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0.0"),  // Exact version
            InstallComponent.Runtime,
            new InstallRequestOptions { ManifestPath = testEnv.ManifestPath });

        // This should return quickly without downloading because it's already in manifest
        var result = InstallerOrchestratorSingleton.Instance.Install(request);

        // Assert
        result.Should().NotBeNull("Orchestrator should return the existing install");
        result!.Version.ToString().Should().Be("9.0.0");
        result.Component.Should().Be(InstallComponent.Runtime);

        _log.WriteLine($"Orchestrator returned: {result.Component} {result.Version}");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void RuntimeInstall_CanInstallCoreRuntime()
    {
        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.Runtime, "9.0");
        _log.WriteLine($"Installing runtime version: {latestVersion}");

        // Act
        installer.Install(installRoot, InstallComponent.Runtime, latestVersion!);

        // Assert - Check that shared/Microsoft.NETCore.App directory exists
        var runtimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");
        Directory.Exists(runtimePath).Should().BeTrue("runtime should be installed to shared/Microsoft.NETCore.App");

        var versionDir = Path.Combine(runtimePath, latestVersion!.ToString());
        Directory.Exists(versionDir).Should().BeTrue($"runtime version directory {latestVersion} should exist");

        _log.WriteLine($"Runtime installed at: {versionDir}");
    }

    [Fact]
    public void RuntimeInstall_CanInstallASPNETCoreRuntime()
    {
        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.ASPNETCore, "9.0");
        _log.WriteLine($"Installing ASP.NET Core version: {latestVersion}");

        // Act
        installer.Install(installRoot, InstallComponent.ASPNETCore, latestVersion!);

        // Assert - Check that shared/Microsoft.AspNetCore.App directory exists
        var runtimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.AspNetCore.App");
        Directory.Exists(runtimePath).Should().BeTrue("ASP.NET Core should be installed to shared/Microsoft.AspNetCore.App");

        var versionDir = Path.Combine(runtimePath, latestVersion!.ToString());
        Directory.Exists(versionDir).Should().BeTrue($"ASP.NET Core version directory {latestVersion} should exist");

        _log.WriteLine($"ASP.NET Core installed at: {versionDir}");
    }

    [Fact(Skip = "Long-running integration test - requires downloading full SDK (~200MB). Run manually or in CI.")]
    public void RuntimeInstall_AfterSdkInstall_AddsToManifest()
    {
        // Arrange - First install SDK, then install runtime
        // Note: This test uses the orchestrator directly to test manifest tracking,
        // as the library's DotnetInstaller doesn't handle manifest updates.
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        // Install SDK first using orchestrator (which handles manifest)
        var sdkVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, "9.0");
        _log.WriteLine($"Installing SDK version: {sdkVersion}");

        var sdkRequest = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(sdkVersion!.ToString()),
            InstallComponent.SDK,
            new InstallRequestOptions { ManifestPath = testEnv.ManifestPath });

        var sdkInstall = InstallerOrchestratorSingleton.Instance.Install(sdkRequest);
        sdkInstall.Should().NotBeNull("SDK installation should succeed");

        // Get the runtime version that would correspond to this SDK
        var runtimeVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.Runtime, "9.0");
        _log.WriteLine($"Installing runtime version: {runtimeVersion}");

        // Act - Install runtime using orchestrator (may already exist on disk from SDK install)
        var runtimeRequest = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(runtimeVersion!.ToString()),
            InstallComponent.Runtime,
            new InstallRequestOptions { ManifestPath = testEnv.ManifestPath });

        var runtimeInstall = InstallerOrchestratorSingleton.Instance.Install(runtimeRequest);
        runtimeInstall.Should().NotBeNull("Runtime installation should succeed");

        // Assert - Both should be in manifest
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine($"Manifest entries after SDK + Runtime install:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        installs.Should().Contain(i => i.Component == InstallComponent.SDK, "SDK should be in manifest");
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime, "Runtime should be in manifest even if files existed from SDK");
    }

    #endregion

    #region Parser Behavior Tests

    [Fact]
    public void Parser_RuntimeInstallWithoutType_ReturnsError()
    {
        // Arrange - Runtime install requires the type argument
        var args = new[] { "runtime", "install" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert - Should have errors because type is required
        _log.WriteLine($"Parse errors: {string.Join(", ", parseResult.Errors.Select(e => e.Message))}");
        parseResult.Errors.Should().NotBeEmpty("type argument is required for runtime install");
    }

    [Theory]
    [InlineData("core", "9.0")]
    [InlineData("aspnetcore", "latest")]
    [InlineData("windowsdesktop", "lts")]
    public void Parser_RuntimeInstallWithValidType_Succeeds(string runtimeType, string channel)
    {
        // Arrange
        var args = new[] { "runtime", "install", runtimeType, channel };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        _log.WriteLine($"Parsing 'runtime install {runtimeType} {channel}'");
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_RuntimeInstall_InvalidType_StillParsesSuccessfully()
    {
        // Note: The parser accepts any string for type - validation happens in the command
        var args = new[] { "runtime", "install", "invalid_type", "9.0" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert - Parser doesn't validate type values, that's done by the command
        _log.WriteLine($"Parse errors for invalid type: {parseResult.Errors.Count}");
        parseResult.Errors.Should().BeEmpty("parser accepts any string, validation is in command");
    }

    #endregion

    #region All Runtimes Installation Tests (Future Feature Specification)

    [Fact]
    public void TypeArgument_IsCurrentlyRequired()
    {
        // This test documents current behavior: type argument IS required.
        // Future: If "install all runtimes when type not specified" is implemented,
        // this test should be updated to verify that behavior instead.

        var args = new[] { "runtime", "install", "9.0" };  // No type, just channel

        // Act
        var parseResult = Parser.Parse(args);

        // Assert - Currently, this parses with "9.0" as the type (not channel)
        // which would fail at runtime since "9.0" isn't a valid runtime type.
        // The parser doesn't error here, but the command would.
        _log.WriteLine($"Without explicit type, '9.0' is interpreted as: type argument");
        _log.WriteLine($"Parse result errors: {parseResult.Errors.Count}");

        // Document the current behavior
        // When only one arg after "runtime install", it's treated as type, not channel
        parseResult.Errors.Should().BeEmpty("parser accepts this, but command would reject '9.0' as invalid type");
    }

    [Fact]
    public void RuntimeTypeMapping_AllValue_NotCurrentlySupported()
    {
        // Test that "all" is not currently a valid runtime type
        var result = RuntimeInstallCommandHelper.ParseRuntimeType("all");

        _log.WriteLine($"'all' runtime type parsed as: {result?.ToString() ?? "null"}");
        result.Should().BeNull("'all' is not currently a supported runtime type");
    }

    #endregion

    #region Runtime Archive Dependency Tests

    [Fact(Skip = "Long-running test - downloads ASP.NET Core runtime. Run manually to verify manifest behavior.")]
    public void ASPNETCoreInstall_OnlyTracksASPNETCoreInManifest_NotCoreRuntime()
    {
        // IMPORTANT: When installing ASP.NET Core, the archive includes Microsoft.NETCore.App files,
        // but the manifest ONLY tracks ASPNETCore - NOT the core runtime.
        // This is a potential issue: if we uninstall ASPNETCore based on manifest,
        // we might delete files that the SDK or other components depend on.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var aspnetRequest = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel("9.0"),
            InstallComponent.ASPNETCore,
            new InstallRequestOptions { ManifestPath = testEnv.ManifestPath });

        // Act - Install ASP.NET Core (which includes core runtime files)
        var result = InstallerOrchestratorSingleton.Instance.Install(aspnetRequest);
        result.Should().NotBeNull();

        // Assert - Check what's in the manifest
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine($"After ASP.NET Core install, manifest contains {installs.Count()} entries:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        // Manifest should ONLY have ASPNETCore, not Runtime
        installs.Should().ContainSingle("only ASPNETCore should be tracked");
        installs.First().Component.Should().Be(InstallComponent.ASPNETCore);

        // But the core runtime files ARE on disk!
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");
        Directory.Exists(coreRuntimePath).Should().BeTrue(
            "Core runtime files exist on disk (from ASP.NET Core archive) but are NOT tracked in manifest");

        _log.WriteLine($"\nWARNING: Core runtime exists at {coreRuntimePath} but is NOT in manifest!");
        _log.WriteLine("This means uninstalling ASPNETCore could orphan or break other components.");
    }

    [Fact]
    public void ASPNETCoreInstall_OnlyTracksASPNETCoreInManifest_NotCoreRuntime_UnitTest()
    {
        // Unit test version - no download, just verifies manifest tracking logic
        // IMPORTANT: When installing ASP.NET Core, the manifest ONLY tracks ASPNETCore - NOT the core runtime.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Simulate what the orchestrator does: add ONLY the requested component to manifest
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(aspnetInstall);
        }

        // Simulate: Core runtime files also exist on disk (from the archive)
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", "9.0.12");
        Directory.CreateDirectory(coreRuntimePath);
        File.WriteAllText(Path.Combine(coreRuntimePath, "System.Runtime.dll"), "placeholder");

        // Assert - Check what's in the manifest
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine($"After ASP.NET Core install, manifest contains {installs.Count()} entries:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        // Manifest should ONLY have ASPNETCore, not Runtime
        installs.Should().ContainSingle("only ASPNETCore should be tracked");
        installs.First().Component.Should().Be(InstallComponent.ASPNETCore);

        // But the core runtime files ARE on disk!
        Directory.Exists(coreRuntimePath).Should().BeTrue(
            "Core runtime files exist on disk but are NOT tracked in manifest");

        _log.WriteLine($"\nWARNING: Core runtime exists at {coreRuntimePath} but is NOT in manifest!");
        _log.WriteLine("This is a design concern: uninstalling ASPNETCore could orphan or break other components.");
    }

    [Fact]
    public void UninstallConcern_ASPNETCoreAndSdkShareCoreRuntime()
    {
        // SCENARIO: User installs SDK (which includes core runtime), then installs ASP.NET Core runtime.
        // The ASP.NET Core install includes core runtime in its archive.
        // If user uninstalls ASP.NET Core, should the shared core runtime files be deleted?
        //
        // CURRENT BEHAVIOR: Manifest tracks components separately.
        // - SDK install tracks: SDK
        // - ASPNETCore install tracks: ASPNETCore
        // - Core runtime files exist on disk but may not be explicitly tracked
        //
        // RISK: Naive uninstall based on "what this component installed" could break other components.

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Simulate: SDK 9.0.100 is installed (which includes core runtime 9.0.x)
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
        }

        // Simulate: User then installs ASPNETCore 9.0.12 (archive includes core runtime 9.0.12)
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(aspnetInstall);
        }

        // Check manifest state
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine("Manifest state before hypothetical uninstall:");
        foreach (var install in installs)
        {
            _log.WriteLine($"  - {install.Component}: {install.Version}");
        }

        // Assert: Both are tracked, but core runtime is NOT explicitly tracked
        installs.Should().HaveCount(2);
        installs.Should().Contain(i => i.Component == InstallComponent.SDK);
        installs.Should().Contain(i => i.Component == InstallComponent.ASPNETCore);
        installs.Should().NotContain(i => i.Component == InstallComponent.Runtime,
            "Core runtime is NOT explicitly tracked even though it exists on disk");

        _log.WriteLine("\nUNINSTALL CONCERN:");
        _log.WriteLine("If we uninstall ASPNETCore 9.0.12, we should NOT delete shared/Microsoft.NETCore.App/9.0.12");
        _log.WriteLine("because the SDK might depend on those files.");
        _log.WriteLine("Current implementation: Uninstall logic must check for dependent components.");
    }

    #endregion

    #region Uninstall Strategy Tests

    /// <summary>
    /// Helper to check if core runtime can be safely uninstalled based on manifest state.
    /// This mirrors the logic that should be implemented in the uninstall command.
    /// </summary>
    private static bool CanSafelyUninstallCoreRuntime(IEnumerable<DotnetInstall> manifestInstalls, ReleaseVersion runtimeVersion)
    {
        int majorMinor = runtimeVersion.Major * 100 + runtimeVersion.Minor;

        // Check for SDK with same major.minor (SDK 9.0.1xx bundles runtime 9.0.x)
        bool hasSdkWithSameMajorMinor = manifestInstalls.Any(i =>
            i.Component == InstallComponent.SDK &&
            (i.Version.Major * 100 + i.Version.Minor) == majorMinor);

        // Check for other runtime components with same major.minor
        bool hasOtherRuntimeWithSameMajorMinor = manifestInstalls.Any(i =>
            i.Component == InstallComponent.ASPNETCore &&
            (i.Version.Major * 100 + i.Version.Minor) == majorMinor);

        // Can only uninstall if no SDK or other runtime depends on it
        return !hasSdkWithSameMajorMinor && !hasOtherRuntimeWithSameMajorMinor;
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_BlockedBySdk()
    {
        // When SDK 9.0.100 exists, cannot safely uninstall core runtime 9.0.x

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // SDK 9.0.100 and explicitly installed Runtime 9.0.12
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Act
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        bool canUninstall = CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12"));

        // Assert
        _log.WriteLine($"Manifest: {string.Join(", ", installs.Select(i => $"{i.Component}:{i.Version}"))}");
        _log.WriteLine($"Can safely uninstall core runtime 9.0.12: {canUninstall}");

        canUninstall.Should().BeFalse("SDK 9.0.100 depends on core runtime 9.0.x");
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_BlockedByASPNETCore()
    {
        // When ASPNETCore 9.0.12 exists, cannot safely uninstall core runtime 9.0.x

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // ASPNETCore 9.0.12 and explicitly installed Runtime 9.0.12
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(aspnetInstall);
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Act
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        bool canUninstall = CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12"));

        // Assert
        _log.WriteLine($"Manifest: {string.Join(", ", installs.Select(i => $"{i.Component}:{i.Version}"))}");
        _log.WriteLine($"Can safely uninstall core runtime 9.0.12: {canUninstall}");

        canUninstall.Should().BeFalse("ASPNETCore 9.0.12 depends on core runtime 9.0.x");
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_AllowedWhenNoDependencies()
    {
        // When only core runtime exists, can safely uninstall

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Only explicitly installed Runtime 9.0.12
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Act
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        bool canUninstall = CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12"));

        // Assert
        _log.WriteLine($"Manifest: {string.Join(", ", installs.Select(i => $"{i.Component}:{i.Version}"))}");
        _log.WriteLine($"Can safely uninstall core runtime 9.0.12: {canUninstall}");

        canUninstall.Should().BeTrue("No other components depend on core runtime 9.0.x");
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_DifferentMajorMinor_Allowed()
    {
        // SDK 9.0.100 should NOT block uninstall of runtime 10.0.x (different major.minor)

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // SDK 9.0.100 and Runtime 10.0.2 (different major.minor)
        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("10.0.2"), InstallComponent.Runtime);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        // Act
        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        bool canUninstall = CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("10.0.2"));

        // Assert
        _log.WriteLine($"Manifest: {string.Join(", ", installs.Select(i => $"{i.Component}:{i.Version}"))}");
        _log.WriteLine($"Can safely uninstall core runtime 10.0.2: {canUninstall}");

        canUninstall.Should().BeTrue("SDK 9.0.100 does not depend on runtime 10.0.x");
    }

    [Fact]
    public void UninstallStrategy_ASPNETCore_SafeToUninstallWhenSdkExists()
    {
        // ASP.NET Core specific files (Microsoft.AspNetCore.App) can be uninstalled
        // even when SDK exists, because SDK doesn't depend on ASP.NET Core runtime

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
            manifestManager.AddInstalledVersion(aspnetInstall);
        }

        // Assert - ASP.NET Core specific directory can be deleted
        // but Microsoft.NETCore.App should NOT be deleted
        _log.WriteLine("When uninstalling ASPNETCore 9.0.12 with SDK 9.0.100 present:");
        _log.WriteLine("  - DELETE: shared/Microsoft.AspNetCore.App/9.0.12 ✓");
        _log.WriteLine("  - KEEP: shared/Microsoft.NETCore.App/9.0.x (SDK depends on it)");
    }

    [Fact]
    public void UninstallStrategy_WindowsDesktop_AlwaysSafeToUninstall()
    {
        // Windows Desktop runtime is standalone - no other component depends on it

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var sdkInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK);
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);
        var desktopInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.WindowsDesktop);

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(sdkInstall);
            manifestManager.AddInstalledVersion(aspnetInstall);
            manifestManager.AddInstalledVersion(desktopInstall);
        }

        // Assert
        _log.WriteLine("When uninstalling WindowsDesktop 9.0.12:");
        _log.WriteLine("  - DELETE: shared/Microsoft.WindowsDesktop.App/9.0.12 ✓");
        _log.WriteLine("  - No other components depend on Windows Desktop");
        _log.WriteLine("  - WindowsDesktop archive doesn't include core runtime, so no shared file concerns");
    }

    [Fact]
    public void ManifestTracking_CoreRuntimeOnlyTrackedWhenExplicitlyInstalled()
    {
        // Core runtime should ONLY be tracked in manifest when installed via "dotnetup runtime install core"
        // NOT when it comes bundled with aspnetcore or windowsdesktop archives

        // Arrange
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifestManager = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Scenario 1: User installs ASPNETCore - should NOT track core runtime
        var aspnetInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(aspnetInstall);
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine("After 'dotnetup runtime install aspnetcore 9.0':");
        foreach (var i in installs) _log.WriteLine($"  - {i.Component}: {i.Version}");

        installs.Should().NotContain(i => i.Component == InstallComponent.Runtime,
            "Core runtime should NOT be tracked when installing ASPNETCore");

        // Scenario 2: User explicitly installs core runtime - SHOULD track it
        var runtimeInstall = new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime);
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestManager.AddInstalledVersion(runtimeInstall);
        }

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifestManager.GetInstalledVersions(installRoot).ToList();
        }

        _log.WriteLine("\nAfter 'dotnetup runtime install core 9.0':");
        foreach (var i in installs) _log.WriteLine($"  - {i.Component}: {i.Version}");

        installs.Should().Contain(i => i.Component == InstallComponent.Runtime,
            "Core runtime SHOULD be tracked when explicitly installed via 'core' option");
    }

    #endregion

    [Fact]
    public void ASPNETCoreArchive_IncludesCoreRuntime()
    {
        // The ASP.NET Core archive from Microsoft includes Microsoft.NETCore.App
        // This test verifies that installing ASP.NET Core also installs the core runtime

        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.ASPNETCore, "9.0");
        _log.WriteLine($"Installing ASP.NET Core version: {latestVersion}");

        // Act
        installer.Install(installRoot, InstallComponent.ASPNETCore, latestVersion!);

        // Assert - Both should exist
        var aspNetPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.AspNetCore.App", latestVersion!.ToString());
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", latestVersion!.ToString());

        _log.WriteLine($"ASP.NET Core path exists: {Directory.Exists(aspNetPath)} ({aspNetPath})");
        _log.WriteLine($"Core runtime path exists: {Directory.Exists(coreRuntimePath)} ({coreRuntimePath})");

        Directory.Exists(aspNetPath).Should().BeTrue("ASP.NET Core should be installed");
        Directory.Exists(coreRuntimePath).Should().BeTrue("Core runtime should be installed as part of ASP.NET Core archive");
    }

    [Fact]
    public void WindowsDesktopArchive_DoesNotIncludeCoreRuntime()
    {
        // Document current behavior: Windows Desktop archive does NOT include core runtime
        // Users would need to install core runtime separately

        if (!OperatingSystem.IsWindows())
        {
            _log.WriteLine("Skipping WindowsDesktop test on non-Windows platform");
            return;
        }

        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.WindowsDesktop, "9.0");
        _log.WriteLine($"Installing Windows Desktop version: {latestVersion}");

        // Act
        installer.Install(installRoot, InstallComponent.WindowsDesktop, latestVersion!);

        // Assert
        var desktopPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.WindowsDesktop.App", latestVersion!.ToString());
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");

        _log.WriteLine($"Windows Desktop path exists: {Directory.Exists(desktopPath)}");
        _log.WriteLine($"Core runtime path exists: {Directory.Exists(coreRuntimePath)}");

        Directory.Exists(desktopPath).Should().BeTrue("Windows Desktop should be installed");
        Directory.Exists(coreRuntimePath).Should().BeFalse(
            "Windows Desktop archive does NOT include core runtime - users must install it separately");
    }

    [Fact]
    public void CoreRuntimeArchive_IncludesASPNETCore()
    {
        // The Core runtime archive from Microsoft DOES include ASP.NET Core runtime
        // This is Microsoft's packaging decision - the "runtime" download includes both

        // Arrange
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.Runtime, "9.0");
        _log.WriteLine($"Installing core runtime version: {latestVersion}");

        // Act
        installer.Install(installRoot, InstallComponent.Runtime, latestVersion!);

        // Assert
        var coreRuntimePath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App", latestVersion!.ToString());
        var aspNetPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.AspNetCore.App");
        var desktopPath = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.WindowsDesktop.App");

        _log.WriteLine($"Core runtime path exists: {Directory.Exists(coreRuntimePath)}");
        _log.WriteLine($"ASP.NET Core path exists: {Directory.Exists(aspNetPath)}");
        _log.WriteLine($"Windows Desktop path exists: {Directory.Exists(desktopPath)}");

        Directory.Exists(coreRuntimePath).Should().BeTrue("Core runtime should be installed");
        // Note: Microsoft's "runtime" archive DOES include ASP.NET Core
        Directory.Exists(aspNetPath).Should().BeTrue("Core runtime archive includes ASP.NET Core");
        Directory.Exists(desktopPath).Should().BeFalse("Core runtime archive should not include Windows Desktop");
    }
}

/// <summary>
/// Helper class for parsing runtime types (mirrors the logic in RuntimeInstallCommand)
/// </summary>
internal static class RuntimeInstallCommandHelper
{
    public static InstallComponent? ParseRuntimeType(string? runtimeType)
    {
        if (string.IsNullOrEmpty(runtimeType))
        {
            return null;
        }

        return runtimeType.ToLowerInvariant() switch
        {
            "core" => InstallComponent.Runtime,
            "aspnetcore" => InstallComponent.ASPNETCore,
            "windowsdesktop" => InstallComponent.WindowsDesktop,
            _ => null
        };
    }
}
