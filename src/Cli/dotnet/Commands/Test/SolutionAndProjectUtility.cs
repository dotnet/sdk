// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

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
            return (false, string.Format(CliCommandStrings.CmdNonExistentDirectoryErrorDescription, directory));
        }

        var actualSolutionFiles = GetSolutionFilePaths(directory);
        var solutionFilterFiles = GetSolutionFilterFilePaths(directory);
        var actualProjectFiles = GetProjectFilePaths(directory);

        // NOTE: The logic here is duplicated from https://github.com/dotnet/msbuild/blob/b878078fbaa28491a3a7fb273474ba71675c1613/src/MSBuild/XMake.cs#L3589
        // If there is exactly 1 project file and exactly 1 solution file
        if (actualProjectFiles.Length == 1 && actualSolutionFiles.Length == 1)
        {
            // Grab the name of both project and solution without extensions
            string solutionName = Path.GetFileNameWithoutExtension(actualSolutionFiles[0]);
            string projectName = Path.GetFileNameWithoutExtension(actualProjectFiles[0]);

            // Compare the names and error if they are not identical
            if (!string.Equals(solutionName, projectName))
            {
                return (false, CliCommandStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }

            projectOrSolutionFilePath = actualSolutionFiles[0];
            isSolution = true;
        }
        // If there is more than one solution file in the current directory we have no idea which one to use
        else if (actualSolutionFiles.Length > 1)
        {
            return (false, string.Format(CliStrings.MoreThanOneSolutionInDirectory, directory));
        }
        // If there is more than one project file in the current directory we may be able to figure it out
        else if (actualProjectFiles.Length > 1)
        {
            // We have more than one project, it is ambiguous at the moment
            bool isAmbiguousProject = true;

            // If there are exactly two projects and one of them is a .proj use that one and ignore the other
            if (actualProjectFiles.Length == 2)
            {
                string firstPotentialProjectExtension = Path.GetExtension(actualProjectFiles[0]);
                string secondPotentialProjectExtension = Path.GetExtension(actualProjectFiles[1]);

                // If the two projects have the same extension we can't decide which one to pick
                if (!string.Equals(firstPotentialProjectExtension, secondPotentialProjectExtension, StringComparison.OrdinalIgnoreCase))
                {
                    // Check to see if the first project is the proj, if it is use it
                    if (string.Equals(firstPotentialProjectExtension, ".proj", StringComparison.OrdinalIgnoreCase))
                    {
                        projectOrSolutionFilePath = actualProjectFiles[0];
                        // We have made a decision
                        isAmbiguousProject = false;
                    }
                    // If the first project is not the proj check to see if the second one is the proj, if so use it
                    else if (string.Equals(secondPotentialProjectExtension, ".proj", StringComparison.OrdinalIgnoreCase))
                    {
                        projectOrSolutionFilePath = actualProjectFiles[1];
                        // We have made a decision
                        isAmbiguousProject = false;
                    }
                }
            }

            if (isAmbiguousProject)
            {
                return (false, string.Format(CliStrings.MoreThanOneProjectInDirectory, directory));
            }
        }
        // if there are no project, solution filter, or solution files in the directory, we can't build
        else if (actualProjectFiles.Length == 0 &&
                 actualSolutionFiles.Length == 0 &&
                 solutionFilterFiles.Length == 0)
        {
            return (false, CliCommandStrings.CmdNoProjectOrSolutionFileErrorDescription);
        }
        else
        {
            // We are down to only one project, solution, or solution filter.
            // If only 1 solution build the solution.  If only 1 project build the project. Otherwise, build the solution filter.
            projectOrSolutionFilePath = actualSolutionFiles.Length == 1 ? actualSolutionFiles[0] : actualProjectFiles.Length == 1 ? actualProjectFiles[0] : solutionFilterFiles[0];
            isSolution = actualSolutionFiles.Length == 1 || (actualProjectFiles.Length != 1 && solutionFilterFiles.Length == 1);
            if (actualSolutionFiles.Length != 1 &&
                actualProjectFiles.Length != 1 &&
                solutionFilterFiles.Length != 1)
            {
                return (false, CliCommandStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }
        }

        return (true, string.Empty);
    }

    private static string[] GetSolutionFilePaths(string directory) => [
            .. Directory.GetFiles(directory, CliConstants.SolutionExtensionPattern, SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(directory, CliConstants.SolutionXExtensionPattern, SearchOption.TopDirectoryOnly)
        ];

    private static string[] GetSolutionFilterFilePaths(string directory)
    {
        return Directory.GetFiles(directory, CliConstants.SolutionFilterExtensionPattern, SearchOption.TopDirectoryOnly);
    }

    private static string[] GetProjectFilePaths(string directory) => Directory.GetFiles(directory, CliConstants.ProjectExtensionPattern, SearchOption.TopDirectoryOnly);

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
        string? fileDirectory = Path.GetDirectoryName(solutionOrProjectFilePath);
        Debug.Assert(fileDirectory is not null);
        return string.IsNullOrEmpty(fileDirectory) ? Directory.GetCurrentDirectory() : fileDirectory;
    }

    public static IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectProperties(string projectFilePath, ProjectCollection projectCollection, bool noLaunchProfile)
    {
        var projects = new List<ParallelizableTestModuleGroupWithSequentialInnerModules>();
        ProjectInstance projectInstance = EvaluateProject(projectCollection, projectFilePath, null);

        var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
        var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);

        Logger.LogTrace(() => $"Loaded project '{Path.GetFileName(projectFilePath)}' with TargetFramework '{targetFramework}', TargetFrameworks '{targetFrameworks}', IsTestProject '{projectInstance.GetPropertyValue(ProjectProperties.IsTestProject)}', and '{ProjectProperties.IsTestingPlatformApplication}' is '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}'.");

        if (!string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(targetFrameworks))
        {
            if (GetModuleFromProject(projectInstance, projectCollection.Loggers, noLaunchProfile) is { } module)
            {
                projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(module));
            }
        }
        else
        {
            if (!bool.TryParse(projectInstance.GetPropertyValue(ProjectProperties.TestTfmsInParallel), out bool testTfmsInParallel) &&
                !bool.TryParse(projectInstance.GetPropertyValue(ProjectProperties.BuildInParallel), out testTfmsInParallel))
            {
                // TestTfmsInParallel takes precedence over BuildInParallel.
                // If, for some reason, we cannot parse either property as bool, we default to true.
                testTfmsInParallel = true;
            }

            var frameworks = targetFrameworks
                .Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct();

            if (testTfmsInParallel)
            {
                foreach (var framework in frameworks)
                {
                    projectInstance = EvaluateProject(projectCollection, projectFilePath, framework);
                    Logger.LogTrace(() => $"Loaded inner project '{Path.GetFileName(projectFilePath)}' has '{ProjectProperties.IsTestingPlatformApplication}' = '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}' (TFM: '{framework}').");

                    if (GetModuleFromProject(projectInstance, projectCollection.Loggers, noLaunchProfile) is { } module)
                    {
                        projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(module));
                    }
                }
            }
            else
            {
                List<TestModule>? innerModules = null;
                foreach (var framework in frameworks)
                {
                    projectInstance = EvaluateProject(projectCollection, projectFilePath, framework);
                    Logger.LogTrace(() => $"Loaded inner project '{Path.GetFileName(projectFilePath)}' has '{ProjectProperties.IsTestingPlatformApplication}' = '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}' (TFM: '{framework}').");

                    if (GetModuleFromProject(projectInstance, projectCollection.Loggers, noLaunchProfile) is { } module)
                    {
                        innerModules ??= new List<TestModule>();
                        innerModules.Add(module);
                    }
                }

                if (innerModules is not null)
                {
                    projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(innerModules));
                }
            }
        }

        return projects;
    }

    private static TestModule? GetModuleFromProject(ProjectInstance project, ICollection<ILogger>? loggers, bool noLaunchProfile)
    {
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);

        if (!isTestProject && !isTestingPlatformApplication)
        {
            return null;
        }

        string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
        RunProperties runProperties = GetRunProperties(project, loggers);
        if (string.IsNullOrEmpty(runProperties.RunCommand) || runProperties.RunCommand.HasExtension(CliConstants.DLLExtension))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.RunCommandExceptionUnableToRun,
                    "dotnet test",
                    "OutputType",
                    project.GetPropertyValue("OutputType")));

        }

        string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);

        // TODO: Support --launch-profile and pass it here.
        var launchSettings = TryGetLaunchProfileSettings(Path.GetDirectoryName(projectFullPath)!, project.GetPropertyValue(ProjectProperties.AppDesignerFolder), noLaunchProfile, profileName: null);

        return new TestModule(runProperties, PathUtility.FixFilePath(projectFullPath), targetFramework, isTestingPlatformApplication, isTestProject, launchSettings, project.GetPropertyValue(ProjectProperties.TargetPath));

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
                    throw new GracefulException(CliCommandStrings.RunCommandEvaluationExceptionBuildFailed, s_computeRunArgumentsTarget);
                }
            }

            return RunProperties.FromProjectAndApplicationArguments(project, []);
        }
    }

    private static ProjectLaunchSettingsModel? TryGetLaunchProfileSettings(string projectDirectory, string appDesignerFolder, bool noLaunchProfile, string? profileName)
    {
        if (noLaunchProfile)
        {
            return null;
        }

        var launchSettingsPath = Path.Combine(projectDirectory, appDesignerFolder, "launchSettings.json");
        if (!File.Exists(launchSettingsPath))
        {
            return null;
        }

        var result = LaunchSettingsManager.TryApplyLaunchSettings(File.ReadAllText(launchSettingsPath), profileName);
        if (!result.Success)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, result.FailureReason).Bold().Red());
            return null;
        }

        return result.LaunchSettings;
    }
}
