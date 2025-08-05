// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
    {
        SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

        (bool isBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties) = BuildProjectOrSolution(solutionFilePath, buildOptions, useSeparateRestoreCall: false);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        var projects = GetProjectsProperties(collectedProperties, buildOptions.NoLaunchProfile, buildOptions.DegreeOfParallelism);

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
    {
        (bool isBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties) =
            BuildProjectOrSolution(projectFilePath, buildOptions, useSeparateRestoreCall: true);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        var projects = SolutionAndProjectUtility.GetProjectProperties1(
            projectFilePath,
            buildOptions.NoLaunchProfile,
            collectedProperties);

        return (projects, isBuiltOrRestored);
    }

    public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
    {
        LoggerUtility.SeparateBinLogArguments(parseResult.UnmatchedTokens, out var binLogArgs, out var otherArgs);

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand());

        PathOptions pathOptions = new(
            parseResult.GetValue(TestingPlatformOptions.ProjectOption),
            parseResult.GetValue(TestingPlatformOptions.SolutionOption),
            parseResult.GetValue(TestingPlatformOptions.DirectoryOption));

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(CommonOptions.NoRestoreOption),
            parseResult.GetValue(TestingPlatformOptions.NoBuildOption),
            parseResult.HasOption(TestCommandParser.VerbosityOption) ? parseResult.GetValue(TestCommandParser.VerbosityOption) : null,
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileOption),
            parseResult.GetValue(TestingPlatformOptions.NoLaunchProfileArgumentsOption),
            degreeOfParallelism,
            otherArgs,
            binLogArgs,
            msbuildArgs);
    }

    private static (bool IsBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties)
        BuildProjectOrSolution(string filePath, BuildOptions buildOptions, bool useSeparateRestoreCall)
    {
        var global = CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.OtherMSBuildArgs]);

        var parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(
            [.. buildOptions.OtherMSBuildArgs, filePath],
            CommonOptions.PropertiesOption,
            CommonOptions.RestorePropertiesOption,
            TestCommandParser.MTPTargetOption,
            TestCommandParser.VerbosityOption);

        var globalProperties = CreateGlobalProperties(buildOptions, parsedMSBuildArgs);
        using var collection = new ProjectCollection(globalProperties)
        {
            PropertiesFromCommandLine = [.. parsedMSBuildArgs.OtherMSBuildArgs]
        };

        var propertyLogger = new PropertyCollectingLogger();
        var loggers = CreateLoggers(buildOptions, propertyLogger, out FacadeLogger? binaryLogger, out ConsoleLogger? consoleLogger);
        var buildParameters = new BuildParameters(collection) { Loggers = loggers };

        try
        {
            return ExecuteSeparateBuildCalls(filePath, buildOptions, buildParameters, parsedMSBuildArgs, collection.GlobalProperties, propertyLogger);
        }
        finally
        {
            binaryLogger?.ReallyShutdown();
            consoleLogger?.Shutdown();
            propertyLogger?.Shutdown();
            collection.UnregisterAllLoggers();
        }
    }

    private static (bool IsBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties)
        ExecuteSeparateBuildCalls(string filePath, BuildOptions buildOptions, BuildParameters buildParameters, MSBuildArgs parsedMSBuildArgs,
            IDictionary<string, string?> globalProperties, PropertyCollectingLogger propertyLogger)
    {
        // First call: Restore (if not skipped)
        if (!buildOptions.HasNoRestore && !buildOptions.HasNoBuild)
        {
            var restoreRequest = new BuildRequestData(filePath, new Dictionary<string, string?>(), null, ["Restore"], null);
            var restoreResult = BuildManager.DefaultBuildManager.Build(buildParameters, restoreRequest);

            if (restoreResult.OverallResult != BuildResultCode.Success)
            {
                LogBuildFailure(restoreResult, "Restore failed");
                return (false, propertyLogger.CollectedProperties);
            }
        }

        // Second call: Other targets
        var otherTargets = GetBuildTargetsExcludingRestore(parsedMSBuildArgs);
        if (otherTargets.Count > 0)
        {
            var buildRequest = new BuildRequestData(filePath, globalProperties, null, [.. otherTargets], null);
            var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                LogBuildFailure(buildResult, "Build failed");
                return (false, propertyLogger.CollectedProperties);
            }
        }

        return (true, propertyLogger.CollectedProperties);
    }

    private static void LogBuildFailure(BuildResult buildResult, string message)
    {
        if (buildResult.Exception != null)
        {
            Logger.LogTrace(() => $"{message}. Exception: {buildResult.Exception}");
        }
        else
        {
            Logger.LogTrace(() => message);
        }
    }

    private static List<string> GetBuildTargetsExcludingRestore(MSBuildArgs parsedMSBuildArgs)
    {
        var targets = new List<string>();

        if (parsedMSBuildArgs.RequestedTargets?.Length > 0)
        {
            targets.AddRange(parsedMSBuildArgs.RequestedTargets);
        }

        return targets;
    }

    private static Dictionary<string, string> CreateGlobalProperties(BuildOptions buildOptions, MSBuildArgs parsedMSBuildArgs)
    {
        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ShouldRunBuild", (!buildOptions.HasNoBuild).ToString().ToLower() },
        };

        if (parsedMSBuildArgs.GlobalProperties is not null)
        {
            globalProperties.AddRange(parsedMSBuildArgs.GlobalProperties);
        }

        return globalProperties;
    }

    private static List<ILogger> CreateLoggers(
        BuildOptions buildOptions,
        PropertyCollectingLogger propertyLogger,
        out FacadeLogger? binaryLogger,
        out ConsoleLogger? consoleLogger)
    {
        var loggers = new List<ILogger> { propertyLogger };

        binaryLogger = LoggerUtility.DetermineBinlogger([.. buildOptions.BinLogArgs], CliConstants.DotnetTestVerb);
        if (binaryLogger is not null)
        {
            loggers.Add(binaryLogger);
        }

        LoggerVerbosity msbuildVerbosity = LoggerVerbosity.Quiet;
        if (buildOptions.Verbosity is not null)
        {
            msbuildVerbosity = buildOptions.Verbosity switch
            {
                VerbosityOptions.quiet or VerbosityOptions.q => LoggerVerbosity.Quiet,
                VerbosityOptions.minimal or VerbosityOptions.m => LoggerVerbosity.Minimal,
                VerbosityOptions.normal or VerbosityOptions.n => LoggerVerbosity.Normal,
                VerbosityOptions.detailed or VerbosityOptions.d => LoggerVerbosity.Detailed,
                VerbosityOptions.diagnostic or VerbosityOptions.diag => LoggerVerbosity.Diagnostic,
                _ => LoggerVerbosity.Normal
            };
        }

        consoleLogger = new ConsoleLogger(msbuildVerbosity);
        loggers.Add(consoleLogger);

        return loggers;
    }

    private static List<string> GetBuildTargets(BuildOptions buildOptions, MSBuildArgs parsedMSBuildArgs)
    {
        var targets = new List<string>();

        if (!buildOptions.HasNoRestore && !buildOptions.HasNoBuild)
        {
            targets.Add("Restore");
        }

        if (parsedMSBuildArgs.RequestedTargets?.Length > 0)
        {
            targets.AddRange(parsedMSBuildArgs.RequestedTargets);
        }

        return targets;
    }

    private static ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectsProperties(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties,
        bool noLaunchProfile,
        int degreeOfParallelism)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        Parallel.ForEach(
            collectedProperties.Keys,
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            (projectPath) =>
            {
                var modules = SolutionAndProjectUtility.GetProjectProperties1(projectPath, noLaunchProfile, collectedProperties);
                foreach (var module in modules)
                {
                    allProjects.Add(module);
                }
            });

        return allProjects;
    }
}
