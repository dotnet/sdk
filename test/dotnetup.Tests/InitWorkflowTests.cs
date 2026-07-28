// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
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

    [TestMethod]
    public void ShouldReplaceSystemConfiguration_ReturnsFalse_ForNone()
    {
        DotnetAccessModePolicy.ShouldReplaceSystemConfiguration(DotnetAccessMode.None)
            .Should().BeFalse();
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.Everywhere)]
    internal void ShouldReplaceSystemConfiguration_ReturnsTrue_ForPathReplacingModes(DotnetAccessMode accessMode)
    {
        DotnetAccessModePolicy.ShouldReplaceSystemConfiguration(accessMode)
            .Should().BeTrue();
    }

    [TestMethod]
    public void ShouldPromptToConvertSystemInstalls_ReturnsFalse_ForNone()
    {
        DotnetAccessModePolicy.ShouldPromptToConvertSystemInstalls(DotnetAccessMode.None)
            .Should().BeFalse();
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Everywhere)]
    internal void ShouldPromptToConvertSystemInstalls_ReturnsTrue_ForNonIsolationModes(DotnetAccessMode accessMode)
    {
        DotnetAccessModePolicy.ShouldPromptToConvertSystemInstalls(accessMode)
            .Should().BeTrue();
    }

    // ── PromptInstallsToMigrateIfDesired — early-exit paths ──

    [TestMethod]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenAccessModeIsNone()
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
            mock, DotnetAccessMode.None, installRoot);

        result.Should().BeEmpty();
        // GetExistingSystemInstalls should not be called when ShouldPromptToConvertSystemInstalls is false
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    [TestMethod]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenNoSystemInstallsExist()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls: []);

        string manifestPath = Path.Combine(_tempDir, "manifest.json");
        var result = InitWorkflows.PromptInstallsToMigrateIfDesired(
            mock, DotnetAccessMode.Shell, installRoot, manifestPath);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }

    [TestMethod]
    public void PromptInstallsToMigrateIfDesired_ReturnsEmpty_WhenInteractiveIsFalse()
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
            mock,
            DotnetAccessMode.Shell,
            installRoot,
            interactive: false);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    // ── GetExistingSystemInstalls — architecture filtering ──

    [TestMethod]
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

    [TestMethod]
    public void GetExistingSystemInstalls_DeduplicatesSameComponentVersionAndArch()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);

        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
                new DotnetInstall(installRoot, new ReleaseVersion("8.0.22"), InstallComponent.Runtime),
            ]);

        var result = mock.GetExistingSystemInstalls();

        result.Should().HaveCount(2);
        result.Should().ContainSingle(i => i.Component == InstallComponent.SDK && i.Version.ToString() == "10.0.100");
        result.Should().ContainSingle(i => i.Component == InstallComponent.Runtime && i.Version.ToString() == "8.0.22");
    }

    [TestMethod]
    public void FormatMigrationDisplayItems_IncludesArchitecture_WhenMultipleArchitecturesArePresent()
    {
        List<MigrationWorkflow.MigrationSelection> migrationSelections =
        [
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.x64),
            new(InstallComponent.SDK, new UpdateChannel("10.0.1xx"), new ReleaseVersion("10.0.100"), InstallArchitecture.arm64),
        ];

        var items = InitWorkflows.FormatMigrationDisplayItems(migrationSelections);

        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.Contains("10.0.1xx") && i.Contains("["));
    }

    // ── GetDefaultAccessMode ──

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void GetDefaultAccessMode_ReturnsShell_WhenShellProviderIsAvailableOnNonWindows()
    {
        InitWorkflowDefaults.GetDefaultAccessMode(new BashEnvShellProvider())
            .Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void GetDefaultAccessMode_ReturnsEverywhere_WhenShellProviderIsAvailableOnWindows()
    {
        InitWorkflowDefaults.GetDefaultAccessMode(new BashEnvShellProvider())
            .Should().Be(DotnetAccessMode.Everywhere);
    }

    // The no-shell isolation fallback (which reads the SHELL environment variable) is covered
    // deterministically by InitWorkflowShellFallbackTests, which mutates SHELL and therefore runs
    // in a serialized collection.

    // ── ResolveDefaultMigrations ──

    [TestMethod]
    public void ResolveDefaultMigrations_ReturnsEmpty_ForIsolationMode()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
            ]);

        var result = InitWorkflowDefaults.ResolveDefaultMigrations(
            mock, DotnetAccessMode.None, installRoot, manifestPath: null, existingRequests: null);

        result.Should().BeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(0);
    }

    [TestMethod]
    public void ResolveDefaultMigrations_ReturnsCandidates_ForShell()
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var installRoot = new DotnetInstallRoot(_tempDir, nativeArch);
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: _tempDir,
            existingSystemInstalls:
            [
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.100"), InstallComponent.SDK),
                new DotnetInstall(installRoot, new ReleaseVersion("10.0.0"), InstallComponent.Runtime),
            ]);

        var result = InitWorkflowDefaults.ResolveDefaultMigrations(
            mock, DotnetAccessMode.Shell, installRoot, manifestPath: null, existingRequests: null);

        result.Should().NotBeEmpty();
        mock.GetExistingSystemInstallsCallCount.Should().Be(1);
    }


}
