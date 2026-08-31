// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Covers <see cref="InitWorkflowDefaults.GetDefaultAccessMode"/>'s no-shell isolation
/// fallback, which depends on the <c>SHELL</c> environment variable on non-Windows. These tests
/// mutate <c>SHELL</c>, so they run in a serialized collection to avoid races with other tests.
/// </summary>
[TestClass]
public class InitWorkflowShellFallbackTests
{
    private const string ShellEnvVar = "SHELL";

    /// <summary>
    /// On non-Windows, when SHELL points at an unsupported shell (so auto-detection fails),
    /// the default falls back to isolation mode rather than terminal-profile mode.
    /// </summary>
    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void GetDefaultAccessMode_FallsBackToIsolation_WhenShellUnsupported()
    {
        RunWithShell("/nonexistent/not-a-real-shell", () =>
            InitWorkflowDefaults.GetDefaultAccessMode(shellProvider: null)
                .Should().Be(DotnetAccessMode.None));
    }

    /// <summary>
    /// On non-Windows, when SHELL points at a supported shell, the default is terminal-profile
    /// mode. Paired with the unsupported case above this proves the fallback is driven by shell
    /// detection rather than being a constant.
    /// </summary>
    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void GetDefaultAccessMode_ReturnsShellProfile_WhenShellSupported()
    {
        RunWithShell("/bin/bash", () =>
            InitWorkflowDefaults.GetDefaultAccessMode(shellProvider: null)
                .Should().Be(DotnetAccessMode.Shell));
    }

    private static void RunWithShell(string shellValue, Action assert)
    {
        string? original = Environment.GetEnvironmentVariable(ShellEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ShellEnvVar, shellValue);
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ShellEnvVar, original);
        }
    }
}
