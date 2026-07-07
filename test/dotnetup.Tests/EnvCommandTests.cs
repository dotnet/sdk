// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Behavioral tests for <see cref="EnvSetCommand"/>, <see cref="EnvClearCommand"/>, and
/// <see cref="EnvShowCommand"/>. Focus is on what each command persists to the config and which
/// underlying environment APIs it invokes — drift-detection branches that depend on the real
/// Windows registry / arbitrary host shell profiles are intentionally not asserted because they
/// cannot be sandboxed cleanly in CI.
/// </summary>
[TestClass]
public class EnvCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MockDotnetInstallManager _env;
    private readonly FakeEnvironmentStateInspector _inspector = new();

    public EnvCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-env-cmd-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // The override is thread-local ([ThreadStatic] in DotnetupPaths). Each xunit test case runs
        // start-to-finish on a single thread, so parallel execution across tests doesn't cross-
        // contaminate. (Thread-local state would not flow across async continuations, but these
        // tests are synchronous.)
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);

        _env = new MockDotnetInstallManager(defaultInstallPath: Path.Combine(_tempDir, "dotnet"));
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ── EnvSetCommand ──

    [TestMethod]
    public void EnvSet_None_PersistsConfig()
    {
        // Pass --shell so the dotnetup-only profile write (None still keeps dotnetup on PATH) does
        // not depend on host shell auto-detection, which is unavailable on some CI agents.
        var parseResult = Parser.Parse(["env", "set", "none", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        // dotnetup-on-PATH defaults to on when not specified and not previously stored.
        config.DotnetupOnPath.Should().BeTrue();
        // None + dotnetup-on-PATH still wires the dotnetup PATH entry (idempotent) but no dotnet env vars.
        _env.ApplyEnvironmentModificationsCallCount.Should().Be(0);
        _env.LastDotnetupOnUserPathEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void EnvSet_DotnetupOnPathOff_PersistsAndRemoves()
    {
        var parseResult = Parser.Parse(["env", "set", "none", "--dotnetup-on-path", "false"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void EnvSet_DotnetupOnPathOnly_LeavesStoredModeUnchanged()
    {
        // Pre-existing config: shell mode, dotnetup on. Change only --dotnetup-on-path false.
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "set", "--dotnetup-on-path", "false", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.Shell);   // mode preserved (re-sync)
        config.DotnetupOnPath.Should().BeFalse();          // only this changed
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void EnvSet_Everywhere_OnNonWindows_RejectedByParser()
    {
        var parseResult = Parser.Parse(["env", "set", "everywhere"]);

        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Should().Contain(e => e.Message.Contains("Windows", StringComparison.Ordinal));
        DotnetupConfig.Read().Should().BeNull();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void EnvSet_Everywhere_FromNone_OnWindows_AppliesAndPersists()
    {
        var parseResult = Parser.Parse(["env", "set", "everywhere"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.Everywhere);
        config.DotnetupOnPath.Should().BeTrue();
        _env.ApplyEnvironmentModificationsUserCallCount.Should().Be(1);
        _env.ApplyEnvironmentModificationsSystemCallCount.Should().Be(0);
        _env.ApplyTerminalProfileModificationsCallCount.Should().Be(1);
    }

    [TestMethod]
    public void EnvSet_NoModeAndNoStoredConfig_Fails()
    {
        var parseResult = Parser.Parse(["env", "set"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvSetCommand(parseResult, _env, _inspector).Execute();

        // Nothing to apply → DotnetInstallException → exit code 1.
        exitCode.Should().Be(1);
        DotnetupConfig.Read().Should().BeNull();
    }

    // ── EnvClearCommand ──

    [TestMethod]
    public void EnvClear_RemovesEverythingAndPersists()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "clear", "--shell", "bash"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvClearCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
        var config = DotnetupConfig.Read();
        config!.AccessMode.Should().Be(DotnetAccessMode.None);
        config.DotnetupOnPath.Should().BeFalse();
        _env.LastDotnetupOnUserPathEnabled.Should().BeFalse();
    }

    // ── EnvShowCommand ──

    [TestMethod]
    public void EnvShow_NoConfig_ExitCodeZero()
    {
        var parseResult = Parser.Parse(["env", "show"]);
        parseResult.Errors.Should().BeEmpty();

        int exitCode = new EnvShowCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public void EnvShow_ConfiguredNone_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.None, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
    }

    [TestMethod]
    public void EnvShow_ConfiguredShell_ExitCodeZero()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell, DotnetupOnPath = true });

        var parseResult = Parser.Parse(["env", "show"]);
        int exitCode = new EnvShowCommand(parseResult, _env, _inspector).Execute();

        exitCode.Should().Be(0);
    }
}
