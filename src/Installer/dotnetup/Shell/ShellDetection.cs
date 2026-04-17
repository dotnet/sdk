// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

/// <summary>
/// Detects the user's current shell and resolves the matching <see cref="IEnvShellProvider"/>.
/// </summary>
public static class ShellDetection
{
    /// <summary>
    /// The list of shell providers supported by dotnetup.
    /// </summary>
    internal static readonly IEnvShellProvider[] s_supportedShells =
    [
        new BashEnvShellProvider(),
        new ZshEnvShellProvider(),
        new PowerShellEnvShellProvider()
    ];

    private static readonly Dictionary<string, IEnvShellProvider> s_shellMap =
        s_supportedShells.ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Looks up a shell provider by its argument name (e.g., "bash", "zsh", "pwsh").
    /// </summary>
    internal static IEnvShellProvider? GetShellProvider(string shellName)
    {
        if (string.IsNullOrWhiteSpace(shellName))
        {
            return null;
        }

        if (s_shellMap.TryGetValue(shellName, out var provider))
        {
            return provider;
        }

        var resolvedShellPath = Microsoft.DotNet.NativeWrapper.FileInterop.ResolveRealPath(shellName) ?? shellName;
        var normalizedShellName = Path.GetFileNameWithoutExtension(resolvedShellPath);
        return s_shellMap.GetValueOrDefault(normalizedShellName);
    }

    /// <summary>
    /// Checks whether a shell name is supported.
    /// </summary>
    internal static bool IsSupported(string shellName)
        => GetShellProvider(shellName) is not null;

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

        return GetShellProvider(shellPath);
    }
}
