// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Install;

internal class ProjectRestorer(IReporter reporter = null,
    IEnumerable<string> additionalRestoreArguments = null) : IProjectRestorer
{
    private readonly IReporter _reporter = reporter ?? Reporter.Output;
    private readonly IReporter _errorReporter = reporter ?? Reporter.Error;
    private readonly bool _forceOutputRedirection = reporter != null;
    private readonly IEnumerable<string> _additionalRestoreArguments = additionalRestoreArguments;

    public void Restore(FilePath project,
        PackageLocation packageLocation,
        string verbosity = null)
    {
        List<string> argsToPassToRestore = [project.Value];
        if (packageLocation.NugetConfig != null)
        {
            argsToPassToRestore.Add("--configfile");
            argsToPassToRestore.Add(packageLocation.NugetConfig.Value.Value);
        }

        argsToPassToRestore.AddRange(["--runtime", Constants.AnyRid]);

        argsToPassToRestore.Add($"--verbosity:{verbosity ?? GetDefaultVerbosity()}");

        if (_additionalRestoreArguments != null)
        {
            argsToPassToRestore.AddRange(_additionalRestoreArguments.Where(arg => !arg.StartsWith("-verbosity")));
        }

        var command = new DotNetCommandFactory(alwaysRunOutOfProc: true)
            .Create("restore", argsToPassToRestore);

        if (verbosity == null || _forceOutputRedirection)
        {
            command = command
                .OnOutputLine(line => WriteLine(_reporter, line, project))
                .OnErrorLine(line => WriteLine(_errorReporter, line, project));
        }

        var result = command.Execute();
        if (result.ExitCode != 0)
        {
            throw new ToolPackageException(LocalizableStrings.ToolInstallationRestoreFailed);
        }
    }

    /// <summary>
    /// Workaround to https://github.com/dotnet/cli/issues/10523
    /// Output quiet will break "--interactive" experience since
    /// it will output nothing. However, minimal output will have
    /// the temp project path.
    /// </summary>
    private string GetDefaultVerbosity()
    {
        var defaultVerbosity = "quiet";
        if ((_additionalRestoreArguments != null)
            && _additionalRestoreArguments.Contains(Constants.RestoreInteractiveOption, StringComparer.Ordinal))
        {
            defaultVerbosity = "minimal";
        }

        return defaultVerbosity;
    }

    internal static void WriteLine(IReporter reporter, string line, FilePath project)
    {
        line ??= string.Empty;

        // Remove the temp project prefix if present
        if (line.StartsWith($"{project.Value} : ", StringComparison.OrdinalIgnoreCase))
        {
            line = line.Substring(project.Value.Length + 3);
        }

        // Note: MSBuild intentionally does not localize "warning" and "error" for diagnostic messages
        if (line.StartsWith("warning ", StringComparison.Ordinal))
        {
            line = line.Yellow();
        }
        else if (line.StartsWith("error ", StringComparison.Ordinal))
        {
            line = line.Red();
        }

        reporter.WriteLine(line);
    }
}
