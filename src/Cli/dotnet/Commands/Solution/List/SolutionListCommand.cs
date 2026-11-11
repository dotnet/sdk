// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

internal sealed class SolutionListCommand : CommandBase<SolutionListCommandDefinition>
{
    private readonly string _fileOrDirectory;
    private readonly bool _displaySolutionFolders;
    private readonly string _format;

    public SolutionListCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(Definition.Parent.SlnArgument);
        _displaySolutionFolders = parseResult.GetValue(Definition.SolutionFolderOption);
        _format = parseResult.GetValue(Definition.FormatOption);
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

        if (_format == "json")
        {
            Reporter.Output.WriteLine(JsonSerializer.Serialize(paths, SolutionListJsonSerializerContext.Default.StringArray));
        }
        else if (paths.Length == 0)
        {
            Reporter.Output.WriteLine(CliStrings.NoProjectsFound);
        }
        else
        {
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

[JsonSerializable(typeof(string[]))]
internal partial class SolutionListJsonSerializerContext : JsonSerializerContext;
