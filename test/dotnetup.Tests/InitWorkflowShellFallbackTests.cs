// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Covers <see cref="InitWorkflowDefaults.GetDefaultPathPreference"/>'s no-shell isolation
/// fallback, which depends on the <c>SHELL</c> environment variable on non-Windows. These tests
/// mutate <c>SHELL</c>, so they run in a serialized collection to avoid races with other tests.
/// </summary>
[Collection("DotnetupEnvironmentMutationTests")]
public class InitWorkflowShellFallbackTests
{
    private const string ShellEnvVar = "SHELL";

    /// <summary>
    /// On non-Windows, when SHELL points at an unsupported shell (so auto-detection fails),
    /// the default falls back to isolation mode rather than terminal-profile mode.
    /// </summary>
    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
    public void GetDefaultPathPreference_FallsBackToIsolation_WhenShellUnsupported()
    {
        RunWithShell("/nonexistent/not-a-real-shell", () =>
            InitWorkflowDefaults.GetDefaultPathPreference(shellProvider: null)
                .Should().Be(PathPreference.DotnetupDotnet));
    }

    /// <summary>
    /// On non-Windows, when SHELL points at a supported shell, the default is terminal-profile
    /// mode. Paired with the unsupported case above this proves the fallback is driven by shell
    /// detection rather than being a constant.
    /// </summary>
    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
    public void GetDefaultPathPreference_ReturnsShellProfile_WhenShellSupported()
    {
        RunWithShell("/bin/bash", () =>
            InitWorkflowDefaults.GetDefaultPathPreference(shellProvider: null)
                .Should().Be(PathPreference.ShellProfile));
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
