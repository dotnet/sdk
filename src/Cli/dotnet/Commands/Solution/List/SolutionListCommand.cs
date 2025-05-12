// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

internal class SolutionListCommand(
    ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly string _fileOrDirectory = parseResult.GetValue(SolutionCommandParser.SlnArgument);
    private readonly bool _displaySolutionFolders = parseResult.GetValue(SolutionListCommandParser.SolutionFolderOption);

    public override int Execute()
    {
        string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_fileOrDirectory, includeSolutionFilterFiles: true);
        try
        {
            ListAllProjectsAsync(solutionFileFullPath);
            return 0;
        }
        catch (Exception ex)
        {
            throw new GracefulException(CliStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
        }
    }

    private void ListAllProjectsAsync(string solutionFileFullPath)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        string[] paths;
        if (_displaySolutionFolders)
        {
            paths = [.. solution.SolutionFolders
                // VS-SolutionPersistence does not return a path object, so there might be issues with forward/backward slashes on different platforms
                .Select(folder => Path.GetDirectoryName(folder.Path.TrimStart('/')))];
        }
        else
        {
            paths = [.. solution.SolutionProjects.Select(project => project.FilePath)];
        }
        if (paths.Length == 0)
        {
            Reporter.Output.WriteLine(CliStrings.NoProjectsFound);
        }
        else
        {
            Array.Sort(paths);

            string header = _displaySolutionFolders ? CliCommandStrings.SolutionFolderHeader : CliCommandStrings.ProjectsHeader;
            Reporter.Output.WriteLine(header);
            Reporter.Output.WriteLine(new string('-', header.Length));
            foreach (string slnProject in paths)
            {
                Reporter.Output.WriteLine(slnProject);
            }
        }

    }
}
