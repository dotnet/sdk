// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

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

    #region Manifest Checksum Tests

    [Fact]
    public void ManifestChecksum_WrittenOnCreate()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        _ = new DotnetupSharedManifest(testEnv.ManifestPath);

        var checksumPath = testEnv.ManifestPath + ".sha256";
        File.Exists(checksumPath).Should().BeTrue("checksum sidecar should be created with manifest");
        File.ReadAllText(checksumPath).Trim().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ManifestChecksum_UpdatedOnWrite()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var installRoot = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var checksumPath = testEnv.ManifestPath + ".sha256";
        var checksumBefore = File.ReadAllText(checksumPath).Trim();

        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifest.AddInstalledVersion(new DotnetInstall(installRoot, new ReleaseVersion("9.0.100"), InstallComponent.SDK));
        }

        var checksumAfter = File.ReadAllText(checksumPath).Trim();
        checksumAfter.Should().NotBe(checksumBefore, "checksum should change after add");
    }

    [Fact]
    public void ManifestCorrupted_WithValidChecksum_ThrowsLocalManifestCorrupted()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Write valid manifest, then corrupt the content without updating the checksum.
        // This simulates a scenario where dotnetup wrote the file but it got corrupted
        // (e.g., disk error, partial write).
        var checksumPath = testEnv.ManifestPath + ".sha256";
        var originalContent = File.ReadAllText(testEnv.ManifestPath);
        var originalChecksum = File.ReadAllText(checksumPath);

        // Write corrupt JSON that still matches the stored checksum (impossible naturally,
        // so instead: write corrupt JSON, then rewrite checksum of the corrupt content)
        var corruptContent = "NOT VALID JSON {{{";
        File.WriteAllText(testEnv.ManifestPath, corruptContent);

        // Compute and write checksum of corrupt content to simulate dotnetup having written it
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(corruptContent));
        File.WriteAllText(checksumPath, Convert.ToHexString(hash));

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.GetInstalledVersions().ToList());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestCorrupted,
            "checksum matches corrupt content → product error");
    }

    [Fact]
    public void ManifestCorrupted_WithMismatchedChecksum_ThrowsLocalManifestUserCorrupted()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Manifest was written by dotnetup (checksum exists), then user edits the file
        File.WriteAllText(testEnv.ManifestPath, "user broke this {[}");

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.GetInstalledVersions().ToList());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "checksum doesn't match user-edited content → user error");
    }

    [Fact]
    public void ManifestCorrupted_WithNoChecksum_ThrowsLocalManifestUserCorrupted()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);

        // Delete checksum file and corrupt manifest — simulates user-created manifest
        var checksumPath = testEnv.ManifestPath + ".sha256";
        File.Delete(checksumPath);
        File.WriteAllText(testEnv.ManifestPath, "garbage data");

        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var ex = Assert.Throws<DotnetInstallException>(() => manifest.GetInstalledVersions().ToList());
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.LocalManifestUserCorrupted,
            "no checksum file → assume external edit → user error");
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
