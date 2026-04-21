// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

/// <summary>
/// Detects the user's current shell and resolves the matching <see cref="IEnvShellProvider"/>.
/// </summary>
public static class ShellDetection
{
    /// <summary>
    /// The list of shell providers supported by dotnetup.
    /// Revisit the generated script/comment helpers before adding a new shell here,
    /// since profile blocks assume the comment and quoting behavior of these shells.
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
    /// Looks up a shell provider by its command-line argument name (for example, "bash", "zsh", or "pwsh").
    /// </summary>
    internal static IEnvShellProvider? GetShellProviderByName(string shellName)
    {
        if (string.IsNullOrWhiteSpace(shellName))
        {
            return null;
        }

        return s_shellMap.GetValueOrDefault(shellName);
    }

    internal static string GetCurrentShellDisplayName()
        => Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";

    internal static string GetUnsupportedShellMessage()
        => $"Unable to detect a supported shell. SHELL={GetCurrentShellDisplayName()}. Supported shells: {string.Join(", ", s_supportedShells.Select(s => s.ArgumentName))}. You can specify one explicitly with --shell.";

    /// <summary>
    /// Resolves a shell provider from either a shell name or the path to a shell executable.
    /// </summary>
    internal static IEnvShellProvider? ResolveShellProvider(string shellPathOrName)
    {
        if (string.IsNullOrWhiteSpace(shellPathOrName))
        {
            return null;
        }

        var provider = GetShellProviderByName(shellPathOrName);
        if (provider is not null)
        {
            return provider;
        }

        var resolvedShellPath = Microsoft.DotNet.NativeWrapper.FileInterop.ResolveRealPath(shellPathOrName) ?? shellPathOrName;
        var normalizedShellPath = resolvedShellPath.Replace('\\', '/');
        var normalizedShellName = Path.GetFileNameWithoutExtension(normalizedShellPath);
        return GetShellProviderByName(normalizedShellName);
    }

    /// <summary>
    /// Checks whether a shell argument name is supported.
    /// </summary>
    internal static bool IsSupported(string shellName)
        => GetShellProviderByName(shellName) is not null;

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

        return ResolveShellProvider(shellPath);
    }

    internal static IEnvShellProvider GetCurrentShellProviderOrThrow(IEnvShellProvider? shellProvider = null)
        => shellProvider ?? GetCurrentShellProvider()
            ?? throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                GetUnsupportedShellMessage());
}
