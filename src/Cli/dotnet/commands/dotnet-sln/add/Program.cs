// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
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

        public AddProjectToSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
            _projects = (IReadOnlyCollection<string>)(parseResult.GetValue(SlnAddParser.ProjectPathArgument) ?? []);
            _inRoot = parseResult.GetValue(SlnAddParser.InRootOption);
            _solutionFolderPath = parseResult.GetValue(SlnAddParser.SolutionFolderOption);
            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SlnArgumentValidator.CommandType.Add, _inRoot, _solutionFolderPath);
        }

        public override int Execute()
        {
            if (_projects.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }
            string solutionFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);

            try
            {
                PathUtility.EnsureAllPathsExist(_projects, CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);
                IEnumerable<string> fullProjectPaths = _projects.Select(project =>
                {
                    var fullPath = Path.GetFullPath(project);
                    return Directory.Exists(fullPath) ? MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName : fullPath;
                });
                AddProjectsToSolutionAsync(solutionFileFullPath, fullProjectPaths, CancellationToken.None).Wait();
                return 0;
            }
            catch (Exception ex) when (ex is not GracefulException)
            {
                {
                    if (ex is SolutionException || ex.InnerException is SolutionException)
                    {
                        throw new GracefulException(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
                    }
                    throw new GracefulException(ex.Message, ex);
                }
            }
        }

        private async Task AddProjectsToSolutionAsync(string solutionFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
        {
            ISolutionSerializer serializer = SlnCommandParser.GetSolutionSerializer(solutionFileFullPath);
            SolutionModel solution = await serializer.OpenAsync(solutionFileFullPath, cancellationToken);
            // set UTF8 BOM encoding for .sln
            if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
            {
                solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
                {
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
                });
            }
            // Set default configurations and platforms for sln file
            foreach (var platform in new[]{ "Any CPU", "x64", "x86" })
            {
                solution.AddPlatform(platform);
            }
            foreach (var buildType in new []{ "Debug", "Release" })
            {
                solution.AddBuildType(buildType);
            }

            SolutionFolderModel? solutionFolder = (!_inRoot && !string.IsNullOrEmpty(_solutionFolderPath))
                ? solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(_solutionFolderPath))
                : null;

            foreach (var projectPath in projectPaths)
            {
                string relativePath = Path.GetRelativePath(Path.GetDirectoryName(solutionFileFullPath), projectPath);
                try
                {
                    AddProject(solution, relativePath, projectPath, solutionFolder);
                }
                catch (InvalidProjectFileException ex)
                {
                    Reporter.Error.WriteLine(string.Format(CommonLocalizableStrings.InvalidProjectWithExceptionMessage, projectPath, ex.Message));
                }
                catch (SolutionArgumentException ex) when (solution.FindProject(relativePath) != null || ex.Type == SolutionErrorType.DuplicateProjectName)
                {
                    Reporter.Output.WriteLine(CommonLocalizableStrings.SolutionAlreadyContainsProject, solutionFileFullPath, relativePath);
                }
            }
            if (solution.SolutionProjects.Count > 1)
            {
                // https://stackoverflow.com/a/14714485
                solution.RemoveProperties("HideSolutionNode");
            }
            await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
        }

        private void AddProject(SolutionModel solution, string solutionRelativeProjectPath, string fullPath, SolutionFolderModel solutionFolder)
        {
            // Open project instance to see if it is a valid project
            ProjectRootElement projectRootElement = ProjectRootElement.Open(fullPath);
            SolutionProjectModel project;
            try
            {
                project = solution.AddProject(solutionRelativeProjectPath, null, solutionFolder);
            }
            catch (SolutionArgumentException ex) when (ex.ParamName == "projectTypeName")
            {
                // If guid is not identified by vs-solutionpersistence, check in project element itself
                var guid = projectRootElement.GetProjectTypeGuid();
                if (string.IsNullOrEmpty(guid))
                {
                    Reporter.Error.WriteLine(CommonLocalizableStrings.UnsupportedProjectType, fullPath);
                    return;
                }
                project = solution.AddProject(solutionRelativeProjectPath, guid, solutionFolder);
            }
            // Generate intermediate solution folders if nested project and solution folder not specified
            if (!_inRoot && solutionFolder is null)
            {
                var relativeDirectoryPath = Path.GetDirectoryName(solutionRelativeProjectPath);
                if (!string.IsNullOrEmpty(relativeDirectoryPath))
                {
                    SolutionFolderModel relativeSolutionFolder = solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(relativeDirectoryPath));
                    project.MoveToFolder(relativeSolutionFolder);
                    // Avoid duplicate folder/project names (eg, App/App.csproj)
                    if (project.Parent is not null && project.Parent.ActualDisplayName == project.ActualDisplayName)
                    {
                        solution.RemoveFolder(project.Parent);
                    }
                }                
            }
            // Add settings based on existing project instance
            ProjectInstance projectInstance = new ProjectInstance(projectRootElement);
            string projectInstanceId = projectInstance.GetProjectId();
            if (!string.IsNullOrEmpty(projectInstanceId))
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
            
            Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectAddedToTheSolution, solutionRelativeProjectPath);
        }
    }
}
