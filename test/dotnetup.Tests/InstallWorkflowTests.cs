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

    #endregion
}
