// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Cli.Commands.Solution.Add;

internal class SolutionAddCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly bool _inRoot;
    private readonly IReadOnlyCollection<string> _projects;
    private readonly string? _solutionFolderPath;
    private string _solutionFileFullPath = string.Empty;
    private bool _includeReferences;

    private static string GetSolutionFolderPathWithForwardSlashes(string path)
    {
        // SolutionModel::AddFolder expects paths to have leading, trailing and inner forward slashes
        // https://github.com/microsoft/vs-solutionpersistence/blob/87ee8ea069662d55c336a9bd68fe4851d0384fa5/src/Microsoft.VisualStudio.SolutionPersistence/Model/SolutionModel.cs#L171C1-L172C1
        return "/" + string.Join("/", PathUtility.GetPathWithDirectorySeparator(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)) + "/";
    }

    private static bool IsSolutionFolderPathInDirectoryScope(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && !Path.IsPathRooted(relativePath) // This means path is in a different volume
            && !relativePath.StartsWith(".."); // This means path is outside the solution directory
    }

    public SolutionAddCommand(ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SolutionCommandParser.SlnArgument)!;
        _projects = (IReadOnlyCollection<string>)(parseResult.GetValue(SolutionAddCommandParser.ProjectPathArgument) ?? []);
        _inRoot = parseResult.GetValue(SolutionAddCommandParser.InRootOption);
        _solutionFolderPath = parseResult.GetValue(SolutionAddCommandParser.SolutionFolderOption);
        _includeReferences = parseResult.GetValue(SolutionAddCommandParser.IncludeReferencesOption);
        SolutionArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SolutionArgumentValidator.CommandType.Add, _inRoot, _solutionFolderPath);
        _solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_fileOrDirectory, includeSolutionFilterFiles: true);
    }

    public override int Execute()
    {
        if (_projects.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneProjectToAdd);
        }

        // Get project paths from the command line arguments
        PathUtility.EnsureAllPathsExist(_projects, CliStrings.CouldNotFindProjectOrDirectory, true);

        List<string> fullProjectPaths = _projects.Select(project =>
        {
            var fullPath = Path.GetFullPath(project);
            return Directory.Exists(fullPath) ? MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName : fullPath;
        }).ToList();

        // Check if we're working with a solution filter file
        if (_solutionFileFullPath.HasExtension(".slnf"))
        {
            AddProjectsToSolutionFilterAsync(fullProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
        }
        else
        {
            // Add projects to the solution
            AddProjectsToSolutionAsync(fullProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
        }
        return 0;
    }

    private SolutionFolderModel? GenerateIntermediateSolutionFoldersForProjectPath(SolutionModel solution, string relativeProjectPath)
    {
        if (_inRoot)
        {
            return null;
        }

        string? relativeSolutionFolderPath = string.Empty;

        if (string.IsNullOrEmpty(_solutionFolderPath))
        {
            // Generate the solution folder path based on the project path
            relativeSolutionFolderPath = Path.GetDirectoryName(relativeProjectPath);
            Debug.Assert(relativeSolutionFolderPath is not null);

            // If the project is in a folder with the same name as the project, we need to go up one level
            if (relativeSolutionFolderPath.Split(Path.DirectorySeparatorChar).LastOrDefault() == Path.GetFileNameWithoutExtension(relativeProjectPath))
            {
                relativeSolutionFolderPath = Path.Combine([.. relativeSolutionFolderPath.Split(Path.DirectorySeparatorChar).SkipLast(1)]);
            }

            // If the generated path is outside the solution directory, we need to set it to empty
            if (!IsSolutionFolderPathInDirectoryScope(relativeSolutionFolderPath))
            {
                relativeSolutionFolderPath = string.Empty;
            }
        }
        else
        {
            // Use the provided solution folder path
            relativeSolutionFolderPath = _solutionFolderPath;
        }

        return string.IsNullOrEmpty(relativeSolutionFolderPath)
            ? null
            : solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(relativeSolutionFolderPath));
    }

    private async Task AddProjectsToSolutionAsync(IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(_solutionFileFullPath);
        Debug.Assert(solution.SerializerExtension is not null);
        ISolutionSerializer serializer = solution.SerializerExtension.Serializer;

        // set UTF8 BOM encoding for .sln
        if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
        {
            solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            });

            // Set default configurations and platforms for sln file
            foreach (var platform in SlnFileFactory.DefaultPlatforms)
            {
                solution.AddPlatform(platform);
            }

            foreach (var buildType in SlnFileFactory.DefaultBuildTypes)
            {
                solution.AddBuildType(buildType);
            }
        }

        foreach (var projectPath in projectPaths)
        {
            AddProject(solution, projectPath, serializer);
        }

        await serializer.SaveAsync(_solutionFileFullPath, solution, cancellationToken);
    }

    private void AddProject(SolutionModel solution, string fullProjectPath, ISolutionSerializer? serializer = null, bool showMessageOnDuplicate = true)
    {
        string solutionRelativeProjectPath = Path.GetRelativePath(Path.GetDirectoryName(_solutionFileFullPath)!, fullProjectPath);

        // Open project instance to see if it is a valid project
        ProjectRootElement projectRootElement;
        try
        {
            projectRootElement = ProjectRootElement.Open(fullProjectPath);
        }
        catch (InvalidProjectFileException ex)
        {
            Reporter.Error.WriteLine(string.Format(CliStrings.InvalidProjectWithExceptionMessage, fullProjectPath, ex.Message));
            return;
        }

        ProjectInstance projectInstance = new ProjectInstance(projectRootElement);

        string projectTypeGuid = solution.ProjectTypes.FirstOrDefault(t => t.Extension == Path.GetExtension(fullProjectPath))?.ProjectTypeId.ToString()
            ?? projectRootElement.GetProjectTypeGuid() ?? projectInstance.GetDefaultProjectTypeGuid();

        // Generate the solution folder path based on the project path
        SolutionFolderModel? solutionFolder = GenerateIntermediateSolutionFoldersForProjectPath(solution, solutionRelativeProjectPath);

        SolutionProjectModel project;

        try
        {
            project = solution.AddProject(solutionRelativeProjectPath, projectTypeGuid, solutionFolder);
        }
        catch (SolutionArgumentException ex) when (ex.Type == SolutionErrorType.InvalidProjectTypeReference)
        {
            Reporter.Error.WriteLine(CliStrings.UnsupportedProjectType, fullProjectPath);
            return;
        }
        catch (SolutionArgumentException ex) when (ex.Type == SolutionErrorType.DuplicateProjectName || solution.FindProject(solutionRelativeProjectPath) is not null)
        {
            if (showMessageOnDuplicate)
            {
                Reporter.Output.WriteLine(CliStrings.SolutionAlreadyContainsProject, _solutionFileFullPath, solutionRelativeProjectPath);
            }
            return;
        }

        // Add settings based on existing project instance
        string projectInstanceId = projectInstance.GetProjectId();

        if (!string.IsNullOrEmpty(projectInstanceId) && serializer is ISolutionSerializer<SlnV12SerializerSettings>)
        {
            project.Id = new Guid(projectInstanceId);
        }

        var projectInstanceBuildTypes = projectInstance.GetConfigurations();
        var projectInstancePlatforms = projectInstance.GetPlatforms();

        foreach (var solutionPlatform in solution.Platforms)
        {
            var projectPlatform = projectInstancePlatforms.FirstOrDefault(
                platform => platform.Replace(" ", string.Empty) == solutionPlatform.Replace(" ", string.Empty), projectInstancePlatforms.FirstOrDefault()!);
            project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.Platform, "*", solutionPlatform, projectPlatform));
        }

        foreach (var solutionBuildType in solution.BuildTypes)
        {
            var projectBuildType = projectInstanceBuildTypes.FirstOrDefault(
                buildType => buildType.Replace(" ", string.Empty) == solutionBuildType.Replace(" ", string.Empty), projectInstanceBuildTypes.FirstOrDefault()!);
            project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.BuildType, solutionBuildType, "*", projectBuildType));
        }

        Reporter.Output.WriteLine(CliStrings.ProjectAddedToTheSolution, solutionRelativeProjectPath);

        // Get referencedprojects from the project instance
        var referencedProjectsFullPaths = projectInstance.GetItems("ProjectReference")
            .Select(item => Path.GetFullPath(item.EvaluatedInclude, Path.GetDirectoryName(fullProjectPath)!));

        if (_includeReferences)
        {
            foreach (var referencedProjectFullPath in referencedProjectsFullPaths)
            {
                AddProject(solution, referencedProjectFullPath, serializer, showMessageOnDuplicate: false);
            }
        }
    }

    private async Task AddProjectsToSolutionFilterAsync(IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        // Solution filter files don't support --in-root or --solution-folder options
        if (_inRoot || !string.IsNullOrEmpty(_solutionFolderPath))
        {
            throw new GracefulException(CliCommandStrings.SolutionFilterDoesNotSupportFolderOptions);
        }

        // Load the filtered solution to get the parent solution path and existing projects
        SolutionModel filteredSolution = SlnFileFactory.CreateFromFilteredSolutionFile(_solutionFileFullPath);
        string parentSolutionPath = filteredSolution.Description!; // The parent solution path is stored in Description

        // Load the parent solution to validate projects exist in it
        SolutionModel parentSolution = SlnFileFactory.CreateFromFileOrDirectory(parentSolutionPath);

        // Get existing projects in the filter (already normalized to OS separator by CreateFromFilteredSolutionFile)
        var existingProjects = filteredSolution.SolutionProjects.Select(p => p.FilePath).ToHashSet();

        // Get solution-relative paths for new projects
        var newProjects = new List<string>();
        string parentSolutionDirectory = Path.GetDirectoryName(parentSolutionPath) ?? string.Empty;
        foreach (var projectPath in projectPaths)
        {
            string parentSolutionRelativePath = Path.GetRelativePath(parentSolutionDirectory, projectPath);

            // Normalize to OS separator for consistent comparison
            parentSolutionRelativePath = parentSolutionRelativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            // Check if project exists in parent solution
            var projectInParent = parentSolution.FindProject(parentSolutionRelativePath);
            if (projectInParent is null)
            {
                Reporter.Error.WriteLine(CliStrings.ProjectNotFoundInTheSolution, parentSolutionRelativePath, parentSolutionPath);
                continue;
            }

            // Check if project is already in the filter
            if (existingProjects.Contains(parentSolutionRelativePath))
            {
                Reporter.Output.WriteLine(CliStrings.SolutionAlreadyContainsProject, _solutionFileFullPath, parentSolutionRelativePath);
                continue;
            }

            newProjects.Add(parentSolutionRelativePath);
            Reporter.Output.WriteLine(CliStrings.ProjectAddedToTheSolution, parentSolutionRelativePath);
        }

        // Add new projects to the existing list and save
        var allProjects = existingProjects.Concat(newProjects).OrderBy(p => p);
        SlnfFileHelper.SaveSolutionFilter(_solutionFileFullPath, parentSolutionPath, allProjects);

        await Task.CompletedTask;
    }
}
