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
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    public static BuildOptions GetBuildOptions(ParseResult parseResult)
        => TestCommandOptions.GetBuildOptions(parseResult);

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

        if (VirtualProjectBuilder.IsValidEntryPointPath(projectFilePath))
        {
            return GetProjectsFromFile(projectFilePath, buildOptions, buildSession);
        }

        // Pre-build device selection: evaluate the project to select devices BEFORE building,
        // so that device-provided RuntimeIdentifiers are included in the build.
        var deviceSelection = SolutionAndProjectUtility.SelectDevicesBeforeBuild(
            projectFilePath,
            buildOptions,
            buildSession);

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

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    private static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, int BuildExitCode) GetProjectsFromFile(
        string entryPointFilePath,
        BuildOptions buildOptions,
        MSBuildSession buildSession)
    {
        var msbuildArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs);
        string fullEntryPointFilePath = Path.GetFullPath(entryPointFilePath);
        var buildCommand = new VirtualProjectBuildingCommand(
            fullEntryPointFilePath,
            msbuildArgs)
        {
            NoRestore = buildOptions.HasNoRestore,
            NoCache = true,
        };

        int buildExitCode = buildOptions.HasNoBuild ? 0 : buildCommand.Execute();
        if (buildExitCode != 0)
        {
            return ([], buildExitCode);
        }

        Dictionary<string, string> globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);
        ProjectInstance EvaluateProject(string? targetFramework)
        {
            var properties = new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase);
            if (targetFramework is not null)
            {
                properties[ProjectProperties.TargetFramework] = targetFramework;
            }

            var evaluationCommand = new VirtualProjectBuildingCommand(fullEntryPointFilePath, msbuildArgs);
            return evaluationCommand.CreateProjectInstance(buildSession.ProjectCollection, properties);
        }

        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects =
            SolutionAndProjectUtility.GetProjectProperties(
                VirtualProjectBuilder.GetVirtualProjectPath(fullEntryPointFilePath),
                EvaluateProject,
                buildOptions,
                buildSession);

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
        var nonDeviceProjects = new List<(string ProjectFilePath, string? Configuration, string? Platform)>();

        // Phase 1: Handle device projects sequentially. Per-TFM builds use in-process MSBuild
        // (BuildManager.DefaultBuildManager), which is a process-wide singleton and cannot run concurrently.
        // The shared session may stay open across them, because it owns a build manager of its own.
        foreach (var project in projects)
        {
            var deviceSelection = SolutionAndProjectUtility.SelectDevicesBeforeBuild(
                project.ProjectFilePath,
                buildOptions,
                buildSession,
                evaluationContext);

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
                nonDeviceProjects.Add(project);
            }
        }

        // Phase 2: Handle non-device projects in parallel (existing behavior).
        var gracefulExceptions = new ConcurrentQueue<GracefulException>();
        Parallel.ForEach(
            nonDeviceProjects,
            // We don't use --max-parallel-test-modules here.
            // If user wants to limit the test applications run in parallel, we don't want to punish them and force the evaluation to also be limited.
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (project) =>
            {
                try
                {
                    IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project.ProjectFilePath, projectCollection, evaluationContext, buildOptions, buildSession, project.Configuration, project.Platform, globalProperties);
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

        return (allProjects, 0);
    }
}
