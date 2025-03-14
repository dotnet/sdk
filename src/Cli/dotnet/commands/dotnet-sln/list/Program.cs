// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using CommandLocalizableStrings = Microsoft.DotNet.Cli.CommonLocalizableStrings;

namespace Microsoft.DotNet.Tools.Sln.List;

internal class ListProjectsInSolutionCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly bool _displaySolutionFolders;

    private readonly SlnListOutputFormat _outputFormat;

    public ListProjectsInSolutionCommand(
        ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
        _displaySolutionFolders = parseResult.GetValue(SlnListParser.SolutionFolderOption);
        _outputFormat = parseResult.GetValue(SlnListParser.OutputFormatOption);
    }

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
            throw new GracefulException(CommandLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
        }
    }

    private void ListAllProjectsAsync(string solutionFileFullPath)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        string[] paths = _displaySolutionFolders
            ? solution.SolutionFolders.Select(folder => Path.GetDirectoryName(folder.Path.TrimStart('/'))).ToArray()
            : solution.SolutionProjects.Select(project => project.FilePath).ToArray();
        
        if (paths.Length == 0)
        {
            Reporter.Output.WriteLine(CommonLocalizableStrings.NoProjectsFound);
            return;
        }

        Array.Sort(paths);

        switch(_outputFormat)
        {
            case SlnListOutputFormat.Text:
                string header = _displaySolutionFolders ? LocalizableStrings.SolutionFolderHeader : LocalizableStrings.ProjectsHeader;
                Reporter.Output.WriteLine(header);
                Reporter.Output.WriteLine(new string('-', header.Length));
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
                break;
            case SlnListOutputFormat.Raw:
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
                break;
            case SlnListOutputFormat.Json:
                Reporter.Output.WriteLine(JsonSerializer.Serialize(paths));
                break;
        }
    }
}
