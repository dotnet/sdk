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

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for install path validation, repository channel resolution, and onboarding in <see cref="InstallWorkflow"/>.
/// Regression coverage: the --untracked flag must bypass the "untracked artifacts" check
/// so that users can install to paths with existing .NET artifacts not in the manifest.
/// </summary>
[TestClass]
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

    [TestMethod]
    [DataRow(null, "10.0.1xx")]
    [DataRow("patch", "10.0.1xx")]
    [DataRow("feature", "10.0")]
    [DataRow("minor", "10")]
    [DataRow("major", "latest")]
    [DataRow("disable", "10.0.103")]
    public void ResolveChannel_UsesGlobalJsonConstraints(string? policy, string expectedChannel)
    {
        var path = Path.Combine(_tempDir, "global.json");
        var policyProperty = policy is null ? "" : $""","rollForward":"{policy}" """;
        File.WriteAllText(path, $$$"""{"sdk":{"version":"10.0.103","allowPrerelease":false{{{policyProperty}}}}}""");

        var (channel, fromFile) = InstallWorkflow.ResolveChannel(
            InstallComponent.SDK, null, new GlobalJsonInfo { GlobalJsonPath = path });

        fromFile.Should().BeTrue();
        channel.Name.Should().Be(expectedChannel);
        channel.MinimumVersion!.ToString().Should().Be("10.0.103");
        channel.AllowPrerelease.Should().BeFalse();
    }

    [TestMethod]
    public void ResolveChannel_MalformedGlobalJsonReportsContextResolutionFailure()
    {
        var path = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(path, "{broken");

        var resolve = () => InstallWorkflow.ResolveChannel(
            InstallComponent.SDK, null, new GlobalJsonInfo { GlobalJsonPath = path });

        resolve.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
    }

    #region ValidateNoUntrackedArtifacts

    [TestMethod]
    public void ValidateNoUntrackedArtifacts_ThrowsWhenPathHasArtifactsNotInManifest()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "sdk"));

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(testEnv.InstallPath, testEnv.ManifestPath);

        act.Should().Throw<DotnetInstallException>()
            .WithMessage("*already contains a .NET installation that is not tracked*");
    }

    [TestMethod]
    public void ValidateNoUntrackedArtifacts_DoesNotThrowWhenPathIsEmpty()
    {
        using var testEnv = new TestEnvironment();

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(testEnv.InstallPath, testEnv.ManifestPath);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateNoUntrackedArtifacts_DoesNotThrowWhenPathDoesNotExist()
    {
        using var testEnv = new TestEnvironment();
        var nonExistentPath = Path.Combine(testEnv.TempRoot, "nonexistent");

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(nonExistentPath, testEnv.ManifestPath);

        act.Should().NotThrow();
    }

    #endregion

    #region First-use onboarding

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsTrue_ForInteractiveInstallWithoutConfig()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null)
            .Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenConfigAlreadyExists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });

        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForExplicitInstallPath()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: @"C:\custom\dotnet")
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForNonInteractiveInstall()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: false, installPath: null)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenMigrateFromSystemWasRequested()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(interactive: true, installPath: null, migrateFromSystem: true)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldPromptForStarterChannel_ReturnsTrue_ForFirstUseSdkInstallWithoutChannel()
    {
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.SDK, null)])
            .Should().BeTrue();
    }

    [TestMethod]
    public void ShouldPromptForStarterChannel_ReturnsFalse_ForFirstUseRuntimeInstallWithoutComponent()
    {
        // First-run `dotnetup runtime install` (no version/channel) should NOT trigger the
        // starter-channel prompt — that prompt is SDK-only and would otherwise silently turn
        // a runtime install into an SDK install.
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.Runtime, null)])
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldPromptForStarterChannel_ReturnsFalse_ForExplicitRuntimeInstall()
    {
        InstallWorkflow.ShouldPromptForStarterChannel(
            runOnboarding: true,
            [new MinimalInstallSpec(InstallComponent.Runtime, "9.0")])
            .Should().BeFalse();
    }

    #endregion
}
