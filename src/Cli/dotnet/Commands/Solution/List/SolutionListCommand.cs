// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

internal class SolutionListCommand(
    ParseResult parseResult, IReporter reporter = null) : CommandBase(parseResult)
{
    private readonly string _fileOrDirectory = parseResult.GetValue(SolutionCommandDefinition.SlnArgument);
    private readonly bool _displaySolutionFolders = parseResult.GetValue(SolutionListCommandDefinition.SolutionFolderOption);
    private readonly SolutionListOutputFormat _outputFormat = parseResult.GetValue(SolutionListCommandDefinition.SolutionListFormatOption);
    private readonly IReporter _reporter = reporter ?? Reporter.Output;

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
        Array.Sort(paths);

        if (_outputFormat is SolutionListOutputFormat.json)
        {
            PrintJson(paths);
        }
        else
        {
            PrintText(paths);
        }
    }

    private void PrintText(string[] paths)
    {
        if (paths.Length == 0)
        {
            _reporter.WriteLine(CliStrings.NoProjectsFound);
            return;
        }

        string header = _displaySolutionFolders ? CliCommandStrings.SolutionFolderHeader : CliCommandStrings.ProjectsHeader;
        _reporter.WriteLine(header);
        _reporter.WriteLine(new string('-', header.Length));
        foreach (string slnProject in paths)
        {
            _reporter.WriteLine(slnProject);
        }
    }

    private void PrintJson(string[] paths)
    {
        string jsonString = JsonSerializer.Serialize(paths, JsonHelper.NoEscapeSerializerOptions);
        _reporter.WriteLine(jsonString);
    }
}
