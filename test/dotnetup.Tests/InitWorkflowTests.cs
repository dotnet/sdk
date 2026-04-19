// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class InitWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public InitWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    // ── ShouldPromptToConvertSystemInstalls ──

    [Fact]
    public void ShouldReplaceSystemConfiguration_ReturnsFalse_ForDotnetupDotnet()
    {
        InitWorkflows.ShouldReplaceSystemConfiguration(PathPreference.DotnetupDotnet)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void ShouldReplaceSystemConfiguration_ReturnsTrue_ForPathReplacingModes(PathPreference preference)
    {
        InitWorkflows.ShouldReplaceSystemConfiguration(preference)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_ForDotnetupDotnet()
    {
        InitWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.DotnetupDotnet)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(PathPreference.ShellProfile)]
    [InlineData(PathPreference.FullPathReplacement)]
    internal void ShouldPromptToConvertSystemInstalls_ReturnsTrue_ForNonIsolationModes(PathPreference preference)
    {
        InitWorkflows.ShouldPromptToConvertSystemInstalls(preference)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_WhenDisabledInConfig()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        InitWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.ShellProfile)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldPromptToConvertSystemInstalls_ReturnsTrue_WhenDisabledInConfig_ButIgnoreConfigIsTrue()
    {
        DotnetupConfig.Write(new DotnetupConfigData { DisableInstallConversion = true });

        InitWorkflows.ShouldPromptToConvertSystemInstalls(PathPreference.ShellProfile, ignoreConfig: true)
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

        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
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
        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot, manifestPath);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    [Fact]
    public void BaseConfigurationWalkthrough_PassesInstallRootToTerminalProfileModifications()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);
        var workflow = new InitWorkflows(mock, null!);

        workflow.BaseConfigurationWalkthrough(
            requests: [],
            primaryActionAfterConfigured: () => { },
            noProgress: true,
            interactive: false,
            shellProvider: new TestShellProvider());

        mock.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
        mock.LastDotnetRootForTerminalProfileModifications.Should().Be(_tempDir);
    }

    // ── GetExistingSystemInstalls — architecture filtering ──

    [Fact]
    public void GetExistingSystemInstalls_FiltersToNativeArchOnly()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var foreignArch = nativeArch == InstallArchitecture.x64 ? InstallArchitecture.arm64 : InstallArchitecture.x64;

        var nativeRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var foreignRoot = new DotnetInstallRoot(_tempDir, foreignArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(foreignRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(nativeRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
                new DotnetInstall(foreignRoot, new ReleaseVersion("9.0.0"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.InstallRoot.Architecture == nativeArch);
        // Verify descending sort order from the real filter
        result[0].Version.ToString().Should().BeOneOf("10.0.100", "10.0.0");
        result[0].Version.CompareTo(result[1].Version).Should().BeGreaterThanOrEqualTo(0);
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

        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
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
        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, PathPreference.ShellProfile, installRoot, manifestPath, askEvenIfConfigured: true);

        result.Should().BeEmpty();
        // Should still query system installs because ignoreConfig overrides the disabled flag
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    private sealed class TestShellProvider : IEnvShellProvider
    {
        public string ArgumentName => "test";
        public string Extension => "test";
        public string? HelpDescription => "Test shell provider";

        public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
            => string.Empty;

        public IReadOnlyList<string> GetProfilePaths()
            => [];

        public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
            => string.Empty;

        public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
            => string.Empty;
    }
}
