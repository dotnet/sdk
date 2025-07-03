// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class DotnetTestLogger : Microsoft.Build.Framework.ILogger
{
    private IEventSource? _eventSource;

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        _eventSource = eventSource;
        _eventSource.TargetFinished += OnTargetFinished;
    }

    private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
    {
        // Here, check if target name is _MTPTest, and access e.TargetOutputs to collect the info needed to run.
    }

    public void Shutdown() { }
}

internal static class MSBuildUtility
{
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

        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), null, toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = GetProjectsProperties(collection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), null, toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection, buildOptions.NoLaunchProfile);

        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
    {
        LoggerUtility.SeparateBinLogArguments(parseResult.UnmatchedTokens, out var binLogArgs, out var otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
            .Concat(binLogArgs);

        PathOptions pathOptions = new(parseResult.GetValue(
            TestingPlatformOptions.ProjectOption),
            parseResult.GetValue(TestingPlatformOptions.SolutionOption),
            parseResult.GetValue(TestingPlatformOptions.DirectoryOption));

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(CommonOptions.NoRestoreOption),
            parseResult.GetValue(TestingPlatformOptions.NoBuildOption),
            parseResult.HasOption(CommonOptions.VerbosityOption) ? parseResult.GetValue(CommonOptions.VerbosityOption) : null,
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileOption),
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileArgumentsOption),
            degreeOfParallelism,
            otherArgs,
            msbuildArgs);
    }

    private static bool BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
    {
        Debugger.Launch();
        List<string> msbuildArgs = [.. buildOptions.MSBuildArgs];

        if (buildOptions.Verbosity is null)
        {
            msbuildArgs.Add($"-verbosity:quiet");
        }

        msbuildArgs.Add(filePath);
        msbuildArgs.Add($"-target:{CliConstants.MTPTest}");
        if (buildOptions.HasNoBuild)
        {
            msbuildArgs.Add($"-property:MTPNoBuild=true");
        }
        msbuildArgs.Add($"-logger:{typeof(DotnetTestLogger).FullName},{typeof(DotnetTestLogger).Assembly.FullName}");

        int result = new RestoringCommand(msbuildArgs, buildOptions.HasNoRestore).Execute();

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
                IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, projectCollection, buildOptions.NoLaunchProfile);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }
}
