// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Build.Evaluation;
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
        SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        string rootDirectory = solutionFilePath.HasExtension(".slnf") ?
                Path.GetDirectoryName(solutionModel.Description)! :
                SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = GetProjectsProperties(collection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);
        logger?.ReallyShutdown();

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
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection, buildOptions);
        logger?.ReallyShutdown();

        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
    {
        LoggerUtility.SeparateBinLogArguments(parseResult.UnmatchedTokens, out var binLogArgs, out var otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
            .Concat(binLogArgs);

        string? resultsDirectory = parseResult.GetValue(TestingPlatformOptions.ResultsDirectoryOption);
        if (resultsDirectory is not null)
        {
            resultsDirectory = Path.GetFullPath(resultsDirectory);
        }

        string? configFile = parseResult.GetValue(TestingPlatformOptions.ConfigFileOption);
        if (configFile is not null)
        {
            configFile = Path.GetFullPath(configFile);
        }

        string? diagnosticOutputDirectory = parseResult.GetValue(TestingPlatformOptions.DiagnosticOutputDirectoryOption);
        if (diagnosticOutputDirectory is not null)
        {
            diagnosticOutputDirectory = Path.GetFullPath(diagnosticOutputDirectory);
        }

        PathOptions pathOptions = new(
            parseResult.GetValue(TestingPlatformOptions.ProjectOption),
            parseResult.GetValue(TestingPlatformOptions.SolutionOption),
            resultsDirectory,
            configFile,
            diagnosticOutputDirectory);

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(CommonOptions.NoRestoreOption),
            parseResult.GetValue(TestingPlatformOptions.NoBuildOption),
            parseResult.HasOption(TestCommandParser.VerbosityOption) ? parseResult.GetValue(TestCommandParser.VerbosityOption) : null,
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileOption),
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileArgumentsOption),
            degreeOfParallelism,
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

    private static ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectsProperties(ProjectCollection projectCollection, IEnumerable<string> projects, BuildOptions buildOptions)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        Parallel.ForEach(
            projects,
            new ParallelOptions { MaxDegreeOfParallelism = buildOptions.DegreeOfParallelism },
            (project) =>
            {
                IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, projectCollection, buildOptions);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }
}
