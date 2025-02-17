// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using NuGet.Packaging;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SolutionAndProjectUtility
    {
        public static (bool SolutionOrProjectFileFound, string Message) TryGetProjectOrSolutionFilePath(string directory, out string projectOrSolutionFilePath, out bool isSolution)
        {
            projectOrSolutionFilePath = string.Empty;
            isSolution = false;

            if (!Directory.Exists(directory))
            {
                return (false, string.Format(LocalizableStrings.CmdNonExistentDirectoryErrorDescription, directory));
            }

            var solutionPaths = GetSolutionFilePaths(directory);

            // If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
            if (solutionPaths.Length > 1)
            {
                return (false, string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
            }

            if (solutionPaths.Length == 1)
            {
                var projectPaths = GetProjectFilePaths(directory);

                if (projectPaths.Length == 0)
                {
                    projectOrSolutionFilePath = solutionPaths[0];
                    isSolution = true;
                    return (true, string.Empty);
                }

                return (false, LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }
            else  // If no solutions are found, look for a project file
            {
                string[] projectPaths = GetProjectFilePaths(directory);

                if (projectPaths.Length == 0)
                {
                    var solutionFilterPaths = GetSolutionFilterFilePaths(directory);

                    if (solutionFilterPaths.Length == 0)
                    {
                        return (false, LocalizableStrings.CmdNoProjectOrSolutionFileErrorDescription);
                    }

                    if (solutionFilterPaths.Length == 1)
                    {
                        projectOrSolutionFilePath = solutionFilterPaths[0];
                        isSolution = true;
                        return (true, string.Empty);
                    }
                    else
                    {
                        return (false, LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
                    }
                }

                if (projectPaths.Length == 1)
                {
                    projectOrSolutionFilePath = projectPaths[0];
                    return (true, string.Empty);
                }

                return (false, string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
            }
        }

        private static string[] GetSolutionFilePaths(string directory)
        {
            string[] solutionFiles = Directory.GetFiles(directory, CliConstants.SolutionExtensionPattern, SearchOption.TopDirectoryOnly);
            solutionFiles.AddRange(Directory.GetFiles(directory, CliConstants.SolutionXExtensionPattern, SearchOption.TopDirectoryOnly));

            return solutionFiles;
        }

        private static string[] GetSolutionFilterFilePaths(string directory)
        {
            return Directory.GetFiles(directory, CliConstants.SolutionFilterExtensionPattern, SearchOption.TopDirectoryOnly);
        }

        private static string[] GetProjectFilePaths(string directory) => [.. Directory.EnumerateFiles(directory, CliConstants.ProjectExtensionPattern, SearchOption.TopDirectoryOnly).Where(IsProjectFile)];

        private static bool IsProjectFile(string filePath) => CliConstants.ProjectExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

        public static string GetRootDirectory(string solutionOrProjectFilePath)
        {
            string fileDirectory = Path.GetDirectoryName(solutionOrProjectFilePath);
            return string.IsNullOrEmpty(fileDirectory) ? Directory.GetCurrentDirectory() : fileDirectory;
        }

        public static IEnumerable<Module> GetProjectProperties(string projectFilePath, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            var project = projectCollection.LoadProject(projectFilePath, globalProperties, null);
            return GetModulesFromProject(project);
        }

        private static List<Module> GetModulesFromProject(Project project)
        {
            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);

            if (!isTestProject)
            {
                return [];
            }

            _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);

            string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
            string targetFrameworks = project.GetPropertyValue(ProjectProperties.TargetFrameworks);
            string targetPath = project.GetPropertyValue(ProjectProperties.TargetPath);
            string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);
            string runSettingsFilePath = project.GetPropertyValue(ProjectProperties.RunSettingsFilePath);

            var projects = new List<Module>();

            if (string.IsNullOrEmpty(targetFrameworks))
            {
                projects.Add(new Module(targetPath, PathUtility.FixFilePath(projectFullPath), targetFramework, runSettingsFilePath, isTestingPlatformApplication, isTestProject));
            }
            else
            {
                var frameworks = targetFrameworks.Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries);
                foreach (var framework in frameworks)
                {
                    project.SetProperty(ProjectProperties.TargetFramework, framework);
                    project.ReevaluateIfNecessary();

                    projects.Add(new Module(project.GetPropertyValue(ProjectProperties.TargetPath),
                        PathUtility.FixFilePath(projectFullPath),
                        framework,
                        runSettingsFilePath,
                        isTestingPlatformApplication,
                        isTestProject));
                }
            }

            return projects;
        }
    }
}
