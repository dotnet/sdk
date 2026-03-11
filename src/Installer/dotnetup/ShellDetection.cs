// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Detects the user's current shell and resolves the matching <see cref="IEnvShellProvider"/>.
/// </summary>
public static class ShellDetection
{
    private static readonly Dictionary<string, IEnvShellProvider> s_shellMap =
        PrintEnvScriptCommandParser.s_supportedShells.ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the <see cref="IEnvShellProvider"/> for the user's current shell,
    /// or null if the shell cannot be detected or is not supported.
    /// </summary>
    public static IEnvShellProvider? GetCurrentShellProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            return s_shellMap.GetValueOrDefault("pwsh");
        }

        var shellPath = Environment.GetEnvironmentVariable("SHELL");
        if (shellPath is null)
        {
            return null;
        }

        var shellName = Path.GetFileName(shellPath);
        return s_shellMap.GetValueOrDefault(shellName);
    }
}
