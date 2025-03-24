// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

internal static class SolutionAndProjectUtility
{
    private static readonly string s_computeRunArgumentsTarget = "ComputeRunArguments";
    private static readonly Lock s_buildLock = new();

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

    private static string[] GetSolutionFilePaths(string directory) => [
            .. Directory.GetFiles(directory, CliConstants.SolutionExtensionPattern, SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(directory, CliConstants.SolutionXExtensionPattern, SearchOption.TopDirectoryOnly)
        ];

    private static string[] GetSolutionFilterFilePaths(string directory)
    {
        return Directory.GetFiles(directory, CliConstants.SolutionFilterExtensionPattern, SearchOption.TopDirectoryOnly);
    }

    private static string[] GetProjectFilePaths(string directory) => [.. Directory.EnumerateFiles(directory, CliConstants.ProjectExtensionPattern, SearchOption.TopDirectoryOnly).Where(IsProjectFile)];

    private static bool IsProjectFile(string filePath) => CliConstants.ProjectExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

    private static ProjectInstance EvaluateProject(ProjectCollection collection, string projectFilePath, string? tfm)
    {
        Debug.Assert(projectFilePath is not null);

        var project = collection.LoadProject(projectFilePath);
        if (tfm is not null)
        {
            project.SetGlobalProperty(ProjectProperties.TargetFramework, tfm);
            project.ReevaluateIfNecessary();
        }

        return project.CreateProjectInstance();
    }

    public static string GetRootDirectory(string solutionOrProjectFilePath)
    {
        string fileDirectory = Path.GetDirectoryName(solutionOrProjectFilePath);
        return string.IsNullOrEmpty(fileDirectory) ? Directory.GetCurrentDirectory() : fileDirectory;
    }

    public static IEnumerable<TestModule> GetProjectProperties(string projectFilePath, ProjectCollection projectCollection)
    {
        var projects = new List<TestModule>();
        ProjectInstance projectInstance = EvaluateProject(projectCollection, projectFilePath, null);

        var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
        var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);
        Logger.LogTrace(() => $"Loaded project '{Path.GetFileName(projectFilePath)}' with TargetFramework '{targetFramework}', TargetFrameworks '{targetFrameworks}', IsTestProject '{projectInstance.GetPropertyValue(ProjectProperties.IsTestProject)}', and '{ProjectProperties.IsTestingPlatformApplication}' is '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}'.");

        if (!string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(targetFrameworks))
        {
            if (GetModuleFromProject(projectInstance, projectCollection.Loggers) is { } module)
            {
                projects.Add(module);
            }
        }
        else
        {
            var frameworks = targetFrameworks.Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries);
            foreach (var framework in frameworks)
            {
                projectInstance = EvaluateProject(projectCollection, projectFilePath, framework);
                Logger.LogTrace(() => $"Loaded inner project '{Path.GetFileName(projectFilePath)}' has '{ProjectProperties.IsTestingPlatformApplication}' = '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}' (TFM: '{framework}').");

                if (GetModuleFromProject(projectInstance, projectCollection.Loggers) is { } module)
                {
                    projects.Add(module);
                }
            }
        }

        return projects;
    }

    private static TestModule? GetModuleFromProject(ProjectInstance project, ICollection<ILogger>? loggers)
    {
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);

        if (!isTestProject && !isTestingPlatformApplication)
        {
            return null;
        }

        string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
        RunProperties runProperties = GetRunProperties(project, loggers);
        string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);

        return new TestModule(runProperties, PathUtility.FixFilePath(projectFullPath), targetFramework, isTestingPlatformApplication, isTestProject);

        static RunProperties GetRunProperties(ProjectInstance project, ICollection<ILogger>? loggers)
        {
            // Build API cannot be called in parallel, even if the projects are different.
            // Otherwise, BuildManager in MSBuild will fail:
            // System.InvalidOperationException: The operation cannot be completed because a build is already in progress.
            // NOTE: BuildManager is singleton.
            lock (s_buildLock)
            {
                if (!project.Build(s_computeRunArgumentsTarget, loggers: loggers))
                {
                    Logger.LogTrace(() => $"The target {s_computeRunArgumentsTarget} failed to build. Falling back to TargetPath.");
                    return new RunProperties(project.GetPropertyValue(ProjectProperties.TargetPath), null, null);
                }
            }

            return RunProperties.FromProjectAndApplicationArguments(project, Array.Empty<string>(), fallbackToTargetPath: true);
        }
    }
}
