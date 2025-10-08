// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

internal class SolutionRemoveCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly IReadOnlyCollection<string> _projects;

    public SolutionRemoveCommand(ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SolutionCommandParser.SlnArgument);

        _projects = (parseResult.GetValue(SolutionRemoveCommandParser.ProjectPathArgument) ?? []).ToList().AsReadOnly();

        SolutionArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SolutionArgumentValidator.CommandType.Remove);
    }

    public override int Execute()
    {
        string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_fileOrDirectory, includeSolutionFilterFiles: true);
        if (_projects.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneProjectToRemove);
        }

        try
        {
            var relativeProjectPaths = _projects
                .Select(p => Path.GetFullPath(p))
                .Select(p => Path.GetRelativePath(
                    Path.GetDirectoryName(solutionFileFullPath),
                    Directory.Exists(p)
                        ? MsbuildProject.GetProjectFileFromDirectory(p).FullName
                        : p));

            // Check if we're working with a solution filter file
            if (solutionFileFullPath.HasExtension(".slnf"))
            {
                RemoveProjectsFromSolutionFilterAsync(solutionFileFullPath, relativeProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
            }
            else
            {
                RemoveProjectsAsync(solutionFileFullPath, relativeProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
            }
            return 0;
        }
        catch (Exception ex) when (ex is not GracefulException)
        {
            if (ex is SolutionException || ex.InnerException is SolutionException)
            {
                throw new GracefulException(CliStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
            }
            throw new GracefulException(ex.Message, ex);
        }
    }

    private static async Task RemoveProjectsAsync(string solutionFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        ISolutionSerializer serializer = solution.SerializerExtension.Serializer;

        // set UTF-8 BOM encoding for .sln
        if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
        {
            solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            });
        }

        foreach (var projectPath in projectPaths)
        {
            var project = solution.FindProject(projectPath);
            // If the project is not found, try to find it by name without extension
            if (project is null && !Path.HasExtension(projectPath))
            {
                var projectsMatchByName = solution.SolutionProjects.Where(p => Path.GetFileNameWithoutExtension(p.DisplayName).Equals(projectPath));
                project = projectsMatchByName.Count() == 1 ? projectsMatchByName.First() : null;
            }
            // If project is still not found, print error
            if (project is null)
            {
                Reporter.Output.WriteLine(CliStrings.ProjectNotFoundInTheSolution, projectPath);
            }
            // If project is found, remove it
            else
            {
                solution.RemoveProject(project);
                Reporter.Output.WriteLine(CliStrings.ProjectRemovedFromTheSolution, projectPath);
            }
        }

        for (int i = 0; i < solution.SolutionFolders.Count; i++)
        {
            var folder = solution.SolutionFolders[i];
            int nonFolderDescendants = 0;
            Stack<SolutionFolderModel> stack = new();
            stack.Push(folder);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                nonFolderDescendants += current.Files?.Count ?? 0;
                foreach (var child in solution.SolutionItems)
                {
                    if (child is { Parent: var parent } && parent == current)
                    {
                        if (child is SolutionFolderModel childFolder)
                        {
                            stack.Push(childFolder);
                        }
                        else
                        {
                            nonFolderDescendants++;
                        }
                    }
                }
            }

            if (nonFolderDescendants == 0)
            {
                solution.RemoveFolder(folder);
                // After removal, adjust index and continue to avoid skipping folders after removal
                i--;
            }
        }

        await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
    }

    private static async Task RemoveProjectsFromSolutionFilterAsync(string slnfFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        // Load the filtered solution to get the parent solution path and existing projects
        SolutionModel filteredSolution = SlnFileFactory.CreateFromFilteredSolutionFile(slnfFileFullPath);
        string parentSolutionPath = filteredSolution.Description!; // The parent solution path is stored in Description

        // Get existing projects in the filter
        var existingProjects = filteredSolution.SolutionProjects.Select(p => p.FilePath).ToHashSet();

        // Remove specified projects
        foreach (var projectPath in projectPaths)
        {
            // Normalize the path to be relative to parent solution
            string normalizedPath = projectPath;

            // Try to find and remove the project
            if (existingProjects.Remove(normalizedPath))
            {
                Reporter.Output.WriteLine(CliStrings.ProjectRemovedFromTheSolution, normalizedPath);
            }
            else
            {
                Reporter.Output.WriteLine(CliStrings.ProjectNotFoundInTheSolution, normalizedPath);
            }
        }

        // Save updated filter
        SlnfFileHelper.SaveSolutionFilter(slnfFileFullPath, parentSolutionPath, existingProjects.OrderBy(p => p));

        await Task.CompletedTask;
    }
}
