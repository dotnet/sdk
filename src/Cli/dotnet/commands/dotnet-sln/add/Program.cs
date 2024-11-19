// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
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
            // SolutionModel::AddFolder expects path to have leading, trailing and inner forward slashes
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
            string solutionFileFullPath = SlnCommandParser.GetSlnFileFullPath(_fileOrDirectory);
            if (_projects.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }
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
            catch (GracefulException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is SolutionException || ex.InnerException is SolutionException)
                {
                    throw new GracefulException(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
                }
                throw new GracefulException(ex.Message, ex);
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
            SolutionFolderModel? solutionFolder = (!_inRoot && _solutionFolderPath != null)
                ? solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(_solutionFolderPath))
                : null;
            foreach (var projectPath in projectPaths)
            {
                string relativePath = Path.GetRelativePath(Path.GetDirectoryName(solutionFileFullPath), projectPath);
                try
                {
                    AddProjectWithDefaultGuid(solution, relativePath, solutionFolder);
                    Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectAddedToTheSolution, relativePath);
                }
                catch (InvalidProjectFileException ex)
                {
                    Reporter.Error.WriteLine(string.Format(CommonLocalizableStrings.InvalidProjectWithExceptionMessage, projectPath, ex.Message));
                }
                catch (ArgumentException ex)
                {
                    // TODO: There are some cases where the project is not found but it already exists on the solution. So it is useful to check the error message. Will remove on future commit.
                    // TODO: Update with error codes from vs-solutionpersistence
                    if (solution.FindProject(relativePath) != null || Regex.Match(ex.Message, @"Project name '.*' already exists in the solution folder.").Success)
                    {
                        Reporter.Output.WriteLine(CommonLocalizableStrings.SolutionAlreadyContainsProject, solutionFileFullPath, relativePath);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            AddDefaultProjectConfigurations(solution);
            await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
        }

        private void AddDefaultProjectConfigurations(SolutionModel solution)
        {
            string[] defaultConfigurationPlatforms = { "Any CPU", "x64", "x86" };
            foreach (var platform in defaultConfigurationPlatforms)
            {
                solution.AddPlatform(platform);
            }
            string[] defaultConfigurationBuildTypes = { "Debug", "Release" };
            foreach (var buildType in defaultConfigurationBuildTypes)
            {
                solution.AddBuildType(buildType);
            }
            if (solution.SolutionProjects.Count > 1)
            {
                // https://stackoverflow.com/a/14714485
                solution.RemoveProperties("HideSolutionNode");
            }
            solution.DistillProjectConfigurations();
        }

        private SolutionProjectModel AddProjectWithDefaultGuid(SolutionModel solution, string relativePath, SolutionFolderModel solutionFolder)
        {
            // Open project instance
            ProjectRootElement projectRootElement = ProjectRootElement.Open(relativePath);
            SolutionProjectModel project;
            try
            {
                project = solution.AddProject(relativePath, null, solutionFolder);
            }
            catch (ArgumentException ex)
            {
                // TODO: Update with error codes from vs-solutionpersistence
                if (ex.Message == "ProjectType '' not found. (Parameter 'projectTypeName')")
                {
                    project = solution.AddProject(relativePath, "130159A9-F047-44B3-88CF-0CF7F02ED50F", solutionFolder);
                }
                else
                {
                    throw;
                }
            }
            // Generate intermediate solution folders
            if (solutionFolder is null && !_inRoot)
            {
                var relativePathDirectory = Path.GetDirectoryName(relativePath);
                if (relativePathDirectory != null)
                {
                    SolutionFolderModel relativeSolutionFolder = solution.AddFolder(GetSolutionFolderPathWithForwardSlashes(relativePathDirectory));
                    project.MoveToFolder(relativeSolutionFolder);
                    // Avoid duplicate folder/project names
                    if (project.Parent is not null && project.Parent.ActualDisplayName == project.ActualDisplayName)
                    {
                        solution.RemoveFolder(project.Parent);
                    }
                }                
            }
            // Generate configurations and platforms
            ProjectInstance projectInstance = new ProjectInstance(projectRootElement);
            foreach (var buildType in projectInstance.GetConfigurations())
            {
                project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.BuildType, buildType, "*", buildType));
            }
            foreach (var platform in projectInstance.GetPlatforms())
            {
                project.AddProjectConfigurationRule(new ConfigurationRule(BuildDimension.Platform, "*", platform, platform));
            }
            return project;

        }
    }
}
