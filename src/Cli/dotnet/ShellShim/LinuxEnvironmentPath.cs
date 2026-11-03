// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.CommandLine.StaticCompletions;

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
            var shellPath = _environmentProvider.GetEnvironmentVariable("SHELL");
            var shell = ShellInfo.GetShellType(shellPath);
            if (shell is ShellType.Bash or ShellType.Sh or ShellType.Dash && _fileSystem.Exists(DotnetCliToolsProfilePath))
            {
                _reporter.WriteLine(
                    CliStrings.EnvironmentPathLinuxNeedLogout);
            }
            else
            {
                PrintManualInstructions(shell, System.IO.Path.GetFileName(shellPath));
            }
        }
    }

    private void PrintManualInstructions(ShellType shell, string shellName)
    {
        var instruction = ShellPathInstructions.Create(
            shell, shellName, _packageExecutablePath.Path, _environmentProvider, _fileSystem);
        if (instruction == null)
        {
            _reporter.WriteLine(string.Format(
                CliStrings.EnvironmentPathLinuxUnknownShellInstructions, _packageExecutablePath.Path));
            return;
        }

        if (!instruction.UsesPosixReporting)
        {
            PrintShellInstructions(instruction);
            return;
        }

        var startupInstructions = instruction.StartupFileKind switch
        {
            ShellStartupFileKind.Login => CliStrings.EnvironmentPathLinuxLoginShellInstructions,
            ShellStartupFileKind.ZshEnvironment => CliStrings.EnvironmentPathLinuxZshEnvironmentInstructions,
            _ => CliStrings.EnvironmentPathLinuxInteractiveShellInstructions,
        };
        _reporter.WriteLine(string.Format(
            CliStrings.EnvironmentPathLinuxManualInstructions,
            _packageExecutablePath.Path,
            string.Format(startupInstructions, instruction.ShellName, instruction.StartupFile),
            instruction.StartupFileArgument,
            instruction.PathCommandArgument,
            instruction.PathCommand));
    }

    private void PrintShellInstructions(ShellPathInstruction instruction) =>
        _reporter.WriteLine(string.Format(
            CliStrings.EnvironmentPathLinuxShellManualInstructions,
            _packageExecutablePath.Path,
            string.Format(CliStrings.EnvironmentPathLinuxInteractiveShellInstructions, instruction.ShellName, instruction.StartupFile),
            instruction.AppendCommand,
            instruction.PathCommand));
}
