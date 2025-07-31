// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MSBuildUtility
{
    private const string dotnetTestVerb = "dotnet-test";

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
    {
        SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

        (bool isBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties) = BuildOrRestoreProjectOrSolution1(solutionFilePath, buildOptions);
        //bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);dotnet t

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }


        string rootDirectory = solutionFilePath.HasExtension(".slnf") ?
                Path.GetDirectoryName(solutionModel.Description)! :
                SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

        var projectPaths = solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath));
        var projects = GetProjectsProperties1(collectedProperties, projectPaths, buildOptions.NoLaunchProfile, buildOptions);

        //FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], dotnetTestVerb);
        //var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.MSBuildArgs]), loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        //ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = GetProjectsProperties(collection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);
        //logger?.ReallyShutdown();

        return (projects, isBuiltOrRestored);

        //return ([], isBuiltOrRestored);
    }

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution1(string solutionFilePath, BuildOptions buildOptions)
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

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.BinLogArgs], dotnetTestVerb);
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.OtherMSBuildArgs]), loggers: logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

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

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.BinLogArgs], dotnetTestVerb);
        var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs([.. buildOptions.OtherMSBuildArgs]), logger is null ? null : [logger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

        IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, collection, buildOptions.NoLaunchProfile);
        logger?.ReallyShutdown();

        return (projects, isBuiltOrRestored);
    }

    public static (IEnumerable<ParallelizableTestModuleGroupWithSequentialInnerModules> Projects, bool IsBuiltOrRestored) GetProjectsFromProject1(string projectFilePath, BuildOptions buildOptions)
    {
        //Debugger.Launch();

        //// Set up the project file path and targets
        //string[] targets = new[] { "Restore", "Build" };

        //// Set up global properties if needed
        //var globalProperties = new Dictionary<string, string?>();

        //// Create a ProjectCollection (optional, for advanced scenarios)
        //var projectCollection = new ProjectCollection(globalProperties);

        //// Set up loggers (optional)
        //var loggers = new List<ILogger> { new ConsoleLogger() };

        //// Build parameters
        //var buildParameters = new BuildParameters(projectCollection)
        //{
        //    Loggers = loggers
        //};
        //var projectInstance = new ProjectInstance(projectFilePath);
        //projectInstance.SetProperty("BuildProjectReferences", "True");
        //var buildRequestData = new BuildRequestData(projectInstance, new[] { "Restore", "Build" });
        //var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);
        //return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), buildResult.OverallResult == BuildResultCode.Success);

        //var collection = new ProjectCollection();
        //var propertyLogger = new PropertyCollectingLogger();
        //var binaryLogger = new BinaryLogger
        //{
        //    Parameters = "msbuild.binlog"
        //};

        //var buildParameters = new BuildParameters(collection)
        //{
        //    Loggers = new ILogger[] { propertyLogger, binaryLogger }
        //};

        //var buildRequest = new BuildRequestData(
        //    projectFilePath,
        //    new Dictionary<string, string?>(),
        //    null,
        //    new[] { "Restore", "Build" },
        //    null
        //);

        //try
        //{
        //    var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

        //    if (buildResult.OverallResult != BuildResultCode.Success)
        //    {
        //        Console.Error.WriteLine("Solution build failed.");
        //        if (buildResult.Exception != null)
        //        {
        //            Console.Error.WriteLine(buildResult.Exception.ToString());
        //        }
        //    }

        //    // Output collected properties
        //    //foreach (var project in propertyLogger._buildContexts)
        //    //{
        //    //	Console.WriteLine($"Project: {project.Key}");
        //    //	foreach (var prop in project.Value)
        //    //	{
        //    //		Console.WriteLine($"  {prop.Key}: {prop.Value}");
        //    //	}
        //    //}
        //}
        //finally
        //{
        //    collection.UnregisterAllLoggers();
        //    collection.Dispose();
        //}
        //return ([], true);
        // Build or restore the project and collect properties
        (bool isBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties) =
            BuildOrRestoreProjectOrSolution1(projectFilePath, buildOptions);

        if (!isBuiltOrRestored)
        {
            return (Array.Empty<ParallelizableTestModuleGroupWithSequentialInnerModules>(), isBuiltOrRestored);
        }

        // Use the collected properties to get project properties (single project)
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

        PathOptions pathOptions = new(parseResult.GetValue(
            TestingPlatformOptions.ProjectOption),
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

    private static bool BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
    {
        if (buildOptions.HasNoBuild)
        {
            return true;
        }
        List<string> msbuildArgs = [.. buildOptions.OtherMSBuildArgs, filePath];

        if (buildOptions.Verbosity is null)
        {
            msbuildArgs.Add($"-verbosity:quiet");
        }

        var parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(msbuildArgs, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, TestCommandParser.MTPTargetOption);

        int result = new RestoringCommand(parsedMSBuildArgs, buildOptions.HasNoRestore).Execute();

        return result == (int)BuildResultCode.Success;
    }


    private static (bool IsBuiltOrRestored, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> CollectedProperties)
     BuildOrRestoreProjectOrSolution1(string filePath, BuildOptions buildOptions)
    {
        var msbuildArgs = new List<string>(buildOptions.OtherMSBuildArgs) { filePath };
        MSBuildArgs parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(
            msbuildArgs,
            CommonOptions.PropertiesOption,
            CommonOptions.RestorePropertiesOption,
            TestCommandParser.MTPTargetOption
        );

        var globalProperties = CreateGlobalProperties(buildOptions, parsedMSBuildArgs);
        using var collection = new ProjectCollection(globalProperties)
        {
            PropertiesFromCommandLine = [.. parsedMSBuildArgs.OtherMSBuildArgs]
        };

        var propertyLogger = new PropertyCollectingLogger();
        var loggers = CreateLoggers(buildOptions, propertyLogger, out FacadeLogger? binaryLogger, out ConsoleLogger? consoleLogger);

        var buildParameters = new BuildParameters(collection) { Loggers = loggers };
        var targets = GetBuildTargets(buildOptions, parsedMSBuildArgs);
        var buildRequest = new BuildRequestData(
            filePath,
            collection.GlobalProperties,
            null,
            [.. targets],
            null
        );

        try
        {
            var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

            //PrintCollectedProperties(propertyLogger);

            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                Console.Error.WriteLine("Build failed.");
                if (buildResult.Exception != null)
                {
                    Console.Error.WriteLine(buildResult.Exception.ToString());
                }
                return (false, propertyLogger.CollectedProperties);
            }

            return (true, propertyLogger.CollectedProperties);
        }
        finally
        {
            binaryLogger?.ReallyShutdown();
            consoleLogger?.Shutdown();
            propertyLogger?.Shutdown();
            collection.UnregisterAllLoggers();
        }
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

        binaryLogger = LoggerUtility.DetermineBinlogger([.. buildOptions.BinLogArgs], "");
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

        if (!buildOptions.HasNoRestore)
        {
            targets.Add("Restore");
        }

        if (parsedMSBuildArgs.RequestedTargets?.Length > 0)
        {
            targets.AddRange(parsedMSBuildArgs.RequestedTargets);
        }

        //targets.Add("ComputeRunArguments");
        return targets;
    }

    private static void PrintCollectedProperties(PropertyCollectingLogger propertyLogger)
    {
        foreach (var prop in propertyLogger.CollectedProperties)
        {
            Console.WriteLine($"Project: {prop.Key}");
            int i = 0;
            foreach (var properties in prop.Value)
            {
                i++;
                var tfm = properties.GetValueOrDefault(ProjectProperties.TargetFramework);
                Console.WriteLine($"  Context {i}: TargetFramework={tfm}");
                foreach (var kvp in properties)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }
        }
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

    private static ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules> GetProjectsProperties1(
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> collectedProperties,
    IEnumerable<string> projects,
    bool noLaunchProfile,
    BuildOptions buildOptions)
    {
        var allProjects = new ConcurrentBag<ParallelizableTestModuleGroupWithSequentialInnerModules>();

        Parallel.ForEach(
            projects,
            new ParallelOptions { MaxDegreeOfParallelism = buildOptions.DegreeOfParallelism },
            (project) =>
            {
                var modules = SolutionAndProjectUtility.GetProjectProperties1(project, noLaunchProfile, collectedProperties);
                foreach (var module in modules)
                {
                    allProjects.Add(module);
                }
            });

        return allProjects;
    }
}
