// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Package.List;

internal sealed class PackageListCommand(ParseResult parseResult) : CommandBase<PackageListCommandDefinitionBase>(parseResult)
{
    public override int Execute()
    {
        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(Definition.FileOption, Definition.ProjectOption, Definition.GetProjectOrFileArgument(), _parseResult);

        fileOrDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, fileOrDirectory));

        bool isFileBasedApp = allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory);

        Debug.Assert(isFileBasedApp || allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFile = isFileBasedApp ? Path.GetFullPath(fileOrDirectory) : GetProjectOrSolution(fileOrDirectory);
        bool noRestore = _parseResult.HasOption(Definition.NoRestore);
        int restoreExitCode = 0;

        if (!noRestore)
        {
            ReportOutputFormat formatOption = _parseResult.GetValue(Definition.FormatOption);
            bool interactive = _parseResult.GetValue(Definition.InteractiveOption);
            restoreExitCode = RunRestore(projectFile, formatOption, interactive, isFileBasedApp);
        }

        return restoreExitCode == 0
            ? NuGetCommand.Run(TransformArgs(projectFile), isFileBasedApp)
            : restoreExitCode;
    }

    private static int RunRestore(string projectOrSolution, ReportOutputFormat formatOption, bool interactive, bool isFileBasedApp)
    {
        CommandBase command;
        if (isFileBasedApp)
        {
            command = new VirtualProjectBuildingCommand(
                entryPointFileFullPath: projectOrSolution,
                msbuildArgs: MSBuildArgs.FromProperties(new Dictionary<string, string>
                {
                    ["NuGetInteractive"] = interactive.ToString(),
                }.AsReadOnly()))
            {
                NoCache = true,
                NoBuild = true,
                NoConsoleLogger = formatOption == ReportOutputFormat.json,
            };
        }
        else
        {
            List<string> args = ["-target:Restore", projectOrSolution];

            if (formatOption == ReportOutputFormat.json)
            {
                args.Add("-noConsoleLogger");
            }
            else
            {
                args.Add("-consoleLoggerParameters:NoSummary");
                args.Add("-verbosity:minimal");
            }

            args.Add($"-interactive:{interactive.ToString().ToLower()}");

            command = new MSBuildForwardingApp(rawMSBuildArgs: args);
        }

        int exitCode = 0;

        try
        {
            exitCode = command.Execute();
        }
        catch (Exception)
        {
            exitCode = 1;
        }

        if (exitCode != 0)
        {
            if (formatOption == ReportOutputFormat.json)
            {
                string jsonError = $$"""
{
   "version": 1,
   "problems": [
      {
         "text": "{{String.Format(CultureInfo.CurrentCulture, CliCommandStrings.Error_restore)}}",
         "level": "error"
      }
   ]
}
""";
                Console.WriteLine(jsonError);
            }
        }

        return exitCode;
    }

    private string[] TransformArgs(string projectOrSolution)
    {
        var args = new List<string>
        {
            "package",
            "list",
            projectOrSolution
        };

        args.AddRange(_parseResult.OptionValuesToBeForwarded(Definition));

        Definition.EnforceOptionRules(_parseResult);

        return [.. args];
    }

    /// <summary>
    /// Gets a solution file or a project file from a given directory.
    /// If the given path is a file, it just returns it after checking
    /// it exists.
    /// </summary>
    /// <returns>Path to send to the command</returns>
    private static string GetProjectOrSolution(string fileOrDirectory)
    {
        string resultPath = fileOrDirectory;

        if (Directory.Exists(resultPath))
        {
            string[] possibleSolutionPath = SlnFileFactory.ListSolutionFilesInDirectory(resultPath, false);

            //If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
            if (possibleSolutionPath.Count() > 1)
            {
                throw new GracefulException(CliStrings.MoreThanOneSolutionInDirectory, resultPath);
            }
            //If a single solution is found, use it.
            else if (possibleSolutionPath.Count() == 1)
            {
                return possibleSolutionPath[0];
            }
            //If no solutions are found, look for a project file
            else
            {
                var possibleProjectPath = Directory.GetFiles(resultPath, "*.*proj", SearchOption.TopDirectoryOnly)
                                          .Where(path => !path.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
                                          .ToList();

                //No projects found throws an error that no sln nor projs were found
                if (possibleProjectPath.Count() == 0)
                {
                    throw new GracefulException(CliCommandStrings.NoProjectsOrSolutions, resultPath);
                }
                //A single project found, use it
                else if (possibleProjectPath.Count() == 1)
                {
                    return possibleProjectPath[0];
                }
                //More than one project found. Not sure which one to choose
                else
                {
                    throw new GracefulException(CliStrings.MoreThanOneProjectInDirectory, resultPath);
                }
            }
        }

        if (!File.Exists(resultPath))
        {
            throw new GracefulException(CliCommandStrings.PackageListFileNotFound, resultPath);
        }

        return resultPath;
    }
}
