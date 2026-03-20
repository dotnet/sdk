// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for install path validation logic in <see cref="InstallWorkflow"/>.
/// Regression coverage: the --untracked flag must bypass the "untracked artifacts" check
/// so that users can install to paths with existing .NET artifacts not in the manifest.
/// </summary>
public class InstallWorkflowTests
{
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

    /// <summary>
    /// Regression test: when --untracked is specified, ValidateInstallPath must NOT call
    /// ValidateNoUntrackedArtifacts. This test verifies that ValidateNoUntrackedArtifacts
    /// DOES throw for the same path (proving the guard in ValidateInstallPath is the only
    /// thing protecting --untracked installs from this error).
    /// If this test ever stops throwing, the guard is no longer needed — but if someone
    /// removes the guard and this test still throws, --untracked installs will break.
    /// </summary>
    [Fact]
    public void ValidateNoUntrackedArtifacts_ThrowsWithSharedDir_ProvingUntrackedGuardIsNeeded()
    {
        using var testEnv = new TestEnvironment();
        Directory.CreateDirectory(Path.Combine(testEnv.InstallPath, "shared"));

        var act = () => InstallWorkflow.ValidateNoUntrackedArtifacts(testEnv.InstallPath, testEnv.ManifestPath);

        act.Should().Throw<DotnetInstallException>(
            "the untracked-artifacts check must throw when .NET artifacts exist — " +
            "the --untracked flag relies on the caller skipping this method entirely");
    }

    #endregion
}
