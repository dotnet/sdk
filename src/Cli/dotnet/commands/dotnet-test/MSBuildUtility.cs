// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli;

internal static class MSBuildUtility
{
    private const string dotnetTestVerb = "dotnet-test";

    public static (IEnumerable<TestModule> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
    {
        SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<TestModule>(), isBuiltOrRestored);
        }

        string rootDirectory = solutionFilePath.HasExtension(".slnf") ?
                Path.GetDirectoryName(solutionModel.Description) :
                SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        ConcurrentBag<TestModule> projects = GetProjectsProperties(collection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);
        logger?.ReallyShutdown();

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<TestModule> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<TestModule>(), isBuiltOrRestored);
        }

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        IEnumerable<TestModule> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection);
        logger?.ReallyShutdown();

        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
    {
        var binLogArgs = new List<string>();
        var otherArgs = new List<string>();

        foreach (var arg in parseResult.UnmatchedTokens)
        {
            if (LoggerUtility.IsBinLogArgument(arg))
            {
                binLogArgs.Add(arg);
            }
            else
            {
                otherArgs.Add(arg);
            }
        }

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
            degreeOfParallelism,
            otherArgs,
            msbuildArgs);
    }

    private static bool BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
    {
        List<string> msbuildArgs = [.. buildOptions.MSBuildArgs];

        if (buildOptions.Verbosity is null)
        {
            msbuildArgs.Add($"-verbosity:quiet");
        }

        msbuildArgs.Add(filePath);
        msbuildArgs.Add($"-target:{CliConstants.MTPTarget}");

        int result = new RestoringCommand(msbuildArgs, buildOptions.HasNoRestore || buildOptions.HasNoBuild).Execute();

        return result == (int)BuildResultCode.Success;
    }

    private static ConcurrentBag<TestModule> GetProjectsProperties(ProjectCollection projectCollection, IEnumerable<string> projects, BuildOptions buildOptions)
    {
        var allProjects = new ConcurrentBag<TestModule>();

        Parallel.ForEach(
            projects,
            new ParallelOptions { MaxDegreeOfParallelism = buildOptions.DegreeOfParallelism },
            (project) =>
            {
                IEnumerable<TestModule> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, projectCollection);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }
}
