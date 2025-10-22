// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    private const string dotnetTestVerb = "dotnet-test";

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        var solutionFile = SolutionFile.Parse(solutionFilePath);
        var projectConfiguration = $"{solutionFile.GetDefaultConfigurationName()}|{solutionFile.GetDefaultPlatformName()}";
        // TODO: What to do if the given key doesn't exist in the ProjectConfigurations?
        // MSBuild seems to be special casing web projects specifically!!
        // https://github.com/dotnet/msbuild/blob/243fb764b25affe8cc5f233001ead3b5742a297e/src/Build/Construction/Solution/SolutionProjectGenerator.cs#L659-L672
        // It doesn't make sense to have to duplicate the MSBuild logic here and having to maintain them in sync.
        // TODO: Ensure we are not breaking solution filters. ProjectsInOrder looks like it *might* contain everything from the solution?
        var projectPaths = solutionFile.ProjectsInOrder.Select(p => (p.ProjectConfigurations[projectConfiguration], p.AbsolutePath)).Where(p => p.Item1.IncludeInBuild).Select(p => p.AbsolutePath);
        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(buildOptions.MSBuildArgs, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CommonOptions.MSBuildTargetOption(), CommonOptions.VerbosityOption());

        using var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs), loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
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

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(buildOptions.MSBuildArgs, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CommonOptions.MSBuildTargetOption(), CommonOptions.VerbosityOption());

        using var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs), logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
        var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection, evaluationContext, buildOptions);
        logger?.ReallyShutdown();
        collection.UnloadAllProjects();
        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult)
    {
        LoggerUtility.SeparateBinLogArguments(parseResult.UnmatchedTokens, out var binLogArgs, out var otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
            .Concat(binLogArgs);

        string? resultsDirectory = parseResult.GetValue(MicrosoftTestingPlatformOptions.ResultsDirectoryOption);
        if (resultsDirectory is not null)
        {
            resultsDirectory = Path.GetFullPath(resultsDirectory);
        }

        string? configFile = parseResult.GetValue(MicrosoftTestingPlatformOptions.ConfigFileOption);
        if (configFile is not null)
        {
            configFile = Path.GetFullPath(configFile);
        }

        string? diagnosticOutputDirectory = parseResult.GetValue(MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption);
        if (diagnosticOutputDirectory is not null)
        {
            diagnosticOutputDirectory = Path.GetFullPath(diagnosticOutputDirectory);
        }

        PathOptions pathOptions = new(
            parseResult.GetValue(MicrosoftTestingPlatformOptions.ProjectOption),
            parseResult.GetValue(MicrosoftTestingPlatformOptions.SolutionOption),
            resultsDirectory,
            configFile,
            diagnosticOutputDirectory);

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(CommonOptions.NoRestoreOption),
            parseResult.GetValue(MicrosoftTestingPlatformOptions.NoBuildOption),
            parseResult.HasOption(TestCommandParser.VerbosityOption) ? parseResult.GetValue(TestCommandParser.VerbosityOption) : null,
            parseResult.GetValue(MicrosoftTestingPlatformOptions.NoLaunchProfileOption),
            parseResult.GetValue(MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption),
            otherArgs,
            msbuildArgs);
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

        var parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(msbuildArgs, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, TestCommandParser.MTPTargetOption, TestCommandParser.VerbosityOption);

        int result = new RestoringCommand(parsedMSBuildArgs, buildOptions.HasNoRestore).Execute();

        return result == (int)BuildResultCode.Success;
    }

    private static ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectsProperties(ProjectCollection projectCollection, EvaluationContext evaluationContext, IEnumerable<string> projects, BuildOptions buildOptions)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        Parallel.ForEach(
            projects,
            // We don't use --max-parallel-test-modules here.
            // If user wants to limit the test applications run in parallel, we don't want to punish them and force the evaluation to also be limited.
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (project) =>
            {
                IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, projectCollection, evaluationContext, buildOptions);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }
}
