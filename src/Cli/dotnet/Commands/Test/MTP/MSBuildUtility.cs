// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.Runtime.CompilerServices;
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

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    private const string dotnetTestVerb = "dotnet-test";

    // Related: https://github.com/dotnet/msbuild/pull/7992
    // Related: https://github.com/dotnet/msbuild/issues/12711
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ProjectShouldBuild")]
    static extern bool ProjectShouldBuild(SolutionFile solutionFile, string projectFile);

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
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

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);

        using var collection = new ProjectCollection(globalProperties, loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = GetProjectsProperties(collection, evaluationContext, projectPaths, buildOptions);
        logger?.ReallyShutdown();
        collection.UnloadAllProjects();

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(buildOptions.MSBuildArgs, CommonOptions.CreatePropertyOption(), CommonOptions.CreateRestorePropertyOption(), CommonOptions.CreateMSBuildTargetOption(), CommonOptions.CreateVerbosityOption(), CommonOptions.CreateNoLogoOption());

        using var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs), logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection, evaluationContext, buildOptions, configuration: null, platform: null);
        logger?.ReallyShutdown();
        collection.UnloadAllProjects();
        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        LoggerUtility.SeparateBinLogArguments(parseResult.UnmatchedTokens, out var binLogArgs, out var otherArgs);

        var (positionalProjectOrSolution, positionalTestModules) = GetPositionalArguments(otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(definition)
            .Concat(binLogArgs);

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
            testModulesFilterOptionValue is not null && positionalTestModules is not null)
        {
            throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }

        PathOptions pathOptions = new(
            positionalProjectOrSolution ?? parseResult.GetValue(definition.ProjectOrSolutionOption),
            parseResult.GetValue(definition.SolutionOption),
            positionalTestModules ?? parseResult.GetValue(definition.TestModulesFilterOption),
            resultsDirectory,
            configFile,
            diagnosticOutputDirectory);

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(definition.NoRestoreOption),
            parseResult.GetValue(definition.NoBuildOption),
            parseResult.HasOption(definition.VerbosityOption) ? parseResult.GetValue(definition.VerbosityOption) : null,
            parseResult.GetValue(definition.NoLaunchProfileOption),
            parseResult.GetValue(definition.NoLaunchProfileArgumentsOption),
            otherArgs,
            msbuildArgs);
    }

    private static (string? PositionalProjectOrSolution, string? PositionalTestModules) GetPositionalArguments(List<string> otherArgs)
    {
        string? positionalProjectOrSolution = null;
        string? positionalTestModules = null;

        // In case there is a valid case, users can opt-out.
        // Note that the validation here is added to have a "better" error message for scenarios that will already fail.
        // So, disabling validation is okay if the user scenario is valid.
        bool throwOnUnexpectedFilePassedAsNonFirstPositionalArgument = Environment.GetEnvironmentVariable("DOTNET_TEST_DISABLE_SWITCH_VALIDATION") is not ("true" or "1");

        for (int i = 0; i < otherArgs.Count; i++)
        {
            var token = otherArgs[i];
            if ((token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseSolution);
                }
            }
            else if ((token.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs.RemoveAt(0);
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
                    otherArgs.RemoveAt(0);
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
                    positionalTestModules = token;
                    otherArgs.RemoveAt(0);
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


    private static bool BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
    {
        if (buildOptions.HasNoBuild)
        {
            return true;
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

        int result = new RestoringCommand(parsedMSBuildArgs, buildOptions.HasNoRestore).Execute();

        return result == (int)BuildResultCode.Success;
    }

    private static ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectsProperties(
        ProjectCollection projectCollection,
        EvaluationContext evaluationContext,
        IEnumerable<(string ProjectFilePath, string? Configuration, string? Platform)> projects,
        BuildOptions buildOptions)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        Parallel.ForEach(
            projects,
            // We don't use --max-parallel-test-modules here.
            // If user wants to limit the test applications run in parallel, we don't want to punish them and force the evaluation to also be limited.
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (project) =>
            {
                IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project.ProjectFilePath, projectCollection, evaluationContext, buildOptions, project.Configuration, project.Platform);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }
}
