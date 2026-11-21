// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    // Fish single-quoted strings treat backslash as an escape character for \' and \\,
    // so backslashes must be escaped first and quotes use \' rather than the POSIX '\''.
    internal static string EscapeFishPath(string path)
        => path.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

    /// <summary>
    /// Returns the script a managed profile block runs for the given settings: what <c>env script</c>
    /// prints, minus its one-line header comment. The block holds this directly instead of running
    /// <c>env script</c>, because that would start a process on every shell start. <c>env set</c>
    /// rewrites the block when the settings change. The rest of the script is kept verbatim, since
    /// a quoted path may itself span lines.
    /// </summary>
    /// <param name="provider">The shell provider that generates the env script.</param>
    /// <param name="dotnetupPath">The full path to the dotnetup binary.</param>
    /// <param name="includeDotnet">When true, the script sets DOTNET_ROOT and adds the managed dotnet to PATH.</param>
    /// <param name="includeDotnetup">When true, the script adds the dotnetup directory to PATH.</param>
    /// <param name="dotnetInstallPath">The .NET install path, or <c>null</c> for the default dotnetup install path.</param>
    internal static string GetProfileEnvScript(
        IEnvShellProvider provider,
        string dotnetupPath,
        bool includeDotnet,
        bool includeDotnetup,
        string? dotnetInstallPath)
    {
        string installPath = string.IsNullOrEmpty(dotnetInstallPath) ? DotnetupPaths.DefaultDotnetInstallPath : dotnetInstallPath;
        string dotnetupDir = includeDotnetup ? Path.GetDirectoryName(dotnetupPath) ?? string.Empty : string.Empty;

        string script = provider.GenerateEnvScript(installPath, dotnetupDir, includeDotnet).ReplaceLineEndings("\n");

        // The header is always a single comment line (see GetEnvironmentConfigurationComment).
        if (script.StartsWith('#'))
        {
            int headerEnd = script.IndexOf('\n');
            script = headerEnd < 0 ? string.Empty : script[(headerEnd + 1)..];
        }

        return script.TrimStart('\n').TrimEnd('\n');
    }

    internal static string BuildPosixActivationCommand(string dotnetupPath)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        return $"eval \"$('{escapedPath}' env script)\"";
    }

    internal static string BuildPosixProfileEntry(string dotnetupPath, string envScript)
    {
        var escapedPath = EscapePosixPath(dotnetupPath);
        return BuildGuardedEntry($"if [ -x '{escapedPath}' ]; then", envScript, "fi");
    }

    // Fish activation runs inside a fish shell, so 'env script' auto-detects fish; no flags are
    // baked in (the stored config is followed at run time), matching the other shells.
    internal static string BuildFishActivationCommand(string dotnetupPath)
    {
        var escapedPath = EscapeFishPath(dotnetupPath);
        return $"'{escapedPath}' env script | source";
    }

    internal static string BuildFishProfileEntry(string dotnetupPath, string envScript)
    {
        var escapedPath = EscapeFishPath(dotnetupPath);
        return BuildGuardedEntry($"if test -x '{escapedPath}'", envScript, "end");
    }

    internal static string BuildPowerShellActivationCommand(string dotnetupPath)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);
        return $"Invoke-Expression (& '{escapedPath}' env script | Out-String)";
    }

    internal static string BuildPowerShellProfileEntry(string dotnetupPath, string envScript)
    {
        var escapedPath = EscapePowerShellPath(dotnetupPath);

        // [System.IO.File]::Exists instead of Test-Path: in a new session Test-Path loads a
        // module first, which noticeably slows down every PowerShell start.
        return BuildGuardedEntry($"if ([System.IO.File]::Exists('{escapedPath}'))\n{{", envScript, "}");
    }

    // The script is not indented, since indenting would change any quoted value that spans lines.
    // Joined with '\n' so the entry doesn't depend on this file's line endings.
    private static string BuildGuardedEntry(string opening, string envScript, string closing)
        => $"{opening}\n{envScript}\n{closing}";

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

    internal static string BuildFishPathExport(string escapedPath, string dotnetupDir, bool includeDotnet)
    {
        // fish_add_path --move prepends (or moves) the entries so the shell resolves
        // dotnet/dotnetup from the selected install immediately.
        // --path/--global keeps the change session-scoped in $PATH instead of persisting
        // to universal fish_user_paths.
        if (includeDotnet && !string.IsNullOrWhiteSpace(dotnetupDir))
        {
            return $"fish_add_path --global --move --path '{EscapeFishPath(dotnetupDir)}' '{escapedPath}'";
        }

        if (includeDotnet)
        {
            return $"fish_add_path --global --move --path '{escapedPath}'";
        }

        return string.IsNullOrWhiteSpace(dotnetupDir)
            ? string.Empty
            : $"fish_add_path --global --move --path '{EscapeFishPath(dotnetupDir)}'";
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
        return DotnetupProcessInfo.ExecutablePath
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

    internal static string GetFishConfigurationDirectoryOrThrow()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(GetUserHomeDirectoryOrThrow(), ".config")
            : Path.GetFullPath(xdgConfigHome);

        var confDDirectory = Path.Combine(configHome, "fish", "conf.d");
        EnsureDirectoryWritable(confDDirectory, "fish configuration directory", createIfMissing: true);
        return confDDirectory;
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
