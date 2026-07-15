// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class SolutionAndProjectUtility
{
    private static readonly string[] s_computeRunArgumentsTarget = [Constants.ComputeRunArguments];
    private static readonly Lock s_buildLock = new();

    /// <summary>
    /// Parses MSBuild args with the standard set of test command options.
    /// </summary>
    internal static MSBuildArgs AnalyzeStandardTestMSBuildArgs(IEnumerable<string> args) =>
        MSBuildArgs.AnalyzeMSBuildArguments(
            args,
            CommonOptions.CreatePropertyOption(),
            CommonOptions.CreateRestorePropertyOption(),
            CommonOptions.CreateMSBuildTargetOption(),
            CommonOptions.CreateVerbosityOption(),
            CommonOptions.CreateNoLogoOption());

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

    public static (bool SolutionFileFound, string Message) TryGetSolutionFilePath(string directory, out string solutionFilePath)
    {
        solutionFilePath = string.Empty;

        if (!Directory.Exists(directory))
        {
            return (false, string.Format(CliCommandStrings.CmdNonExistentDirectoryErrorDescription, directory));
        }

        var actualSolutionFiles = GetSolutionFilePaths(directory);

        if (actualSolutionFiles.Length == 0)
        {
            return (false, string.Format(CliStrings.SolutionDoesNotExist, directory + Path.DirectorySeparatorChar));
        }

        if (actualSolutionFiles.Length > 1)
        {
            return (false, string.Format(CliStrings.MoreThanOneSolutionInDirectory, directory + Path.DirectorySeparatorChar));
        }

        solutionFilePath = actualSolutionFiles[0];
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

    private static ProjectInstance EvaluateProject(
        ProjectCollection collection,
        EvaluationContext evaluationContext,
        string projectFilePath,
        string? tfm,
        string? configuration,
        string? platform)
    {
        Debug.Assert(projectFilePath is not null);

        Dictionary<string, string>? globalProperties = null;
        var capacity = 0;

        if (tfm is not null)
        {
            capacity++;
        }

        if (configuration is not null)
        {
            capacity++;
        }

        if (platform is not null)
        {
            capacity++;
        }

        if (capacity > 0)
        {
            globalProperties = new Dictionary<string, string>(capacity);
            if (tfm is not null)
            {
                globalProperties.Add(ProjectProperties.TargetFramework, tfm);
            }

            if (configuration is not null)
            {
                globalProperties.Add(ProjectProperties.Configuration, configuration);
            }

            if (platform is not null)
            {
                globalProperties.Add(ProjectProperties.Platform, platform);
            }
        }

        // Merge the global properties from the project collection.
        // It's unclear why MSBuild isn't considering the global properties defined in the ProjectCollection when
        // the collection is passed in ProjectOptions below.
        foreach (var property in collection.GlobalProperties)
        {
            if (!(globalProperties ??= new Dictionary<string, string>()).ContainsKey(property.Key))
            {
                globalProperties.Add(property.Key, property.Value);
            }
        }

        return ProjectInstance.FromFile(projectFilePath, new ProjectOptions
        {
            GlobalProperties = globalProperties,
            EvaluationContext = evaluationContext,
            ProjectCollection = collection,
        });
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectProperties(
        string projectFilePath,
        ProjectCollection projectCollection,
        EvaluationContext evaluationContext,
        BuildOptions buildOptions,
        string? configuration,
        string? platform,
        HashSet<string>? visitedTraversalProjects = null)
    {
        var projects = new List<ParallelizableTestModuleGroupWithSequentialInnerModules>();
        ProjectInstance projectInstance = EvaluateProject(projectCollection, evaluationContext, projectFilePath, tfm: null, configuration, platform);

        // Traversal projects (e.g. Microsoft.Build.Traversal "dirs.proj") are not test projects themselves.
        // They act as a container that forwards build/test operations to their ProjectReference items.
        // Special-case them the same way solutions are handled: expand into the referenced projects and
        // evaluate each of them. This is done recursively so that nested traversal projects work as well.
        if (IsTraversalProject(projectInstance))
        {
            // Track visited projects across the whole traversal graph so that a project referenced by
            // multiple traversal projects (a "diamond") is only tested once, and to guard against cycles
            // (a traversal project that transitively references itself).
            visitedTraversalProjects ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visitedTraversalProjects.Add(Path.GetFullPath(projectFilePath));

            foreach (var reference in GetTraversalReferencedProjects(projectInstance, configuration, platform))
            {
                if (!visitedTraversalProjects.Add(reference.FullPath))
                {
                    // Already handled via another traversal path (diamond) or a cycle.
                    continue;
                }

                projects.AddRange(GetProjectProperties(reference.FullPath, projectCollection, evaluationContext, buildOptions, reference.Configuration, reference.Platform, visitedTraversalProjects));
            }

            return projects;
        }

        var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
        var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);

        Logger.LogTrace($"Loaded project '{Path.GetFileName(projectFilePath)}' with TargetFramework '{targetFramework}', TargetFrameworks '{targetFrameworks}', IsTestProject '{projectInstance.GetPropertyValue(ProjectProperties.IsTestProject)}', and '{ProjectProperties.IsTestingPlatformApplication}' is '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}'.");

        if (!string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(targetFrameworks))
        {
            if (GetModuleFromProject(projectInstance, buildOptions) is { } module)
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
                    projectInstance = EvaluateProject(projectCollection, evaluationContext, projectFilePath, framework, configuration, platform);
                    Logger.LogTrace($"Loaded inner project '{Path.GetFileName(projectFilePath)}' has '{ProjectProperties.IsTestingPlatformApplication}' = '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}' (TFM: '{framework}').");

                    if (GetModuleFromProject(projectInstance, buildOptions) is { } module)
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
                    projectInstance = EvaluateProject(projectCollection, evaluationContext, projectFilePath, framework, configuration, platform);
                    Logger.LogTrace($"Loaded inner project '{Path.GetFileName(projectFilePath)}' has '{ProjectProperties.IsTestingPlatformApplication}' = '{projectInstance.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication)}' (TFM: '{framework}').");

                    if (GetModuleFromProject(projectInstance, buildOptions) is { } module)
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

    /// <summary>
    /// Determines whether the evaluated project is a traversal project (e.g. a
    /// <c>Microsoft.Build.Traversal</c> "dirs.proj"). Traversal projects set the
    /// <c>IsTraversal</c> property to <c>true</c> and merely forward operations to their
    /// <c>ProjectReference</c> items rather than producing a test module of their own.
    /// </summary>
    private static bool IsTraversalProject(ProjectInstance projectInstance)
        => bool.TryParse(projectInstance.GetPropertyValue(ProjectProperties.IsTraversal), out bool isTraversal) && isTraversal;

    /// <summary>
    /// Returns the projects a traversal project references. The globs and conditions in the traversal
    /// project are already expanded by MSBuild during evaluation, so the resolved <c>ProjectReference</c>
    /// items represent the effective set of projects to test. Per-reference <c>Configuration</c>/
    /// <c>Platform</c> metadata is honored when present (falling back to the values inherited from the
    /// traversal project), mirroring how MSBuild lets a <c>ProjectReference</c> target a specific
    /// configuration or platform.
    /// </summary>
    private static IEnumerable<(string FullPath, string? Configuration, string? Platform)> GetTraversalReferencedProjects(
        ProjectInstance projectInstance,
        string? inheritedConfiguration,
        string? inheritedPlatform)
    {
        foreach (ProjectItemInstance projectReference in projectInstance.GetItems(ProjectProperties.ProjectReferenceItemName))
        {
            // "FullPath" is a well-known item metadata that MSBuild computes relative to the project directory.
            var fullPath = projectReference.GetMetadataValue("FullPath");
            if (string.IsNullOrEmpty(fullPath))
            {
                continue;
            }

            var configurationMetadata = projectReference.GetMetadataValue(ProjectProperties.Configuration);
            var platformMetadata = projectReference.GetMetadataValue(ProjectProperties.Platform);

            yield return (
                Path.GetFullPath(fullPath),
                string.IsNullOrEmpty(configurationMetadata) ? inheritedConfiguration : configurationMetadata,
                string.IsNullOrEmpty(platformMetadata) ? inheritedPlatform : platformMetadata);
        }
    }

    /// <summary>
    /// RuntimeIdentifiers are included in the build. Returns a result with device mappings
    /// and TestTfmsInParallel setting, or null if no device selection is needed.
    /// When projectCollection/evaluationContext are provided, reuses them to avoid redundant evaluation.
    /// </summary>
    internal static DeviceSelectionResult? SelectDevicesBeforeBuild(
        string projectFilePath,
        BuildOptions buildOptions,
        ProjectCollection? projectCollection = null,
        EvaluationContext? evaluationContext = null)
    {
        // --device is already handled by HandleDeviceWithTargetFrameworkSelection
        if (!string.IsNullOrWhiteSpace(buildOptions.Device))
        {
            return null;
        }

        var msbuildArgs = AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs);

        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);

        // If Device is already set via -p:Device=..., skip device selection
        if (globalProperties.TryGetValue("Device", out var deviceProp) && !string.IsNullOrWhiteSpace(deviceProp))
        {
            return null;
        }

        // Create a ProjectCollection if one wasn't provided
        using var ownedCollection = projectCollection is null ? new ProjectCollection(globalProperties) : null;
        var collection = projectCollection ?? ownedCollection!;
        evaluationContext ??= EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

        var projectInstance = ProjectInstance.FromFile(projectFilePath, new ProjectOptions
        {
            GlobalProperties = collection.GlobalProperties,
            EvaluationContext = evaluationContext,
            ProjectCollection = collection,
        });

        // If the project doesn't support device selection, skip entirely
        if (!projectInstance.Targets.ContainsKey(Constants.ComputeAvailableDevices))
        {
            return null;
        }

        var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
        var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);

        // Read TestTfmsInParallel from the initial evaluation so callers don't need to re-evaluate
        bool testTfmsInParallel = true;
        if (bool.TryParse(projectInstance.GetPropertyValue(ProjectProperties.TestTfmsInParallel), out bool parsed) ||
            bool.TryParse(projectInstance.GetPropertyValue(ProjectProperties.BuildInParallel), out parsed))
        {
            testTfmsInParallel = parsed;
        }

        bool isInteractive = !Console.IsOutputRedirected && !new Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment();

        IEnumerable<string> frameworks;
        if (!string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(targetFrameworks))
        {
            // Single TFM (either explicit or via -f/--framework)
            frameworks = [targetFramework ?? string.Empty];
        }
        else
        {
            frameworks = targetFrameworks
                .Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f));
        }

        var devicesByTfm = new Dictionary<string, (string? Device, string? RuntimeIdentifier)>();
        foreach (var framework in frameworks)
        {
            var (device, rid) = SelectDeviceForTfm(projectFilePath, buildOptions, framework, isInteractive);
            devicesByTfm[framework] = (device, rid);
        }

        return devicesByTfm.Values.Any(v => v.Device is not null)
            ? new DeviceSelectionResult(devicesByTfm, testTfmsInParallel)
            : null;
    }

    internal sealed record DeviceSelectionResult(
        Dictionary<string, (string? Device, string? RuntimeIdentifier)> DevicesByTfm,
        bool TestTfmsInParallel);

    /// <summary>
    /// Selects a device for a specific TFM using RunCommandSelector.
    /// Returns (null, null) if no device support or no devices available for this TFM.
    /// </summary>
    private static (string? device, string? runtimeIdentifier) SelectDeviceForTfm(
        string projectFilePath,
        BuildOptions buildOptions,
        string? tfm,
        bool isInteractive)
    {
        var msbuildArgsToAppend = buildOptions.MSBuildArgs;
        if (!string.IsNullOrEmpty(tfm))
        {
            msbuildArgsToAppend = msbuildArgsToAppend.Append($"-p:{ProjectProperties.TargetFramework}={tfm}");
        }

        var msbuildArgs = AnalyzeStandardTestMSBuildArgs(msbuildArgsToAppend);

        using var selector = new RunCommandSelector(
            projectFilePath,
            isInteractive,
            msbuildArgs,
            ImmutableDictionary<string, string>.Empty,
            commandName: "dotnet test");

        lock (s_buildLock)
        {
            if (!selector.TrySelectDevice(
                listDevices: false,
                noRestore: buildOptions.HasNoRestore || buildOptions.HasNoBuild,
                out var selectedDevice,
                out var runtimeIdentifier,
                out _))
            {
                throw new GracefulException(
                    string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
            }

            return (selectedDevice, runtimeIdentifier);
        }
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    private static TestModule? GetModuleFromProject(ProjectInstance project, BuildOptions buildOptions)
    {
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestProject), out bool isTestProject);
        _ = bool.TryParse(project.GetPropertyValue(ProjectProperties.IsTestingPlatformApplication), out bool isTestingPlatformApplication);

        if (!isTestProject && !isTestingPlatformApplication)
        {
            return null;
        }

        string targetFramework = project.GetPropertyValue(ProjectProperties.TargetFramework);
        string projectFullPath = project.GetPropertyValue(ProjectProperties.ProjectFullPath);


        // Only get run properties if IsTestingPlatformApplication is true
        RunProperties runProperties;
        if (isTestingPlatformApplication)
        {
            runProperties = GetRunProperties(project);

            // dotnet run throws the same if RunCommand is null or empty.
            // In dotnet test, we are additionally checking that RunCommand is not dll.
            // In any "default" scenario, RunCommand is never dll.
            // If we found it to be dll, that is user explicitly setting RunCommand incorrectly.
            if (string.IsNullOrEmpty(runProperties.Command) || runProperties.Command.HasExtension(CliConstants.DLLExtension))
            {
                throw new GracefulException(
                    string.Format(
                        CliCommandStrings.RunCommandExceptionUnableToRun,
                        projectFullPath,
                        Product.TargetFrameworkVersion,
                        project.GetPropertyValue("OutputType")));
            }
        }
        else
        {
            // For VSTest test projects, create minimal RunProperties
            runProperties = new RunProperties(
                project.GetPropertyValue(ProjectProperties.TargetPath),
                null,
                null);
        }

        // TODO: Support --launch-profile and pass it here.
        var launchSettings = TryGetLaunchProfileSettings(Path.GetDirectoryName(projectFullPath)!, Path.GetFileNameWithoutExtension(projectFullPath), project.GetPropertyValue(ProjectProperties.AppDesignerFolder), buildOptions, profileName: null);

        var rootVariableName = EnvironmentVariableNames.TryGetDotNetRootArchVariableName(
            runProperties.RuntimeIdentifier,
            runProperties.DefaultAppHostRuntimeIdentifier);

        if (rootVariableName is not null && Environment.GetEnvironmentVariable(rootVariableName) != null)
        {
            // If already set, we do not override it.
            rootVariableName = null;
        }

        return new TestModule(runProperties, PathUtility.FixFilePath(projectFullPath), targetFramework, isTestingPlatformApplication, launchSettings, project.GetPropertyValue(ProjectProperties.TargetPath), rootVariableName);

        [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
        [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
        static RunProperties GetRunProperties(ProjectInstance project)
        {
            // Build API cannot be called in parallel, even if the projects are different.
            // Otherwise, BuildManager in MSBuild will fail:
            // System.InvalidOperationException: The operation cannot be completed because a build is already in progress.
            // NOTE: BuildManager is singleton.
            lock (s_buildLock)
            {
                if (!project.Build(s_computeRunArgumentsTarget, loggers: null))
                {
                    throw new GracefulException(CliCommandStrings.RunCommandEvaluationExceptionBuildFailed, s_computeRunArgumentsTarget[0]);
                }
            }

            return RunProperties.FromProject(project);
        }
    }

    private static LaunchProfile? TryGetLaunchProfileSettings(string projectDirectory, string projectNameWithoutExtension, string appDesignerFolder, BuildOptions buildOptions, string? profileName)
    {
        if (buildOptions.NoLaunchProfile)
        {
            return null;
        }

        var launchSettingsPath = LaunchSettings.GetPropertiesLaunchSettingsPath(projectDirectory, appDesignerFolder);
        bool hasLaunchSettings = File.Exists(launchSettingsPath);

        var runJsonPath = LaunchSettings.GetFlatLaunchSettingsPath(projectDirectory, projectNameWithoutExtension);
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

        // If buildOptions.Verbosity is null, we still want to print the message.
        if (buildOptions.Verbosity != VerbosityOptions.quiet)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
        }

        var result = LaunchSettings.ReadProfileSettingsFromFile(launchSettingsPath, profileName);
        if (!result.Successful)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, result.FailureReason).Bold().Red());
            return null;
        }

        return result.Profile;
    }
}
