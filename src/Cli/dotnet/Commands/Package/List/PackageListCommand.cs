// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Globalization;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Package.List;

internal class PackageListCommand(
    ParseResult parseResult) : CommandBase(parseResult)
{
    //The file or directory passed down by the command
    private readonly string _fileOrDirectory = GetAbsolutePath(Directory.GetCurrentDirectory(),
            parseResult.HasOption(PackageCommandParser.ProjectOption) ?
            parseResult.GetValue(PackageCommandParser.ProjectOption) :
            parseResult.GetValue(ListCommandParser.SlnOrProjectArgument) ?? "");

    private static string GetAbsolutePath(string currentDirectory, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(currentDirectory, relativePath));
    }

    public override int Execute()
    {
        string projectFile = GetProjectOrSolution();
        bool noRestore = _parseResult.HasOption(PackageListCommandParser.NoRestore);
        int restoreExitCode = 0;

        if (!noRestore)
        {
            ReportOutputFormat formatOption = _parseResult.GetValue((Option<ReportOutputFormat>)PackageListCommandParser.FormatOption);
            bool interactive = _parseResult.GetValue((Option<bool>)PackageListCommandParser.InteractiveOption);
            restoreExitCode = RunRestore(projectFile, formatOption, interactive);
        }

        return restoreExitCode == 0
            ? NuGetCommand.Run(TransformArgs(projectFile))
            : restoreExitCode;
    }

    private int RunRestore(string projectOrSolution, ReportOutputFormat formatOption, bool interactive)
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

        MSBuildForwardingApp restoringCommand = new MSBuildForwardingApp(rawMSBuildArgs: args);

        int exitCode = 0;

        try
        {
            exitCode = restoringCommand.Execute();
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

    internal static void EnforceOptionRules(ParseResult parseResult)
    {
        var mutexOptionCount = 0;
        mutexOptionCount += parseResult.HasOption(PackageListCommandParser.DeprecatedOption) ? 1 : 0;
        mutexOptionCount += parseResult.HasOption(PackageListCommandParser.OutdatedOption) ? 1 : 0;
        mutexOptionCount += parseResult.HasOption(PackageListCommandParser.VulnerableOption) ? 1 : 0;
        if (mutexOptionCount > 1)
        {
            throw new GracefulException(CliCommandStrings.OptionsCannotBeCombined);
        }
    }

    private string[] TransformArgs(string projectOrSolution)
    {
        var args = new List<string>
        {
            "package",
            "list",
            projectOrSolution
        };

        args.AddRange(_parseResult.OptionValuesToBeForwarded(PackageListCommandParser.GetCommand()));

        EnforceOptionRules(_parseResult);

        return [.. args];
    }

    /// <summary>
    /// Gets a solution file or a project file from a given directory.
    /// If the given path is a file, it just returns it after checking
    /// it exists.
    /// </summary>
    /// <returns>Path to send to the command</returns>
    private string GetProjectOrSolution()
    {
        string resultPath = _fileOrDirectory;

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
