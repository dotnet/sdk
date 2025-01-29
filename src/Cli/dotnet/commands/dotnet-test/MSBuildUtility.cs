// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
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
        public static (IEnumerable<Module>, bool) HandleSolution(string solutionFilePath, BuildOptions buildOptions)
        {
            var projectCollection = new ProjectCollection();
            string rootDirectory = SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

            SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

            // TO DO: for the case of .slnf file, we need to pass the solution file (.sln) path in order to build/restore the whole solution
            bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(
                solutionFilePath,
                projectCollection,
                buildOptions,
                GetCommands(buildOptions.HasNoRestore, buildOptions.HasNoBuild));

            ConcurrentBag<Module> allProjects = ProcessProjectsInParallel(projectCollection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions.DegreeOfParallelism);
            return (allProjects, isBuiltOrRestored);
        }

        public static (IEnumerable<Module>, bool) HandleProject(string projectFilePath, BuildOptions buildOptions)
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

            var allProjects = GetRelatedProjects(projectFilePath, projectCollection);

            return (allProjects, isBuiltOrRestored);
        }

        private static bool BuildOrRestoreProjectOrSolution(string filePath, ProjectCollection projectCollection, BuildOptions buildOptions, string[] commands)
        {
            var parameters = GetBuildParameters(projectCollection, buildOptions);
            var globalProperties = GetGlobalProperties(buildOptions);

            var buildRequestData = new BuildRequestData(filePath, globalProperties, null, commands, null);

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(parameters, buildRequestData);

            return buildResult.OverallResult == BuildResultCode.Success;
        }

        private static List<Module> GetRelatedProjects(string solutionOrProjectFilePath, ProjectCollection projectCollection)
        {
            var allProjects = new List<Module>();
            IEnumerable<Module> relatedProjects = SolutionAndProjectUtility.GetProjectPropertiesInternal(solutionOrProjectFilePath, projectCollection);
            allProjects.AddRange(relatedProjects);

            return allProjects;
        }

        private static ConcurrentBag<Module> ProcessProjectsInParallel(ProjectCollection projectCollection, IEnumerable<string> projects, int degreeOfParallelism)
        {
            var allProjects = new ConcurrentBag<Module>();

            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
                (project, state) =>
                {
                    IEnumerable<Module> relatedProjects = SolutionAndProjectUtility.GetProjectPropertiesInternal(project, projectCollection);
                    foreach (var relatedProject in relatedProjects)
                    {
                        allProjects.Add(relatedProject);
                    }
                });

            return allProjects;
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
            var commands = new List<string>();

            if (!hasNoRestore)
            {
                commands.Add(CliConstants.RestoreCommand);
            }

            if (!hasNoBuild)
            {
                commands.Add(CliConstants.BuildCommand);
            }

            return commands.ToArray();
        }
    }
}
