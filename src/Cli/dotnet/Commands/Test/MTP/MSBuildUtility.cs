// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Build.Definition;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    // Related: https://github.com/dotnet/msbuild/pull/7992
    // Related: https://github.com/dotnet/msbuild/issues/12711
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ProjectShouldBuild")]
    static extern bool ProjectShouldBuild(SolutionFile solutionFile, string projectFile);

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, int BuildExitCode) GetProjectsFromSolution(
        string solutionFilePath,
        BuildOptions buildOptions,
        MSBuildSession buildSession)
    {
        using var _ = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

        int buildExitCode = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

        if (buildExitCode != 0)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), buildExitCode);
        }

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(buildOptions.MSBuildArgs, CommonOptions.CreatePropertyOption(), CommonOptions.CreateRestorePropertyOption(), CommonOptions.CreateMSBuildTargetOption(), CommonOptions.CreateVerbosityOption(), CommonOptions.CreateNoLogoOption());
        var solutionFile = SolutionFile.Parse(Path.GetFullPath(solutionFilePath));
        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);

        globalProperties.TryGetValue("Configuration", out var activeSolutionConfiguration);
        globalProperties.TryGetValue("Platform", out var activeSolutionPlatform);

        if (string.IsNullOrEmpty(activeSolutionConfiguration))
        {
            activeSolutionConfiguration = solutionFile.GetDefaultConfigurationName();
        }

        if (string.IsNullOrEmpty(activeSolutionPlatform))
        {
            activeSolutionPlatform = solutionFile.GetDefaultPlatformName();
        }

        var solutionConfiguration = solutionFile.SolutionConfigurations.FirstOrDefault(c => activeSolutionConfiguration.Equals(c.ConfigurationName, StringComparison.OrdinalIgnoreCase) && activeSolutionPlatform.Equals(c.PlatformName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The solution configuration '{activeSolutionConfiguration}|{activeSolutionPlatform}' is invalid.");

        // Note: MSBuild seems to be special casing web projects specifically.
        // https://github.com/dotnet/msbuild/blob/243fb764b25affe8cc5f233001ead3b5742a297e/src/Build/Construction/Solution/SolutionProjectGenerator.cs#L659-L672
        // There is no interest to duplicate this workaround here in test command, unless MSBuild provides a public API that does it.
        // https://github.com/dotnet/msbuild/issues/12711 tracks having a better public API.
        var projectPaths = solutionFile.ProjectsInOrder
            .Where(p => ProjectShouldBuild(solutionFile, p.RelativePath) && p.ProjectConfigurations.ContainsKey(solutionConfiguration.FullName))
            .Select(p => (p.ProjectConfigurations[solutionConfiguration.FullName], p.AbsolutePath))
            .Where(p => p.Item1.IncludeInBuild)
            .Select(p => (p.AbsolutePath, (string?)p.Item1.ConfigurationName, (string?)p.Item1.PlatformName));

        var collection = buildSession.ProjectCollection;
        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        var (projects, deviceBuildExitCode) = GetProjectsProperties(collection, evaluationContext, projectPaths, buildOptions, globalProperties, buildSession);

        return (projects, deviceBuildExitCode != 0 ? deviceBuildExitCode : buildExitCode);
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, int BuildExitCode) GetProjectsFromProject(
        string projectFilePath,
        BuildOptions buildOptions,
        MSBuildSession buildSession)
    {
        using var _ = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

        // Pre-build device selection: evaluate the project to select devices BEFORE building,
        // so that device-provided RuntimeIdentifiers are included in the build.
        var deviceEvaluation = SolutionAndProjectUtility.EvaluateProjectForDeviceSelection(
            projectFilePath,
            buildOptions,
            buildSession);
        var deviceSelection = deviceEvaluation is not null
            ? SolutionAndProjectUtility.SelectDevicesBeforeBuild(
                projectFilePath,
                buildOptions,
                buildSession,
                deviceEvaluation)
            : null;

        if (deviceSelection is not null)
        {
            return BuildPerTfmWithDevices(projectFilePath, buildOptions, deviceSelection, buildSession);
        }

        int buildExitCode = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

        if (buildExitCode != 0)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), buildExitCode);
        }

        var collection = buildSession.ProjectCollection;
        // A fresh evaluation context: the one device selection used above ran before the build, so it
        // caches a view of the file system that predates the build outputs.
        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        var msbuildArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs);
        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(
            projectFilePath, collection, evaluationContext, buildOptions, buildSession, configuration: null, platform: null,
            CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs));
        return (projects, buildExitCode);
    }

    /// <summary>
    /// Builds each TFM separately with its selected device/RuntimeIdentifier injected, then
    /// evaluates each to get test modules. This ensures device-provided RIDs are part of the build.
    /// </summary>
    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    private static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, int BuildExitCode) BuildPerTfmWithDevices(
        string projectFilePath,
        BuildOptions buildOptions,
        SolutionAndProjectUtility.DeviceSelectionResult deviceSelection,
        MSBuildSession buildSession,
        string? configuration = null,
        string? platform = null)
    {
        var allGroups = new List<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        foreach (var (tfm, (device, rid)) in deviceSelection.DevicesByTfm)
        {
            var perTfmArgs = buildOptions.MSBuildArgs;
            if (!string.IsNullOrEmpty(tfm))
            {
                perTfmArgs = perTfmArgs.Append($"-p:{ProjectProperties.TargetFramework}={tfm}");
            }

            if (device is not null)
            {
                perTfmArgs = perTfmArgs.Append($"-p:Device={device}");
            }

            if (!string.IsNullOrEmpty(rid))
            {
                perTfmArgs = perTfmArgs.Append($"-p:RuntimeIdentifier={rid}");
            }

            if (!string.IsNullOrEmpty(configuration))
            {
                perTfmArgs = perTfmArgs.Append($"-p:Configuration={configuration}");
            }

            if (!string.IsNullOrEmpty(platform))
            {
                perTfmArgs = perTfmArgs.Append($"-p:Platform={platform}");
            }

            var perTfmBuildOptions = buildOptions with
            {
                HasNoRestore = buildOptions.HasNoRestore ||
                    (deviceSelection.RestoreWasPerformed && string.IsNullOrEmpty(rid)),
                MSBuildArgs = perTfmArgs,
                Device = device,
            };

            int exitCode = BuildOrRestoreProjectOrSolution(projectFilePath, perTfmBuildOptions);
            if (exitCode != 0)
            {
                return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), exitCode);
            }

            var msbuildArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(perTfmBuildOptions.MSBuildArgs);

            // The target framework, device and runtime identifier of this iteration are passed as
            // per-project global properties instead of through a project collection of their own: every
            // project built in the session has to come from the collection the session owns.
            var perTfmGlobalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);
            var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
            IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> modules = SolutionAndProjectUtility.GetProjectProperties(
                projectFilePath, buildSession.ProjectCollection, evaluationContext, perTfmBuildOptions, buildSession, configuration, platform, perTfmGlobalProperties);

            allGroups.AddRange(modules);
        }

        // When TestTfmsInParallel is false, merge all modules into one sequential group
        if (!deviceSelection.TestTfmsInParallel && allGroups.Count > 1)
        {
            var allModules = new List<TestModule>();
            foreach (var group in allGroups)
            {
                if (group.Modules is not null)
                {
                    allModules.AddRange(group.Modules);
                }
                else if (group.Module is not null)
                {
                    allModules.Add(group.Module);
                }
            }

            return (allModules.Count > 0
                ? [new ParallelizableTestModuleGroupWithSequentialInnerModules(allModules)]
                : [], 0);
        }

        return (allGroups, 0);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        LoggerUtility.SeparateLoggerArguments(parseResult.UnmatchedTokens, out var loggerArgs, out var otherArgs);

        if (parseResult.GetValue(definition.NoLogoOption) && !otherArgs.Contains("--no-banner"))
        {
            otherArgs = otherArgs.Add("--no-banner");
        }

        var (positionalProjectOrSolution, positionalTestModules) = GetPositionalArguments(ref otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(definition)
            .Concat(loggerArgs);

        string? resultsDirectory = parseResult.GetValue(definition.ResultsDirectoryOption);
        if (resultsDirectory is not null)
        {
            resultsDirectory = Path.GetFullPath(resultsDirectory);
        }

        string? configFile = parseResult.GetValue(definition.ConfigFileOption);
        if (configFile is not null)
        {
            configFile = Path.GetFullPath(configFile);
        }

        string? diagnosticOutputDirectory = parseResult.GetValue(definition.DiagnosticOutputDirectoryOption);
        if (diagnosticOutputDirectory is not null)
        {
            diagnosticOutputDirectory = Path.GetFullPath(diagnosticOutputDirectory);
        }

        var projectOrSolutionOptionValue = parseResult.GetValue(definition.ProjectOrSolutionOption);
        var testModulesFilterOptionValue = parseResult.GetValue(definition.TestModulesFilterOption);

        if ((projectOrSolutionOptionValue is not null && positionalProjectOrSolution is not null) ||
            (testModulesFilterOptionValue is not null && positionalTestModules is not null))
        {
            throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }

        PathOptions pathOptions = new(
            positionalProjectOrSolution ?? parseResult.GetValue(definition.ProjectOrSolutionOption),
            parseResult.GetValue(definition.SolutionOption),
            positionalTestModules ?? parseResult.GetValue(definition.TestModulesFilterOption),
            resultsDirectory,
            parseResult.GetValue(definition.ResultsDirectoryLayoutOption) == "per-module"
                ? ResultsDirectoryLayout.PerModule
                : ResultsDirectoryLayout.Flat,
            configFile,
            diagnosticOutputDirectory,
            parseResult.HasOption(definition.ResultsDirectoryLayoutOption));

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(definition.NoRestoreOption),
            parseResult.GetValue(definition.NoBuildOption),
            parseResult.HasOption(definition.VerbosityOption) ? parseResult.GetValue(definition.VerbosityOption) : null,
            parseResult.GetValue(definition.NoLaunchProfileOption),
            parseResult.GetValue(definition.NoLaunchProfileArgumentsOption),
            otherArgs,
            msbuildArgs,
            Device: parseResult.GetValue(definition.DeviceOption),
            ListDevices: parseResult.GetValue(definition.ListDevicesOption),
            EnvironmentVariables: parseResult.GetValue(definition.EnvOption) ?? ImmutableDictionary<string, string>.Empty);
    }

    private static (string? PositionalProjectOrSolution, string? PositionalTestModules) GetPositionalArguments(ref ImmutableArray<string> otherArgs)
    {
        string? positionalProjectOrSolution = null;
        string? positionalTestModules = null;

        // In case there is a valid case, users can opt-out.
        // Note that the validation here is added to have a "better" error message for scenarios that will already fail.
        // So, disabling validation is okay if the user scenario is valid.
        bool throwOnUnexpectedFilePassedAsNonFirstPositionalArgument = Environment.GetEnvironmentVariable("DOTNET_TEST_DISABLE_SWITCH_VALIDATION") is not ("true" or "1");

        for (int i = 0; i < otherArgs.Length; i++)
        {
            var token = otherArgs[i];
            if ((token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseSolution);
                }
            }
            else if (Path.GetExtension(token).EndsWith("proj", StringComparison.OrdinalIgnoreCase) && File.Exists(token))
            {
                // Any MSBuild project extension ending in "proj" (.csproj, .vbproj, .fsproj, and traversal
                // container projects such as dirs.proj / *.proj). This mirrors ValidateProjectOrSolutionPath,
                // which accepts any "*proj" extension. Recognizing it here ensures the project path is not
                // accidentally forwarded to the test application as an argument.
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseProject);
                }
            }
            else if ((token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                      token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                     File.Exists(token))
            {
                if (i == 0)
                {
                    positionalTestModules = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseTestModules);
                }
            }
            else if (Directory.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseDirectoryWithSwitch);
                }
            }
        }

        return (positionalProjectOrSolution, positionalTestModules);
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    private static int BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
    {
        if (buildOptions.HasNoBuild)
        {
            return 0;
        }

        List<string> msbuildArgs = [.. buildOptions.MSBuildArgs, filePath];

        if (buildOptions.Verbosity is null)
        {
            msbuildArgs.Add($"-verbosity:quiet");
        }

        var parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(
            msbuildArgs,
            CommonOptions.CreatePropertyOption(),
            CommonOptions.CreateRestorePropertyOption(),
            CommonOptions.CreateRequiredMSBuildTargetOption(TestCommandDefinition.MicrosoftTestingPlatform.BuildTargetName),
            CommonOptions.CreateVerbosityOption(),
            CommonOptions.CreateNoLogoOption());

        string? envPropsFile = null;
        try
        {
            if (buildOptions.EnvironmentVariables.Count > 0 &&
                Path.GetExtension(filePath).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(parsedMSBuildArgs);
                using var collection = new ProjectCollection(globalProperties);
                var project = ProjectInstance.FromFile(filePath, new ProjectOptions
                {
                    GlobalProperties = globalProperties,
                    EvaluationStage = ProjectEvaluationStage.Items,
                    ProjectCollection = collection,
                });

                if (EnvironmentVariablesToMSBuild.HasRuntimeEnvironmentVariableSupport(project))
                {
                    envPropsFile = EnvironmentVariablesToMSBuild.CreatePropsFile(
                        filePath,
                        buildOptions.EnvironmentVariables,
                        "dotnet-test-env.props",
                        project.GetPropertyValue(Constants.IntermediateOutputPath));
                    parsedMSBuildArgs = EnvironmentVariablesToMSBuild.AddPropsFileToArgs(parsedMSBuildArgs, envPropsFile);
                }
            }

            return new RestoringCommand(parsedMSBuildArgs, buildOptions.HasNoRestore).Execute();
        }
        finally
        {
            EnvironmentVariablesToMSBuild.DeletePropsFile(envPropsFile);
        }
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    private static (ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, int BuildExitCode) GetProjectsProperties(
        ProjectCollection projectCollection,
        EvaluationContext evaluationContext,
        IEnumerable<(string ProjectFilePath, string? Configuration, string? Platform)> projects,
        BuildOptions buildOptions,
        IReadOnlyDictionary<string, string> globalProperties,
        MSBuildSession buildSession)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();
        var solutionProjects = projects.ToArray();
        var deviceProjects = new (
            string ProjectFilePath,
            string? Configuration,
            string? Platform,
            SolutionAndProjectUtility.DeviceSelectionEvaluation Evaluation)?[solutionProjects.Length];
        var gracefulExceptions = new ConcurrentQueue<GracefulException>();

        // Phase 1: Evaluate projects in parallel. Non-device projects are processed immediately
        // using the same instances, while device projects are retained for the sequential phase.
        Parallel.For(
            fromInclusive: 0,
            toExclusive: solutionProjects.Length,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            index =>
            {
                var project = solutionProjects[index];
                try
                {
                    var evaluation = SolutionAndProjectUtility.EvaluateProjectForDeviceSelection(
                        project.ProjectFilePath,
                        buildOptions,
                        buildSession,
                        evaluationContext,
                        project.Configuration,
                        project.Platform);

                    if (evaluation?.SupportsDeviceSelection == true)
                    {
                        deviceProjects[index] = (
                            project.ProjectFilePath,
                            project.Configuration,
                            project.Platform,
                            evaluation);
                        return;
                    }

                    IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata =
                        SolutionAndProjectUtility.GetProjectProperties(
                            project.ProjectFilePath,
                            projectCollection,
                            evaluationContext,
                            buildOptions,
                            buildSession,
                            project.Configuration,
                            project.Platform,
                            globalProperties,
                            preEvaluatedProjects: evaluation?.EvaluatedProjects);
                    foreach (var projectMetadata in projectsMetadata)
                    {
                        allProjects.Add(projectMetadata);
                    }
                }
                catch (GracefulException ex)
                {
                    gracefulExceptions.Enqueue(ex);
                }
            });

        if (gracefulExceptions.TryDequeue(out GracefulException? gracefulException))
        {
            throw gracefulException;
        }

        // Phase 2: Select, build and inspect device projects sequentially. These operations use
        // in-process MSBuild and may prompt for a device, so they cannot run concurrently.
        foreach (var deviceProject in deviceProjects)
        {
            if (deviceProject is not { } project)
            {
                continue;
            }

            var deviceSelection = SolutionAndProjectUtility.SelectDevicesBeforeBuild(
                project.ProjectFilePath,
                buildOptions,
                buildSession,
                project.Evaluation,
                project.Configuration,
                project.Platform);

            if (deviceSelection is not null)
            {
                var (modules, exitCode) = BuildPerTfmWithDevices(
                    project.ProjectFilePath,
                    buildOptions,
                    deviceSelection,
                    buildSession,
                    project.Configuration,
                    project.Platform);
                if (exitCode != 0)
                {
                    return (allProjects, exitCode);
                }

                foreach (var module in modules)
                {
                    allProjects.Add(module);
                }
            }
            else
            {
                throw new InvalidOperationException($"Device selection unexpectedly returned no result for '{project.ProjectFilePath}'.");
            }
        }

        return (allProjects, 0);
    }
}
