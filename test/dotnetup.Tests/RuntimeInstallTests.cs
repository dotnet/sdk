// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
/// Unit tests for runtime installation functionality. E2E tests are in DnupE2Etest.cs.
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
    [InlineData("latest", InstallComponent.Runtime)]
    [InlineData("lts", InstallComponent.Runtime)]
    [InlineData("9.0", InstallComponent.Runtime)]
    [InlineData("latest", InstallComponent.ASPNETCore)]
    [InlineData("9.0", InstallComponent.ASPNETCore)]
    public void VersionResolution_ValidChannels_ReturnsVersion(string channel, InstallComponent component)
    {
        var version = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(channel), component);

        _log.WriteLine($"Channel '{channel}' for {component} resolved to: {version}");
        version.Should().NotBeNull();
    }

    [Theory]
    [InlineData("9.0.1xx", InstallComponent.Runtime)]
    [InlineData("9.0.1xx", InstallComponent.ASPNETCore)]
    [InlineData("9.0.1xx", InstallComponent.WindowsDesktop)]
    public void VersionResolution_FeatureBand_ReturnsNull(string featureBand, InstallComponent component)
    {
        var version = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(featureBand), component);

        _log.WriteLine($"Feature band '{featureBand}' for {component}: {version?.ToString() ?? "null"}");
        version.Should().BeNull("feature bands are SDK-specific");
    }

    [Fact]
    public void VersionResolution_SdkAndRuntime_DifferentVersions()
    {
        var resolver = new ChannelVersionResolver();
        var sdkVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.SDK);
        var runtimeVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.Runtime);

        _log.WriteLine($"SDK: {sdkVersion}, Runtime: {runtimeVersion}");
        sdkVersion.Should().NotBeNull();
        runtimeVersion.Should().NotBeNull();
        sdkVersion!.ToString().Should().NotBe(runtimeVersion!.ToString());
    }

    #endregion

    #region Component Type Mapping Tests

    [Theory]
    [InlineData("core", InstallComponent.Runtime)]
    [InlineData("CORE", InstallComponent.Runtime)]
    [InlineData("aspnetcore", InstallComponent.ASPNETCore)]
    [InlineData("AspNetCore", InstallComponent.ASPNETCore)]
    [InlineData("windowsdesktop", InstallComponent.WindowsDesktop)]
    [InlineData("WindowsDesktop", InstallComponent.WindowsDesktop)]
    public void ComponentTypeMapping_ValidTypes(string typeName, InstallComponent expected)
    {
        RuntimeInstallCommandHelper.ParseRuntimeType(typeName).Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("sdk")]
    [InlineData("")]
    [InlineData(null)]
    public void ComponentTypeMapping_InvalidTypes_ReturnsNull(string? typeName)
    {
        RuntimeInstallCommandHelper.ParseRuntimeType(typeName).Should().BeNull();
    }

    #endregion

    #region Manifest Tracking Tests

    [Fact]
    public void ManifestTracking_DifferentComponents_TrackedSeparately()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        // Add SDK, Runtime, ASPNETCore
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.ASPNETCore));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().HaveCount(3);
        installs.Should().Contain(i => i.Component == InstallComponent.SDK);
        installs.Should().Contain(i => i.Component == InstallComponent.Runtime);
        installs.Should().Contain(i => i.Component == InstallComponent.ASPNETCore);
    }

    [Fact]
    public void ManifestTracking_SameVersionDifferentComponent_BothTracked()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.0"), InstallComponent.ASPNETCore));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().HaveCount(2);
    }

    #endregion

    #region Uninstall Strategy Tests

    /// <summary>
    /// Core runtime can be safely uninstalled only when no SDK or ASPNETCore with same major.minor exists.
    /// </summary>
    private static bool CanSafelyUninstallCoreRuntime(IEnumerable<DotnetInstall> installs, ReleaseVersion version)
    {
        int majorMinor = version.Major * 100 + version.Minor;
        return !installs.Any(i =>
            (i.Component == InstallComponent.SDK || i.Component == InstallComponent.ASPNETCore) &&
            (i.Version.Major * 100 + i.Version.Minor) == majorMinor);
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_BlockedBySdk()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12")).Should().BeFalse();
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_BlockedByASPNETCore()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.ASPNETCore));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12")).Should().BeFalse();
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_AllowedWhenNoDependencies()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.12"), InstallComponent.Runtime));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("9.0.12")).Should().BeTrue();
    }

    [Fact]
    public void UninstallStrategy_CoreRuntime_DifferentMajorMinor_Allowed()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK));
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("10.0.2"), InstallComponent.Runtime));
        }

        IEnumerable<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            installs = manifest.GetInstalledVersions().ToList();
        }

        // Runtime 10.0.x can be uninstalled even with SDK 9.0.x present
        CanSafelyUninstallCoreRuntime(installs, new ReleaseVersion("10.0.2")).Should().BeTrue();
    }

    #endregion

    #region Parser Tests

    [Fact]
    public void Parser_RuntimeInstallWithoutType_HasErrors()
    {
        var parseResult = Parser.Parse(["runtime", "install"]);
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("core", "9.0")]
    [InlineData("aspnetcore", "latest")]
    [InlineData("windowsdesktop", "lts")]
    public void Parser_RuntimeInstallWithValidArgs_NoErrors(string runtimeType, string channel)
    {
        var parseResult = Parser.Parse(["runtime", "install", runtimeType, channel]);
        parseResult.Errors.Should().BeEmpty();
    }

    #endregion
}

/// <summary>
/// Helper for parsing runtime types (mirrors RuntimeInstallCommand logic).
/// </summary>
internal static class RuntimeInstallCommandHelper
{
    public static InstallComponent? ParseRuntimeType(string? runtimeType)
    {
        if (string.IsNullOrEmpty(runtimeType))
            return null;

        return runtimeType.ToLowerInvariant() switch
        {
            "core" => InstallComponent.Runtime,
            "aspnetcore" => InstallComponent.ASPNETCore,
            "windowsdesktop" => InstallComponent.WindowsDesktop,
            _ => null
        };
    }
}
