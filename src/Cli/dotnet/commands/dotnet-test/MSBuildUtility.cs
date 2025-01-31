// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Microsoft.DotNet.Cli
{
    internal static class MSBuildUtility
    {
        public static (IEnumerable<Module> Projects, bool IsBuiltOrRestored) GetProjectsFromSolution(string solutionFilePath, BuildOptions buildOptions)
        {
            var projectCollection = new ProjectCollection();
            string rootDirectory = SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

            SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

            if (solutionFilePath.HasExtension(".slnf"))
            {
                solutionFilePath = HandleFilteredSolutionFilePath(solutionFilePath, out rootDirectory);
            }

            bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(
                solutionFilePath,
                projectCollection,
                buildOptions,
                GetCommands(buildOptions.HasNoRestore, buildOptions.HasNoBuild));

            ConcurrentBag<Module> projects = GetProjectsProperties(projectCollection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);
            return (projects, isBuiltOrRestored);
        }

        public static (IEnumerable<Module> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
        {
            var projectCollection = new ProjectCollection();
            bool isBuiltOrRestored = true;

            if (!buildOptions.HasNoRestore)
            {
                isBuiltOrRestored = BuildOrRestoreProjectOrSolution(
                    projectFilePath,
                    projectCollection,
                    buildOptions,
                    [CliConstants.RestoreCommand]);
            }

            if (!buildOptions.HasNoBuild)
            {
                isBuiltOrRestored = isBuiltOrRestored && BuildOrRestoreProjectOrSolution(
                    projectFilePath,
                    projectCollection,
                    buildOptions,
                    [CliConstants.BuildCommand]);
            }

            IEnumerable<Module> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, GetGlobalProperties(buildOptions), projectCollection);

            return (projects, isBuiltOrRestored);
        }

        private static bool BuildOrRestoreProjectOrSolution(string filePath, ProjectCollection projectCollection, BuildOptions buildOptions, string[] commands)
        {
            var parameters = GetBuildParameters(projectCollection, buildOptions);
            var globalProperties = GetGlobalProperties(buildOptions);

            var buildRequestData = new BuildRequestData(filePath, globalProperties, null, commands, null);

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(parameters, buildRequestData);

            return buildResult.OverallResult == BuildResultCode.Success;
        }

        private static ConcurrentBag<Module> GetProjectsProperties(ProjectCollection projectCollection, IEnumerable<string> projects, BuildOptions buildOptions)
        {
            var allProjects = new ConcurrentBag<Module>();

            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = buildOptions.DegreeOfParallelism },
                (project) =>
                {
                    IEnumerable<Module> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, GetGlobalProperties(buildOptions), projectCollection);
                    foreach (var projectMetadata in projectsMetadata)
                    {
                        allProjects.Add(projectMetadata);
                    }
                });

            return allProjects;
        }

        private static string HandleFilteredSolutionFilePath(string solutionFilterFilePath, out string rootDirectory)
        {
            string solution = SlnFileFactory.GetSolutionPathFromFilteredSolutionFile(solutionFilterFilePath);

            // Resolve the solution path relative to the .slnf file directory
            string solutionFilterDirectory = Path.GetDirectoryName(solutionFilterFilePath);
            string solutionFullPath = Path.GetFullPath(solution, solutionFilterDirectory);
            rootDirectory = Path.GetDirectoryName(solutionFullPath);

            return solutionFullPath;
        }

        internal static bool IsBinaryLoggerEnabled(List<string> args, out string binLogFileName)
        {
            binLogFileName = string.Empty;
            var binLogArgs = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("/bl:") || arg.Equals("/bl")
                    || arg.StartsWith("--binaryLogger:") || arg.Equals("--binaryLogger")
                    || arg.StartsWith("-bl:") || arg.Equals("-bl"))
                {
                    binLogArgs.Add(arg);

                }
            }

            if (binLogArgs.Count > 0)
            {
                // Remove all BinLog args from the list of args
                args.RemoveAll(arg => binLogArgs.Contains(arg));

                // Get BinLog filename
                var binLogArg = binLogArgs.LastOrDefault();

                if (binLogArg.Contains(CliConstants.Colon))
                {
                    var parts = binLogArg.Split(CliConstants.Colon, 2);
                    binLogFileName = !string.IsNullOrEmpty(parts[1]) ? parts[1] : CliConstants.BinLogFileName;
                }
                else
                {
                    binLogFileName = CliConstants.BinLogFileName;
                }

                return true;
            }

            return false;
        }

        private static BuildParameters GetBuildParameters(ProjectCollection projectCollection, BuildOptions buildOptions)
        {
            BuildParameters parameters = new(projectCollection)
            {
                Loggers = [new ConsoleLogger(LoggerVerbosity.Quiet)]
            };

            if (!buildOptions.AllowBinLog)
                return parameters;

            parameters.Loggers =
            [
                .. parameters.Loggers,
                .. new[]
                {
                    new BinaryLogger
                    {
                        Parameters = buildOptions.BinLogFileName
                    }
                },
            ];

            return parameters;
        }

        private static Dictionary<string, string> GetGlobalProperties(BuildOptions buildOptions)
        {
            var globalProperties = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(buildOptions.Configuration))
            {
                globalProperties[CliConstants.Configuration] = buildOptions.Configuration;
            }

            if (!string.IsNullOrEmpty(buildOptions.RuntimeIdentifier))
            {
                globalProperties[CliConstants.RuntimeIdentifier] = buildOptions.RuntimeIdentifier;
            }

            return globalProperties;
        }

        private static string[] GetCommands(bool hasNoRestore, bool hasNoBuild)
        {
            var commands = ImmutableArray.CreateBuilder<string>();

            if (!hasNoRestore)
            {
                commands.Add(CliConstants.RestoreCommand);
            }

            if (!hasNoBuild)
            {
                commands.Add(CliConstants.BuildCommand);
            }

            return [.. commands];
        }
    }
}
