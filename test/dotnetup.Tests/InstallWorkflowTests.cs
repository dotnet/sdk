// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for install path validation logic in <see cref="InstallWorkflow"/>.
/// Regression coverage: the --untracked flag must bypass the "untracked artifacts" check
/// so that users can install to paths with existing .NET artifacts not in the manifest.
/// </summary>
[TestClass]
public class InstallWorkflowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeDotnetupPolicyStore _policyStore = new();
    private readonly DotnetupPolicy _policy;

    public InstallWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-installworkflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
        _policy = new DotnetupPolicy(_policyStore);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
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
        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: true, installPath: null)
            .Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenConfigAlreadyExists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });

        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: true, installPath: null)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForExplicitInstallPath()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: true, installPath: @"C:\custom\dotnet")
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_ForNonInteractiveInstall()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: false, installPath: null)
            .Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_WhenMigrateFromSystemWasRequested()
    {
        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: true, installPath: null, migrateFromSystem: true)
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ShouldRunFirstUseOnboarding_ReturnsFalse_InAdminMode()
    {
        UseAdminMode();

        InstallWorkflow.ShouldRunFirstUseOnboarding(ResolveAccessMode(), interactive: true, installPath: null)
            .Should().BeFalse();
    }

    #endregion

    #region Admin mode

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(new[] { "sdk", "install", "9.0.100" })]
    [DataRow(new[] { "sdk", "install", "9.0.100", "--untracked", "false" })]
    public void GenerateInstallRequests_AdminModeWithoutUntracked_ThrowsAdminModeRequiresUntracked(string[] arguments)
    {
        UseAdminMode();
        var workflow = new InstallWorkflow(new SdkInstallCommand(Parser.Parse(arguments), _policy));

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.SDK, "9.0.100")]));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeRequiresUntracked);
        ex.Message.Should().Contain("--untracked");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GenerateInstallRequests_AdminModeRuntimeInstallWithoutUntracked_ThrowsAdminModeRequiresUntracked()
    {
        UseAdminMode();
        var workflow = new InstallWorkflow(new RuntimeInstallCommand(Parser.Parse(["runtime", "install", "9.0.0"]), _policy));

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.Runtime, "9.0.0")]));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeRequiresUntracked);
        File.Exists(Path.Combine(_tempDir, "manifest.json")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GenerateInstallRequests_AdminModeWithUntracked_TargetsSystemRootWithInstallerBundle()
    {
        UseAdminMode();
        var workflow = new InstallWorkflow(new SdkInstallCommand(Parser.Parse(["sdk", "install", "9.0.100", "--untracked"]), _policy));
        var expectedRoot = new AdminInstallRootResolver().Resolve(InstallerUtilities.GetDefaultInstallArchitecture());

        // The admin root is under Program Files, which ValidateInstallPath would reject with AdminPathBlocked.
        var requests = workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.SDK, "9.0.100")]);

        var request = requests.Should().ContainSingle().Subject.Request;
        request.InstallRoot.Should().Be(expectedRoot);
        InstallPathClassifier.IsAdminInstallPath(request.InstallRoot.Path).Should().BeTrue();
        request.Options.Untracked.Should().BeTrue();
        request.Options.InstallMechanism.Should().Be(InstallMechanism.WindowsInstallerBundle);
        File.Exists(Path.Combine(_tempDir, "manifest.json")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void GenerateInstallRequests_AdminMode_GlobalJsonLocalPath_UsesUntrackedArchiveInstallThere()
    {
        UseAdminMode();
        string projectDir = WriteGlobalJson("""[".dotnet"]""");

        var resolved = GenerateSdkRequestFromGlobalJson(projectDir, ["sdk", "install", "--untracked"]);

        resolved.ResolvedVersion.ToString().Should().Be("9.0.100");
        resolved.Request.Options.InstallSource.Should().Be(InstallRequestSource.GlobalJson);
        resolved.Request.InstallRoot.Path.Should().Be(Path.Combine(projectDir, ".dotnet"));
        resolved.Request.Options.InstallMechanism.Should().Be(InstallMechanism.Archive);
        resolved.Request.Options.Untracked.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "manifest.json")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void GenerateInstallRequests_AdminMode_GlobalJsonLocalPath_RuntimeInstallUsesArchiveThere()
    {
        UseAdminMode();
        string projectDir = WriteGlobalJson("""[".dotnet"]""");
        var workflow = new InstallWorkflow(new RuntimeInstallCommand(Parser.Parse(["runtime", "install", "9.0.0", "--untracked"]), _policy));

        var request = RunInDirectory(projectDir, () =>
            workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.Runtime, "9.0.0")]))
            .Should().ContainSingle().Subject.Request;

        request.InstallRoot.Path.Should().Be(Path.Combine(projectDir, ".dotnet"));
        request.Options.InstallMechanism.Should().Be(InstallMechanism.Archive);
        request.Options.Untracked.Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void GenerateInstallRequests_AdminMode_GlobalJsonLocalPathWithoutUntracked_ThrowsAdminModeRequiresUntracked()
    {
        UseAdminMode();
        string projectDir = WriteGlobalJson("""[".dotnet"]""");
        var workflow = new InstallWorkflow(new SdkInstallCommand(Parser.Parse(["sdk", "install"]), _policy));

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() =>
            RunInDirectory(projectDir, () => workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.SDK, null)])));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeRequiresUntracked);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    [DataRow("""["$host$"]""")]
    [DataRow("""["$host$", ".dotnet"]""")]
    [DataRow("[]")]
    [DataRow(null)]
    public void GenerateInstallRequests_AdminMode_GlobalJsonWithoutLocalPath_UsesInstallerBundleAtSystemRoot(string? pathsJson)
    {
        UseAdminMode();
        string projectDir = WriteGlobalJson(pathsJson);

        var resolved = GenerateSdkRequestFromGlobalJson(projectDir, ["sdk", "install", "--untracked"]);

        resolved.ResolvedVersion.ToString().Should().Be("9.0.100");
        resolved.Request.Options.InstallSource.Should().Be(InstallRequestSource.GlobalJson);
        resolved.Request.InstallRoot.Should().Be(new AdminInstallRootResolver().Resolve(InstallerUtilities.GetDefaultInstallArchitecture()));
        resolved.Request.Options.InstallMechanism.Should().Be(InstallMechanism.WindowsInstallerBundle);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [ResourceLock(WellKnownResources.CurrentDirectory)]
    public void GenerateInstallRequests_AdminMode_GlobalJsonPathIsSystemLocation_UsesInstallerBundle()
    {
        UseAdminMode();
        string systemDotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        string projectDir = WriteGlobalJson($"[{System.Text.Json.JsonSerializer.Serialize(systemDotnet)}]");

        var resolved = GenerateSdkRequestFromGlobalJson(projectDir, ["sdk", "install", "--untracked"]);

        resolved.Request.InstallRoot.Should().Be(new AdminInstallRootResolver().Resolve(InstallerUtilities.GetDefaultInstallArchitecture()));
        resolved.Request.Options.InstallMechanism.Should().Be(InstallMechanism.WindowsInstallerBundle);
    }

    /// <summary>Writes a global.json pinning SDK 9.0.100 with the given <c>sdk.paths</c> JSON array (omitted when null).</summary>
    private string WriteGlobalJson(string? pathsJson)
    {
        string projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);
        string pathsProperty = pathsJson is null ? string.Empty : $",\n    \"paths\": {pathsJson}";
        File.WriteAllText(Path.Combine(projectDir, "global.json"), $$"""
            {
              "sdk": {
                "version": "9.0.100",
                "rollForward": "disable"{{pathsProperty}}
              }
            }
            """);
        return projectDir;
    }

    private ResolvedInstallRequest GenerateSdkRequestFromGlobalJson(string projectDir, string[] arguments)
    {
        var workflow = new InstallWorkflow(new SdkInstallCommand(Parser.Parse(arguments), _policy));
        return RunInDirectory(projectDir, () => workflow.GenerateInstallRequests([new MinimalInstallSpec(InstallComponent.SDK, null)]))
            .Should().ContainSingle().Subject;
    }

    private static T RunInDirectory<T>(string directory, Func<T> action)
    {
        string originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory;
            return action();
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private void UseAdminMode() => DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin });

    private EffectiveAccessMode ResolveAccessMode() => AccessModeResolver.Resolve();

    #endregion
}
