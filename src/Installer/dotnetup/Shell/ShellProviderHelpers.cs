// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

internal static class ShellProviderHelpers
{
    private const string DotnetupOnlyComment = "# This script adds dotnetup to your PATH";

    internal static string GetDotnetupOnlyComment() => DotnetupOnlyComment;

    internal static string GetEnvironmentConfigurationComment(string dotnetInstallPath)
        => $"# This script configures the environment for .NET installed at {dotnetInstallPath}";

    internal static string EscapePosixPath(string path)
        => path.Replace("'", "'\\''", StringComparison.Ordinal);

    internal static string EscapePowerShellPath(string path)
        => path.Replace("'", "''", StringComparison.Ordinal);

    internal static string GetCommandFlags(bool dotnetupOnly, string? dotnetInstallPath, Func<string, string> escapePath)
    {
        List<string> flags = [];

        if (dotnetupOnly)
        {
            flags.Add("--dotnetup-only");
        }
        else if (dotnetInstallPath is { Length: > 0 } installPath &&
            !DotnetupUtilities.PathsEqual(installPath, DotnetupPaths.DefaultDotnetInstallPath))
        {
            flags.Add($"--dotnet-install-path '{escapePath(installPath)}'");
        }

        return string.Join(" ", flags);
    }

    internal static string AppendArguments(string command, string flags)
        => string.IsNullOrEmpty(flags) ? command : $"{command} {flags}";

    internal static string BuildPosixProfileEntry(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        var command = AppendArguments($"'{escapedPath}' print-env-script --shell {shellName}", flags);

        return $$"""
            if [ -x '{{escapedPath}}' ]; then
                eval "$({{command}})"
            fi
            """;
    }

    internal static string BuildPowerShellProfileEntry(string dotnetupPath, string shellName, string flags)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);
        var command = AppendArguments($"& '{escapedPath}' print-env-script --shell {shellName}", flags);

        return $$"""
            if (Test-Path -LiteralPath '{{escapedPath}}' -PathType Leaf)
            {
                {{command}} | Invoke-Expression
            }
            """;
    }

    internal static string BuildPosixPathExport(string escapedPath, string dotnetupDir, bool includeDotnet)
    {
        // Put the managed paths first so the shell resolves dotnet/dotnetup from the selected install immediately.
        if (includeDotnet && !string.IsNullOrWhiteSpace(dotnetupDir))
        {
            return $"export PATH='{EscapePosixPath(dotnetupDir)}':'{escapedPath}':$PATH";
        }

        if (includeDotnet)
        {
            return $"export PATH='{escapedPath}':$PATH";
        }

        return string.IsNullOrWhiteSpace(dotnetupDir)
            ? string.Empty
            : $"export PATH='{EscapePosixPath(dotnetupDir)}':$PATH";
    }

    internal static string BuildPowerShellPathExport(string escapedPath, string dotnetupDir, bool includeDotnet)
    {
        // Put the managed paths first so the shell resolves dotnet/dotnetup from the selected install immediately.
        if (includeDotnet && !string.IsNullOrWhiteSpace(dotnetupDir))
        {
            return $"$env:PATH = '{EscapePowerShellPath(dotnetupDir)}' + [IO.Path]::PathSeparator + '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";
        }

        if (includeDotnet)
        {
            return $"$env:PATH = '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";
        }

        return string.IsNullOrWhiteSpace(dotnetupDir)
            ? string.Empty
            : $"$env:PATH = '{EscapePowerShellPath(dotnetupDir)}' + [IO.Path]::PathSeparator + $env:PATH";
    }

    internal static string GetDotnetupExecutablePathOrThrow()
    {
        return Environment.ProcessPath
            ?? throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to determine the dotnetup executable path.");
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
