// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for install path validation logic in <see cref="InstallWorkflow"/>.
/// Regression coverage: the --untracked flag must bypass the "untracked artifacts" check
/// so that users can install to paths with existing .NET artifacts not in the manifest.
/// </summary>
public class InstallWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public InstallWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-installworkflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    #region ValidateNoUntrackedArtifacts

    [Fact]
    public void ValidateNoUntrackedArtifacts_ThrowsWhenPathHasArtifactsNotInManifest()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk"));

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(testEnv.InstallPath, testEnv.ManifestPath);

        act.Should().Throw<DotnetInstallException>()
            .WithMessage("*already contains a .NET installation that is not tracked*");
    }

    [Fact]
    public void ValidateNoUntrackedArtifacts_DoesNotThrowWhenPathIsEmpty()
    {
        using var testEnv = new TestEnvironment();

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(testEnv.InstallPath, testEnv.ManifestPath);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateNoUntrackedArtifacts_DoesNotThrowWhenPathDoesNotExist()
    {
        using var testEnv = new TestEnvironment();
        var nonExistentPath = Path.Combine(testEnv.TempRoot, "nonexistent");

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(nonExistentPath, testEnv.ManifestPath);

        act.Should().NotThrow();
    }

    #endregion

    #region First-use onboarding

    [Fact]
    public void ShouldRunFirstUseOnboarding_ReturnsTrue_ForInteractiveInstallWithoutConfig()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenConfigAlreadyExists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = PathPreference.ShellProfile });

        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForExplicitInstallPath()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: @"C:\custom\dotnet")
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForNonInteractiveInstall()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: false, installPath: null)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenMigrateFromSystemWasRequested()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null, migrateFromSystem: true)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldPromptForStarterChannel_ReturnsTrue_ForFirstUseSdkInstallWithoutChannel()
    {
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.SDK, null)])
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptForStarterChannel_ReturnsTrue_ForFirstUseRuntimeInstallWithoutComponent()
    {
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.Runtime, null)])
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldPromptForStarterChannel_ReturnsFalse_ForExplicitRuntimeInstall()
    {
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.Runtime, "9.0")])
            .Should().BeFalse();
    }

    #endregion
}
