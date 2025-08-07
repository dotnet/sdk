// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class SolutionAndProjectUtility
{
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

    public static string GetRootDirectory(string solutionOrProjectFilePath)
    {
        string? fileDirectory = Path.GetDirectoryName(solutionOrProjectFilePath);
        Debug.Assert(fileDirectory is not null);
        return string.IsNullOrEmpty(fileDirectory) ? Directory.GetCurrentDirectory() : fileDirectory;
    }

    public static IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectProperties(
        string projectFilePath,
        bool noLaunchProfile,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties)
    {
        var projects = new List<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        if (!collectedProperties.TryGetValue(projectFilePath, out var propertySets) || propertySets == null || propertySets.Count == 0)
            return projects;

        // Find the "outer" context (meta-project) which has empty TargetFramework but non-empty TargetFrameworks
        var outerProps = propertySets.FirstOrDefault(props =>
            string.IsNullOrWhiteSpace(props.GetValueOrDefault(ProjectProperties.TargetFramework)) &&
            !string.IsNullOrWhiteSpace(props.GetValueOrDefault(ProjectProperties.TargetFrameworks)));

        // Check if this is a multi-TFM project
        if (outerProps != null)
        {
            // Multi-TFM project - use outer build properties for parallelization settings
            var targetFrameworks = outerProps.GetValueOrDefault(ProjectProperties.TargetFrameworks) ?? string.Empty;

            Logger.LogTrace(() => $"Loaded project '{Path.GetFileName(projectFilePath)}' with TargetFramework '', TargetFrameworks '{targetFrameworks}', IsTestProject '{outerProps.GetValueOrDefault(ProjectProperties.IsTestProject)}', and '{ProjectProperties.IsTestingPlatformApplication}' is '{outerProps.GetValueOrDefault(ProjectProperties.IsTestingPlatformApplication)}'.");

            // Determine if we should run TFMs in parallel using outer build properties
            if (!bool.TryParse(outerProps.GetValueOrDefault(ProjectProperties.TestTfmsInParallel), out bool testTfmsInParallel) &&
                !bool.TryParse(outerProps.GetValueOrDefault(ProjectProperties.BuildInParallel), out testTfmsInParallel))
            {
                testTfmsInParallel = true;
            }

            // Get only the inner build contexts (those with specific TargetFramework values)
            var tfmPropertySets = propertySets
                .Where(props => !string.IsNullOrWhiteSpace(props.GetValueOrDefault(ProjectProperties.TargetFramework)))
                .ToList();

            // Defensive: If no TFM property sets, return empty
            if (tfmPropertySets.Count == 0)
                return projects;

            if (testTfmsInParallel)
            {
                // Each TFM gets its own group (parallelizable)
                foreach (var properties in tfmPropertySets)
                {
                    if (!TryCreateTestModule(properties, out var module, noLaunchProfile))
                        continue;

                    projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(module));
                }
            }
            else
            {
                // All TFMs are grouped together and run sequentially
                List<TestModule>? innerModules = null;
                foreach (var properties in tfmPropertySets)
                {
                    if (!TryCreateTestModule(properties, out var module, noLaunchProfile))
                        continue;

                    innerModules ??= new List<TestModule>();
                    innerModules.Add(module);
                }

                if (innerModules is not null)
                {
                    projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(innerModules));
                }
            }
        }
        else
        {
            // Single-TFM project - look for the single property set with a TargetFramework
            var singleTfmProps = propertySets.FirstOrDefault(props =>
                !string.IsNullOrWhiteSpace(props.GetValueOrDefault(ProjectProperties.TargetFramework)));

            if (singleTfmProps != null)
            {
                var targetFramework = singleTfmProps.GetValueOrDefault(ProjectProperties.TargetFramework) ?? string.Empty;

                Logger.LogTrace(() => $"Loaded project '{Path.GetFileName(projectFilePath)}' with TargetFramework '{targetFramework}', TargetFrameworks '', IsTestProject '{singleTfmProps.GetValueOrDefault(ProjectProperties.IsTestProject)}', and '{ProjectProperties.IsTestingPlatformApplication}' is '{singleTfmProps.GetValueOrDefault(ProjectProperties.IsTestingPlatformApplication)}'.");

                if (TryCreateTestModule(singleTfmProps, out var module, noLaunchProfile))
                {
                    projects.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(module));
                }
            }
        }

        return projects;
    }

    private static bool TryCreateTestModule(
        IReadOnlyDictionary<string, string> properties,
        out TestModule module,
        bool noLaunchProfile)
    {
        module = null!;
        bool.TryParse(properties.GetValueOrDefault(ProjectProperties.IsTestProject), out bool isTestProject);
        bool.TryParse(properties.GetValueOrDefault(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);


        if (!isTestProject && !isTestingPlatformApplication)
            return false;

        string? targetFramework = properties.GetValueOrDefault(ProjectProperties.TargetFramework);
        string? projectFullPath = properties.GetValueOrDefault(ProjectProperties.ProjectFullPath);
        string? appDesignerFolder = properties.GetValueOrDefault(ProjectProperties.AppDesignerFolder);

        // Defensive: Ensure required properties are present
        if (string.IsNullOrEmpty(projectFullPath))
            return false;

        var runProperties = RunProperties.FromPropertiesAndApplicationArguments(properties);

        // dotnet run throws the same if RunCommand is null or empty.
        // In dotnet test, we are additionally checking that RunCommand is not dll.
        // In any "default" scenario, RunCommand is never dll.
        // If we found it to be dll, that is user explicitly setting RunCommand incorrectly.
        if (string.IsNullOrEmpty(runProperties.RunCommand) || runProperties.RunCommand.HasExtension(CliConstants.DLLExtension))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.RunCommandExceptionUnableToRun,
                    "dotnet test",
                    "OutputType",
                    properties.GetValueOrDefault("OutputType") ?? string.Empty));
        }

        var launchSettings = TryGetLaunchProfileSettings(
            Path.GetDirectoryName(projectFullPath)!,
            Path.GetFileNameWithoutExtension(projectFullPath),
            appDesignerFolder ?? string.Empty,
            noLaunchProfile,
            profileName: null);

        module = new TestModule(
            runProperties,
            PathUtility.FixFilePath(projectFullPath),
            targetFramework,
            isTestingPlatformApplication,
            isTestProject,
            launchSettings,
            properties.GetValueOrDefault(ProjectProperties.TargetPath)!);

        return true;
    }

    private static ProjectLaunchSettingsModel? TryGetLaunchProfileSettings(string projectDirectory, string projectNameWithoutExtension, string appDesignerFolder, bool noLaunchProfile, string? profileName)
    {
        if (noLaunchProfile)
        {
            return null;
        }

        var launchSettingsPath = CommonRunHelpers.GetPropertiesLaunchSettingsPath(projectDirectory, appDesignerFolder);
        bool hasLaunchSettings = File.Exists(launchSettingsPath);

        var runJsonPath = CommonRunHelpers.GetFlatLaunchSettingsPath(projectDirectory, projectNameWithoutExtension);
        bool hasRunJson = File.Exists(runJsonPath);

        if (hasLaunchSettings)
        {
            if (hasRunJson)
            {
                Reporter.Output.WriteLine(string.Format(CliCommandStrings.RunCommandWarningRunJsonNotUsed, runJsonPath, launchSettingsPath).Yellow());
            }
        }
        else if (hasRunJson)
        {
            launchSettingsPath = runJsonPath;
        }
        else
        {
            return null;
        }

        var result = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsPath, profileName);
        if (!result.Success)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, result.FailureReason).Bold().Red());
            return null;
        }

        return result.LaunchSettings;
    }
}
