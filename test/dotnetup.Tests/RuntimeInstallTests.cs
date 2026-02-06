// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
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

    #region Component Spec Parsing Tests

    [Theory]
    [InlineData(null, InstallComponent.Runtime, null)]
    [InlineData("", InstallComponent.Runtime, null)]
    [InlineData("10.0.1", InstallComponent.Runtime, "10.0.1")]
    [InlineData("latest", InstallComponent.Runtime, "latest")]
    [InlineData("9.0", InstallComponent.Runtime, "9.0")]
    [InlineData("runtime@10.0.1", InstallComponent.Runtime, "10.0.1")]
    [InlineData("runtime@latest", InstallComponent.Runtime, "latest")]
    [InlineData("aspnetcore@10.0.1", InstallComponent.ASPNETCore, "10.0.1")]
    [InlineData("aspnetcore@9.0", InstallComponent.ASPNETCore, "9.0")]
    [InlineData("ASPNETCORE@10.0.1", InstallComponent.ASPNETCore, "10.0.1")]
    [InlineData("windowsdesktop@10.0.1", InstallComponent.WindowsDesktop, "10.0.1")]
    [InlineData("WindowsDesktop@9.0", InstallComponent.WindowsDesktop, "9.0")]
    public void ComponentSpecParsing_ValidSpecs(string? spec, InstallComponent expectedComponent, string? expectedVersion)
    {
        var (component, version, error) = RuntimeInstallCommand.ParseComponentSpec(spec);

        error.Should().BeNull();
        component.Should().Be(expectedComponent);
        version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("invalid@10.0.1", "invalid")]
    [InlineData("sdk@10.0.1", "sdk")]
    [InlineData("unknown@latest", "unknown")]
    public void ComponentSpecParsing_InvalidComponent_ReturnsError(string spec, string invalidComponent)
    {
        var (_, _, error) = RuntimeInstallCommand.ParseComponentSpec(spec);

        error.Should().NotBeNull();
        error.Should().Contain(invalidComponent);
    }

    [Theory]
    [InlineData("aspnetcore@")]
    [InlineData("runtime@")]
    [InlineData("windowsdesktop@")]
    public void ComponentSpecParsing_MissingVersion_ReturnsError(string spec)
    {
        var (_, _, error) = RuntimeInstallCommand.ParseComponentSpec(spec);

        error.Should().NotBeNull();
        error.Should().Contain("Version is required");
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

    #region Parser Tests

    [Fact]
    public void Parser_RuntimeInstallWithoutArgs_NoErrors()
    {
        // Now valid - installs latest core runtime
        var parseResult = Parser.Parse(["runtime", "install"]);
        parseResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("9.0")]
    [InlineData("latest")]
    [InlineData("10.0.1")]
    [InlineData("aspnetcore@9.0")]
    [InlineData("windowsdesktop@10.0.1")]
    [InlineData("runtime@latest")]
    public void Parser_RuntimeInstallWithValidComponentSpec_NoErrors(string componentSpec)
    {
        var parseResult = Parser.Parse(["runtime", "install", componentSpec]);
        parseResult.Errors.Should().BeEmpty();
    }

    #endregion
}
