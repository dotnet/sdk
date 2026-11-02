// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class LinuxEnvironmentPath : IEnvironmentPath
{
    private readonly IFile _fileSystem;
    private readonly IEnvironmentProvider _environmentProvider;
    private readonly IReporter _reporter;
    private const string PathName = "PATH";
    private readonly BashPathUnderHomeDirectory _packageExecutablePath;

    internal static readonly string DotnetCliToolsProfilePath =
        Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_LINUX_PROFILED_PATH") ??
        @"/etc/profile.d/dotnet-cli-tools-bin-path.sh";

    internal LinuxEnvironmentPath(
        BashPathUnderHomeDirectory packageExecutablePath,
        IReporter reporter,
        IEnvironmentProvider environmentProvider,
        IFile fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _environmentProvider
            = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
        _reporter
            = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _packageExecutablePath = packageExecutablePath;
    }

    public void AddPackageExecutablePathToUserPath()
    {
        if (PackageExecutablePathExists())
        {
            return;
        }

        var script = $"export PATH=\"$PATH:{_packageExecutablePath.PathWithDollar}\"";
        _fileSystem.WriteAllText(DotnetCliToolsProfilePath, script);
    }

    private bool PackageExecutablePathExists()
    {
        var value = _environmentProvider.GetEnvironmentVariable(PathName);
        if (value == null)
        {
            return false;
        }

        return value
            .Split(':')
            .Any(p => p == _packageExecutablePath.Path || p == _packageExecutablePath.PathWithTilde);
    }

    public void PrintAddPathInstructionIfPathDoesNotExist()
    {
        if (!PackageExecutablePathExists())
        {
            var shell = System.IO.Path.GetFileName(_environmentProvider.GetEnvironmentVariable("SHELL"));
            if (shell is "bash" or "sh" or "dash" && _fileSystem.Exists(DotnetCliToolsProfilePath))
            {
                _reporter.WriteLine(
                    CliStrings.EnvironmentPathLinuxNeedLogout);
            }
            else
            {
                PrintManualInstructions(shell);
            }
        }
    }

    private void PrintManualInstructions(string shell)
    {
        if (shell is "pwsh" or "powershell")
        {
            var command = $"$env:PATH += [System.IO.Path]::PathSeparator + {QuotePowerShellArgument(_packageExecutablePath.Path)}";
            // Resolve profiles in the user's shell, including host-specific and XDG overrides.
            // File APIs preserve literal backslashes on Unix, unlike PowerShell's filesystem provider.
            var appendCommand = "$dotnetProfile = if ([System.IO.File]::Exists($PROFILE.CurrentUserAllHosts)) { $PROFILE.CurrentUserAllHosts } " +
                "elseif ([System.IO.File]::Exists($PROFILE)) { $PROFILE } else { $PROFILE }; " +
                "[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($dotnetProfile)) | Out-Null; " +
                $"[System.IO.File]::AppendAllText($dotnetProfile, \"`n\" + {QuotePowerShellArgument(command)} + \"`n\")";
            PrintShellInstructions(shell, "$PROFILE.CurrentUserAllHosts / $PROFILE", appendCommand, command);
            return;
        }

        if (shell == "nu")
        {
            var command = $"$env.PATH = ($env.PATH | append {QuoteNushellArgument(_packageExecutablePath.Path)})";
            // These variables reflect --env-config/--config and Nushell's configuration directory.
            var appendCommand = "let dotnet_profile = (if ($nu.config-path | path exists) { $nu.config-path } " +
                "else if ($nu.env-path | path exists) { $nu.env-path } else { $nu.config-path }); " +
                "mkdir ($dotnet_profile | path dirname); " +
                $"(\"\\n\" + {QuoteNushellArgument(command)} + \"\\n\") | save --append --raw $dotnet_profile";
            PrintShellInstructions(shell, "$nu.config-path / $nu.env-path", appendCommand, command);
            return;
        }

        var home = _environmentProvider.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home) || shell is not ("bash" or "zsh" or "sh" or "dash" or "fish"))
        {
            _reporter.WriteLine(string.Format(
                CliStrings.EnvironmentPathLinuxUnknownShellInstructions, _packageExecutablePath.Path));
            return;
        }

        if (shell == "fish")
        {
            var xdgConfigHome = _environmentProvider.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var directory = System.IO.Path.Combine(
                !string.IsNullOrEmpty(xdgConfigHome) && System.IO.Path.IsPathFullyQualified(xdgConfigHome)
                    ? xdgConfigHome : System.IO.Path.Combine(home, ".config"), "fish");
            var startupPath = System.IO.Path.Combine(directory, "config.fish");
            var command = $"fish_add_path --append --path {QuoteFishArgument(_packageExecutablePath.Path)}";
            var appendCommand = $"mkdir -p -- {QuoteFishArgument(directory)}; " +
                $"printf '\\n%s\\n' {QuoteFishArgument(command)} >> {QuoteFishArgument(startupPath)}";
            PrintShellInstructions(shell, startupPath, appendCommand, command);
            return;
        }

        string startupFile;
        string startupInstructions;
        if (shell == "bash")
        {
            // Bash reads only the first existing login file; creating .bash_profile can hide .profile.
            startupFile = new[] { ".bash_profile", ".bash_login", ".profile" }
                .Select(file => System.IO.Path.Combine(home, file))
                .FirstOrDefault(_fileSystem.Exists);
            if (startupFile == null && _fileSystem.Exists(System.IO.Path.Combine(home, ".bashrc")))
            {
                startupFile = System.IO.Path.Combine(home, ".bashrc");
                startupInstructions = CliStrings.EnvironmentPathLinuxInteractiveShellInstructions;
            }
            else
            {
                startupFile ??= System.IO.Path.Combine(home, ".profile");
                startupInstructions = CliStrings.EnvironmentPathLinuxLoginShellInstructions;
            }
        }
        else if (shell == "zsh")
        {
            var zdotdir = _environmentProvider.GetEnvironmentVariable("ZDOTDIR");
            // Zsh uses HOME only when ZDOTDIR is unset; an empty ZDOTDIR refers to the root directory.
            var directory = zdotdir == null ? home : zdotdir + "/";
            startupFile = new[] { ".zshrc", ".zprofile", ".zlogin", ".zshenv" }
                .Select(file => System.IO.Path.Combine(directory, file))
                .FirstOrDefault(_fileSystem.Exists) ?? System.IO.Path.Combine(directory, ".zshrc");
            startupInstructions = System.IO.Path.GetFileName(startupFile) switch
            {
                ".zprofile" or ".zlogin" => CliStrings.EnvironmentPathLinuxLoginShellInstructions,
                ".zshenv" => CliStrings.EnvironmentPathLinuxZshEnvironmentInstructions,
                _ => CliStrings.EnvironmentPathLinuxInteractiveShellInstructions
            };
        }
        else
        {
            startupFile = System.IO.Path.Combine(home, ".profile");
            startupInstructions = CliStrings.EnvironmentPathLinuxLoginShellInstructions;
        }

        var exportCommand = $"export PATH=\"$PATH\":{QuoteShellArgument(_packageExecutablePath.Path)}";
        _reporter.WriteLine(string.Format(
            CliStrings.EnvironmentPathLinuxManualInstructions,
            _packageExecutablePath.Path,
            string.Format(startupInstructions, shell, startupFile),
            QuoteShellArgument(startupFile),
            QuoteShellArgument(exportCommand),
            exportCommand));
    }

    private static string QuoteShellArgument(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string QuoteFishArgument(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    // PowerShell also recognizes typographic apostrophes as single-quote delimiters.
    private static string QuotePowerShellArgument(string value) => "'" + value
        .Replace("'", "''")
        .Replace("\u2018", "\u2018\u2018")
        .Replace("\u2019", "\u2019\u2019")
        .Replace("\u201a", "\u201a\u201a")
        .Replace("\u201b", "\u201b\u201b") + "'";

    private static string QuoteNushellArgument(string value)
    {
        var delimiter = "#";
        while (value.Contains("'" + delimiter))
        {
            delimiter += "#";
        }

        return "r" + delimiter + "'" + value + "'" + delimiter;
    }

    private void PrintShellInstructions(string shell, string startupFile, string appendCommand, string command) =>
        _reporter.WriteLine(string.Format(
            CliStrings.EnvironmentPathLinuxShellManualInstructions,
            _packageExecutablePath.Path,
            string.Format(CliStrings.EnvironmentPathLinuxInteractiveShellInstructions, shell, startupFile),
            appendCommand,
            command));
}
