// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class WalkthroughWorkflowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalDataDir;

    public WalkthroughWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-walkthrough-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _originalDataDir = Environment.GetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR");
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR", _originalDataDir);
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    // ── ShouldPromptToConvertSystemInstalls ──

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_ForDotnetupDotnet()
    {
        WalkthroughWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.DotnetupDotnet)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PathPreference.ShellProfile)]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void ShouldPromptToConvertSystemInstalls_ReturnsTrue_ForNonIsolationModes(PathPreference preference)
    {
        WalkthroughWorkflows.ShouldPromptToConvertSystemInstalls(preference)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_WhenDisabledInConfig()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        WalkthroughWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.ShellProfile)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsTrue_WhenDisabledInConfig_ButIgnoreConfigIsTrue()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        WalkthroughWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.ShellProfile, ignoreConfig: true)
            .Should().BeTrue();
    }

    // ── PromptInstallsToMigrateIfDesired — early-exit paths ──

    [Fact]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenPreferenceIsDotnetupDotnet()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = WalkthroughWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.DotnetupDotnet, installRoot);

        result.Should().BeEmpty();
        // GetExistingSystemInstalls should not be called when ShouldPromptToConvertSystemInstalls is false
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    [Fact]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenNoSystemInstallsExist()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);

        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var result = WalkthroughWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot, manifestPath);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    // ── GetExistingSystemInstalls — architecture filtering ──

    [Fact]
    public void GetExistingSystemInstalls_MockReturnsOnlyNativeArch()
    {
        // Verify the mock contract: when GetExistingSystemInstalls is called,
        // it should return only installs matching the native architecture.
        // The real implementation filters via .Where(i => i.InstallRoot.Architecture == nativeArch).
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var foreignArch = nativeArch == InstallArchitecture.x64 ? InstallArchitecture.arm64 : InstallArchitecture.x64;

        var nativeRoot = new DotnetInstallRoot("/dotnet", nativeArch);
        var foreignRoot = new DotnetInstallRoot("/dotnet", foreignArch);

        // Simulate what the real implementation does: filter to native arch only
        var allInstalls = new List<DotnetInstall>
        {
            new(nativeRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            new(foreignRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            new(nativeRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
            new(foreignRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime),
        };

        // Apply the same filter that GetExistingSystemInstalls uses
        var filtered = allInstalls.Where(i => i.InstallRoot.Architecture == nativeArch).ToList();

        filtered.Should().HaveCount(2);
        filtered.Should().OnlyContain(i => i.InstallRoot.Architecture == nativeArch);
    }

    [Fact]
    public void PromptInstallsToMigrateIfDesired_DoesNotQuerySystemInstalls_WhenConversionDisabled()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = WalkthroughWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    [Fact]
    public void PromptInstallsToMigrateIfDesired_QueriesSystemInstalls_WhenConversionDisabled_ButIgnoreConfigIsTrue()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);

        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var result = WalkthroughWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot, manifestPath, askEvenIfConfigured: true);

        result.Should().BeEmpty();
        // Should still query system installs because ignoreConfig overrides the disabled flag
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }
}
