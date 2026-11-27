// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal enum ShellStartupFileKind
{
    Interactive,
    Login,
    ZshEnvironment,
}

internal sealed record ShellPathInstruction(
    string ShellName,
    string StartupFile,
    ShellStartupFileKind StartupFileKind,
    string AppendCommand,
    string PathCommand,
    bool UsesPosixReporting,
    string StartupFileArgument = null,
    string PathCommandArgument = null);

internal static class ShellPathInstructions
{
    public static ShellPathInstruction Create(
        ShellType shell,
        string shellName,
        string toolsPath,
        IEnvironmentProvider environmentProvider,
        IFile fileSystem)
    {
        return shell switch
        {
            ShellType.PowerShell => CreatePowerShell(shellName, toolsPath),
            ShellType.Nushell => CreateNushell(shellName, toolsPath),
            ShellType.Fish => CreateFish(toolsPath, environmentProvider),
            ShellType.Bash or ShellType.Sh or ShellType.Dash or ShellType.Zsh =>
                CreatePosix(shell, toolsPath, environmentProvider, fileSystem),
            _ => null,
        };
    }

    private static ShellPathInstruction CreatePowerShell(string shellName, string toolsPath)
    {
        var command = $"$env:PATH += [System.IO.Path]::PathSeparator + {Quote(ShellType.PowerShell, toolsPath)}";
        // Resolve profiles in the user's shell, including host-specific and XDG overrides.
        // File APIs preserve literal backslashes on Unix, unlike PowerShell's filesystem provider.
        var appendCommand = "$dotnetProfile = if ([System.IO.File]::Exists($PROFILE.CurrentUserAllHosts)) { $PROFILE.CurrentUserAllHosts } " +
            "elseif ([System.IO.File]::Exists($PROFILE)) { $PROFILE } else { $PROFILE }; " +
            "[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($dotnetProfile)) | Out-Null; " +
            $"[System.IO.File]::AppendAllText($dotnetProfile, \"`n\" + {Quote(ShellType.PowerShell, command)} + \"`n\")";
        return new(shellName, "$PROFILE.CurrentUserAllHosts / $PROFILE", ShellStartupFileKind.Interactive, appendCommand, command, false);
    }

    private static ShellPathInstruction CreateNushell(string shellName, string toolsPath)
    {
        var command = $"$env.PATH = ($env.PATH | append {Quote(ShellType.Nushell, toolsPath)})";
        // These variables reflect --env-config/--config and Nushell's configuration directory.
        var appendCommand = "let dotnet_profile = (if ($nu.config-path | path exists) { $nu.config-path } " +
            "else if ($nu.env-path | path exists) { $nu.env-path } else { $nu.config-path }); " +
            "mkdir ($dotnet_profile | path dirname); " +
            $"(\"\\n\" + {Quote(ShellType.Nushell, command)} + \"\\n\") | save --append --raw $dotnet_profile";
        return new(shellName, "$nu.config-path / $nu.env-path", ShellStartupFileKind.Interactive, appendCommand, command, false);
    }

    private static ShellPathInstruction CreateFish(string toolsPath, IEnvironmentProvider environmentProvider)
    {
        var home = environmentProvider.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        var xdgConfigHome = environmentProvider.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var directory = Path.Combine(
            !string.IsNullOrEmpty(xdgConfigHome) && Path.IsPathFullyQualified(xdgConfigHome)
                ? xdgConfigHome : Path.Combine(home, ".config"), "fish");
        var startupFile = Path.Combine(directory, "config.fish");
        var command = $"fish_add_path --append --path {Quote(ShellType.Fish, toolsPath)}";
        var appendCommand = $"mkdir -p -- {Quote(ShellType.Fish, directory)}; " +
            $"printf '\\n%s\\n' {Quote(ShellType.Fish, command)} >> {Quote(ShellType.Fish, startupFile)}";
        return new("fish", startupFile, ShellStartupFileKind.Interactive, appendCommand, command, false);
    }

    private static ShellPathInstruction CreatePosix(
        ShellType shell,
        string toolsPath,
        IEnvironmentProvider environmentProvider,
        IFile fileSystem)
    {
        var home = environmentProvider.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        string startupFile;
        ShellStartupFileKind startupFileKind;
        if (shell == ShellType.Bash)
        {
            startupFile = new[] { ".bash_profile", ".bash_login", ".profile" }
                .Select(file => Path.Combine(home, file))
                .FirstOrDefault(fileSystem.Exists);
            if (startupFile == null && fileSystem.Exists(Path.Combine(home, ".bashrc")))
            {
                startupFile = Path.Combine(home, ".bashrc");
                startupFileKind = ShellStartupFileKind.Interactive;
            }
            else
            {
                startupFile ??= Path.Combine(home, ".profile");
                startupFileKind = ShellStartupFileKind.Login;
            }
        }
        else if (shell == ShellType.Zsh)
        {
            var zdotdir = environmentProvider.GetEnvironmentVariable("ZDOTDIR");
            // Zsh uses HOME only when ZDOTDIR is unset; an empty ZDOTDIR refers to the root directory.
            var directory = zdotdir == null ? home : zdotdir + "/";
            startupFile = new[] { ".zshrc", ".zprofile", ".zlogin", ".zshenv" }
                .Select(file => Path.Combine(directory, file))
                .FirstOrDefault(fileSystem.Exists) ?? Path.Combine(directory, ".zshrc");
            startupFileKind = Path.GetFileName(startupFile) switch
            {
                ".zprofile" or ".zlogin" => ShellStartupFileKind.Login,
                ".zshenv" => ShellStartupFileKind.ZshEnvironment,
                _ => ShellStartupFileKind.Interactive,
            };
        }
        else
        {
            startupFile = Path.Combine(home, ".profile");
            startupFileKind = ShellStartupFileKind.Login;
        }

        var command = $"export PATH=\"$PATH\":{Quote(shell, toolsPath)}";
        return new(
            ShellInfo.GetCanonicalName(shell),
            startupFile,
            startupFileKind,
            $"printf '\\n%s\\n' {Quote(shell, command)} >> {Quote(shell, startupFile)}",
            command,
            true,
            Quote(shell, startupFile),
            Quote(shell, command));
    }

    private static string Quote(ShellType shell, string value) => ShellInfo.QuoteLiteralArgument(shell, value);
}
