// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
    private static readonly string[] _defaultPlatforms = ["Any CPU", "x64", "x86"];
    private static readonly string[] _defaultBuildTypes = ["Debug", "Release"];
    private readonly string _fileOrDirectory;
    private readonly bool _inRoot;
    private readonly IReadOnlyCollection<string> _projects;
    private readonly string? _solutionFolderPath;

    private static string GetSolutionFolderPathWithForwardSlashes(string path)
    {
        // SolutionModel::AddFolder expects paths to have leading, trailing and inner forward slashes
        // https://github.com/microsoft/vs-solutionpersistence/blob/87ee8ea069662d55c336a9bd68fe4851d0384fa5/src/Microsoft.VisualStudio.SolutionPersistence/Model/SolutionModel.cs#L171C1-L172C1
        return "/" + string.Join("/", PathUtility.GetPathWithDirectorySeparator(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)) + "/";
    }

    public SolutionAddCommand(ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SolutionCommandParser.SlnArgument);
        _projects = (IReadOnlyCollection<string>)(parseResult.GetValue(SolutionAddCommandParser.ProjectPathArgument) ?? []);
        _inRoot = parseResult.GetValue(SolutionAddCommandParser.InRootOption);
        _solutionFolderPath = parseResult.GetValue(SolutionAddCommandParser.SolutionFolderOption);
        SolutionArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SolutionArgumentValidator.CommandType.Add, _inRoot, _solutionFolderPath);
    }

    public override int Execute()
    {
        if (_projects.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneProjectToAdd);
        }
        string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_fileOrDirectory);

        try
        {
            PathUtility.EnsureAllPathsExist(_projects, CliStrings.CouldNotFindProjectOrDirectory, true);
            IEnumerable<string> fullProjectPaths = _projects.Select(project =>
            {
                var fullPath = Path.GetFullPath(project);
                return Directory.Exists(fullPath) ? MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName : fullPath;
            });
            AddProjectsToSolutionAsync(solutionFileFullPath, fullProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex) when (ex is not GracefulException)
        {
            {
                if (ex is SolutionException || ex.InnerException is SolutionException)
                {
                    throw new GracefulException(CliStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
                }
                throw new GracefulException(ex.Message, ex);
            }
        }
    }

    private async Task AddProjectsToSolutionAsync(string solutionFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        ISolutionSerializer serializer = solution.SerializerExtension.Serializer;
        // set UTF8 BOM encoding for .sln
        if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
        {
            solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            });
            // Set default configurations and platforms for sln file
            foreach (var platform in _defaultPlatforms)
            {
                solution.AddPlatform(platform);
            }
            foreach (var buildType in _defaultBuildTypes)
            {
                solution.AddBuildType(buildType);
            }
        }

        SolutionFolderModel? solutionFolder = !_inRoot && !string.IsNullOrEmpty(_solutionFolderPath)
            ? solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(_solutionFolderPath))
            : null;

        foreach (var projectPath in projectPaths)
        {
            string relativePath = Path.GetRelativePath(Path.GetDirectoryName(solutionFileFullPath), projectPath);
            // Add fallback solution folder if relative path does not contain `..`.
            string relativeSolutionFolder =  relativePath.Split(Path.DirectorySeparatorChar).Any(p => p == "..")
                ? string.Empty : Path.GetDirectoryName(relativePath);

            if (!_inRoot && solutionFolder is null && !string.IsNullOrEmpty(relativeSolutionFolder))
            {
                if (relativeSolutionFolder.Split(Path.DirectorySeparatorChar).LastOrDefault() == Path.GetFileNameWithoutExtension(relativePath))
                {
                    relativeSolutionFolder = Path.Combine([.. relativeSolutionFolder.Split(Path.DirectorySeparatorChar).SkipLast(1)]);
                }
                if (!string.IsNullOrEmpty(relativeSolutionFolder))
                {
                    solutionFolder = solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(relativeSolutionFolder));
                }
            }

            try
            {
                AddProject(solution, relativePath, projectPath, solutionFolder, serializer);
            }
            catch (InvalidProjectFileException ex)
            {
                Reporter.Error.WriteLine(string.Format(CliStrings.InvalidProjectWithExceptionMessage, projectPath, ex.Message));
            }
            catch (SolutionArgumentException ex) when (solution.FindProject(relativePath) != null || ex.Type == SolutionErrorType.DuplicateProjectName)
            {
                Reporter.Output.WriteLine(CliStrings.SolutionAlreadyContainsProject, solutionFileFullPath, relativePath);
            }
        }
        await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
    }

    private static void AddProject(SolutionModel solution, string solutionRelativeProjectPath, string fullPath, SolutionFolderModel? solutionFolder, ISolutionSerializer serializer = null)
    {
        // Open project instance to see if it is a valid project
        ProjectRootElement projectRootElement = ProjectRootElement.Open(fullPath);
        ProjectInstance projectInstance = new ProjectInstance(projectRootElement);
        SolutionProjectModel project;
        try
        {
            project = solution.AddProject(solutionRelativeProjectPath, null, solutionFolder);
        }
        catch (SolutionArgumentException ex) when (ex.ParamName == "projectTypeName")
        {
            // If guid is not identified by vs-solutionpersistence, check in project element itself
            var guid = projectRootElement.GetProjectTypeGuid() ?? projectInstance.GetDefaultProjectTypeGuid();
            if (string.IsNullOrEmpty(guid))
            {
                Reporter.Error.WriteLine(CliStrings.UnsupportedProjectType, fullPath);
                return;
            }
            project = solution.AddProject(solutionRelativeProjectPath, guid, solutionFolder);
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
                platform => platform.Replace(" ", string.Empty) ==  solutionPlatform.Replace(" ", string.Empty), projectInstancePlatforms.FirstOrDefault());
            project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.Platform, "*", solutionPlatform, projectPlatform));
        }

        foreach (var solutionBuildType in solution.BuildTypes)
        {
            var projectBuildType = projectInstanceBuildTypes.FirstOrDefault(
                buildType => buildType.Replace(" ", string.Empty) == solutionBuildType.Replace(" ", string.Empty), projectInstanceBuildTypes.FirstOrDefault());
            project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.BuildType, solutionBuildType, "*", projectBuildType));
        }
        Reporter.Output.WriteLine(CliStrings.ProjectAddedToTheSolution, solutionRelativeProjectPath);
    }
}
