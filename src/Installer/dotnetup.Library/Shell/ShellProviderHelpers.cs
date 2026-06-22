// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

internal static class ShellProviderHelpers
{
    internal static string GetDotnetupOnlyComment(string shellName)
        => $"# This {GetShellDisplayName(shellName)} script adds dotnetup to your PATH";

    // This text is emitted as a shell comment for the supported providers in ShellDetection.
    // Keep it on one line so an unusual install path can't break out of the comment block.
    internal static string GetEnvironmentConfigurationComment(string shellName, string dotnetInstallPath)
        => $"# This {GetShellDisplayName(shellName)} script configures the environment for .NET installed at {dotnetInstallPath.ReplaceLineEndings(" ")}";

    private static string GetShellDisplayName(string shellName)
        => shellName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ? "PowerShell" : shellName;

    internal static string EscapePosixPath(string path)
        => path.Replace("'", "'\\''", StringComparison.Ordinal);

    internal static string EscapePowerShellPath(string path)
        => path.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// Builds the explicit selection flags for an <c>env script</c> invocation baked into a
    /// profile entry or activation command. Always emits <c>--dotnet</c> and/or <c>--dotnetup</c>
    /// for the requested aspects so the generated call never relies on the command's no-flag
    /// default. Adds <c>--dotnet-install-path</c> only when dotnet is
    /// included and the path is non-default.
    /// </summary>
    internal static string GetCommandFlags(bool includeDotnet, bool includeDotnetup, string? dotnetInstallPath, Func<string, string> escapePath)
    {
        List<string> flags = [];

        if (includeDotnet)
        {
            flags.Add("--dotnet");
        }

        if (includeDotnetup)
        {
            flags.Add("--dotnetup");
        }

        if (includeDotnet && dotnetInstallPath is { Length: > 0 } installPath &&
            !DotnetupUtilities.PathsEqual(installPath, DotnetupPaths.DefaultDotnetInstallPath))
        {
            flags.Add($"--dotnet-install-path '{escapePath(installPath)}'");
        }

        return string.Join(" ", flags);
    }

    internal static string AppendArguments(string command, string flags)
        => string.IsNullOrEmpty(flags) ? command : $"{command} {flags}";

    internal static string BuildPosixActivationCommand(string dotnetupPath)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        return $"eval \"$('{escapedPath}' env script)\"";
    }

    internal static string BuildPosixProfileEntry(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        var command = BuildPosixPrintEnvCommand(dotnetupPath, shellName, flags);

        return $$"""
            if [ -x '{{escapedPath}}' ]; then
                eval "$({{command}})"
            fi
            """;
    }

    internal static string BuildPowerShellActivationCommand(string dotnetupPath)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);
        return $"Invoke-Expression (& '{escapedPath}' env script | Out-String)";
    }

    internal static string BuildPowerShellProfileEntry(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);
        var command = BuildPowerShellPrintEnvCommand(dotnetupPath, shellName, flags);
        var activationBlock = IndentLines(BuildPowerShellGuardedInvocationBlock(command), "    ");

        return $$"""
            if (Test-Path -LiteralPath '{{escapedPath}}' -PathType Leaf)
            {
                {{activationBlock}}
            }
            """;
    }

    private static string BuildPosixPrintEnvCommand(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        return AppendArguments($"'{escapedPath}' env script --shell {shellName}", flags);
    }

    private static string BuildPowerShellPrintEnvCommand(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);
        return AppendArguments($"& '{escapedPath}' env script --shell {shellName}", flags);
    }

    private static string BuildPowerShellGuardedInvocationBlock(string command)
    {
        return $$"""
            $dotnetupScript = {{command}} | Out-String
            if (-not [string]::IsNullOrWhiteSpace($dotnetupScript))
            {
                Invoke-Expression $dotnetupScript
            }
            """;
    }

    private static string IndentLines(string text, string indentation)
        => indentation + text.ReplaceLineEndings(Environment.NewLine + indentation);

    internal static string BuildPosixPathExport(string escapedPath, string dotnetupDir, bool includeDotnet)
    {
        // The two axes are independent: the dotnetup directory is added whenever it is supplied
        // (regardless of includeDotnet), and the managed dotnet is added when includeDotnet is set.
        // Managed paths go first so the shell resolves dotnet/dotnetup from the selected install.
        var entries = new List<string>();

        if (!string.IsNullOrWhiteSpace(dotnetupDir))
        {
            entries.Add($"'{EscapePosixPath(dotnetupDir)}'");
        }

        if (includeDotnet)
        {
            entries.Add($"'{escapedPath}'");
        }

        return entries.Count == 0
            ? string.Empty
            : $"export PATH={string.Join(":", entries)}:$PATH";
    }

    internal static string BuildPowerShellPathExport(string escapedPath, string dotnetupDir, bool includeDotnet)
    {
        // The two axes are independent: the dotnetup directory is added whenever it is supplied
        // (regardless of includeDotnet), and the managed dotnet is added when includeDotnet is set.
        // Managed paths go first so the shell resolves dotnet/dotnetup from the selected install.
        var entries = new List<string>();

        if (!string.IsNullOrWhiteSpace(dotnetupDir))
        {
            entries.Add($"'{EscapePowerShellPath(dotnetupDir)}'");
        }

        if (includeDotnet)
        {
            entries.Add($"'{escapedPath}'");
        }

        return entries.Count == 0
            ? string.Empty
            : $"$env:PATH = {string.Join(" + [IO.Path]::PathSeparator + ", entries)} + [IO.Path]::PathSeparator + $env:PATH";
    }

    internal static string GetDotnetupExecutablePathOrThrow()
    {
        return Environment.ProcessPath
            ?? throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to determine the full path to the running dotnetup executable.");
    }

    internal static string GetDotnetupDirectoryOrThrow()
    {
        var dotnetupPath = GetDotnetupExecutablePathOrThrow();
        var dotnetupDir = Path.GetDirectoryName(dotnetupPath);

        if (string.IsNullOrWhiteSpace(dotnetupDir))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"Unable to determine the directory containing '{dotnetupPath}'.");
        }

        return dotnetupDir;
    }

    internal static string GetUserHomeDirectoryOrThrow()
    {
        var envVarName = OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME";
        var home = Environment.GetEnvironmentVariable(envVarName);

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"Unable to determine the current user's home directory. The {envVarName} environment variable is not set.");
        }

        var fullPath = Path.GetFullPath(home);
        if (!Directory.Exists(fullPath))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"The current user's home directory '{fullPath}' does not exist.");
        }

        EnsureDirectoryWritable(fullPath, "home directory");
        return fullPath;
    }

    internal static string GetZshConfigurationDirectoryOrThrow()
    {
        var zdotdir = Environment.GetEnvironmentVariable("ZDOTDIR");
        if (string.IsNullOrWhiteSpace(zdotdir))
        {
            return GetUserHomeDirectoryOrThrow();
        }

        var fullPath = Path.GetFullPath(zdotdir);
        EnsureDirectoryWritable(fullPath, "ZDOTDIR", createIfMissing: true);
        return fullPath;
    }

    internal static string GetPowerShellProfileDirectoryOrThrow()
    {
        var profileDir = Path.Combine(GetUserHomeDirectoryOrThrow(), ".config", "powershell");
        EnsureDirectoryWritable(profileDir, "PowerShell profile directory", createIfMissing: true);
        return profileDir;
    }

    private static void EnsureDirectoryWritable(string path, string description, bool createIfMissing = false)
    {
        string tempFile = Path.Combine(path, Path.GetRandomFileName());

        try
        {
            if (createIfMissing)
            {
                Directory.CreateDirectory(path);
            }

            using var stream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PermissionDenied,
                $"The {description} '{path}' is not writable.",
                ex);
        }
        catch (IOException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"Unable to verify that the {description} '{path}' is writable.",
                ex);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }
    }
}
