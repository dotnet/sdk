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

        // TODO: We should pass a binary logger if the dotnet test invocation passed one.
        // We will take the same file name but append something to it, like `-dotnet-test-evaluation`
        // Tracked by https://github.com/dotnet/sdk/issues/47494
        ConcurrentBag<TestModule> projects = GetProjectsProperties(new ProjectCollection(), solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<TestModule> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
    {
        bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<TestModule>(), isBuiltOrRestored);
        }

        // TODO: We should pass a binary logger if the dotnet test invocation passed one.
        // We will take the same file name but append something to it, like `-dotnet-test-evaluation`
        // Tracked by https://github.com/dotnet/sdk/issues/47494
        IEnumerable<TestModule> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, GetGlobalProperties(buildOptions), new ProjectCollection());

        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
    {
        IEnumerable<string> binaryLoggerTokens = GetBinaryLoggerTokens(parseResult.UnmatchedTokens);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
            .Concat(binaryLoggerTokens);

        List<string> unmatchedTokens = [.. parseResult.UnmatchedTokens];
        unmatchedTokens.RemoveAll(arg => binaryLoggerTokens.Contains(arg));

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
            GetGlobalProperties([.. msbuildArgs]),
            unmatchedTokens,
            msbuildArgs);
    }

    private static string[]? GetGlobalProperties(IReadOnlyList<string> args)
        => new CliConfiguration(new CliCommand("dotnet") { CommonOptions.PropertiesOption }).Parse(args).GetValue(CommonOptions.PropertiesOption);

    private static IEnumerable<string> GetBinaryLoggerTokens(IEnumerable<string> args)
    {
        return args.Where(arg =>
            arg.StartsWith("/bl:", StringComparison.OrdinalIgnoreCase) || arg.Equals("/bl", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--binaryLogger:", StringComparison.OrdinalIgnoreCase) || arg.Equals("--binaryLogger", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("-bl:", StringComparison.OrdinalIgnoreCase) || arg.Equals("-bl", StringComparison.OrdinalIgnoreCase));
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
                IEnumerable<TestModule> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, GetGlobalProperties(buildOptions), projectCollection);
                foreach (var projectMetadata in projectsMetadata)
                {
                    allProjects.Add(projectMetadata);
                }
            });

        return allProjects;
    }

    private static Dictionary<string, string> GetGlobalProperties(BuildOptions buildOptions)
    {
        var globalProperties = new Dictionary<string, string>(buildOptions.UserSpecifiedProperties.Length);

        foreach (var property in buildOptions.UserSpecifiedProperties)
        {
            foreach (var (key, value) in MSBuildPropertyParser.ParseProperties(property))
            {
                globalProperties[key] = value;
            }
        }

        return globalProperties;
    }
}
