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
        var home = _environmentProvider.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home) || shell is not ("bash" or "zsh" or "sh" or "dash"))
        {
            _reporter.WriteLine(string.Format(
                CliStrings.EnvironmentPathLinuxUnknownShellInstructions, _packageExecutablePath.Path));
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
}
