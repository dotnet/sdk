// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Tools;
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
             solutionFilePath, buildOptions.MSBuildArgs, buildOptions.HasNoRestore, buildOptions.HasNoBuild);

            ConcurrentBag<Module> projects = GetProjectsProperties(projectCollection, solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);
            return (projects, isBuiltOrRestored);
        }

        public static (IEnumerable<Module> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
        {
            var projectCollection = new ProjectCollection();

            bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions.MSBuildArgs, buildOptions.HasNoRestore, buildOptions.HasNoBuild);

            IEnumerable<Module> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, GetGlobalProperties(buildOptions), projectCollection);

            return (projects, isBuiltOrRestored);
        }

        public static IEnumerable<string> GetPropertyTokens(IEnumerable<string> unmatchedTokens)
        {
            return unmatchedTokens.Where(token =>
                token.StartsWith("--property:", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("/property:", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("-p:", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("/p:", StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<string> GetBinaryLoggerTokens(IEnumerable<string> args)
        {
            return args.Where(arg =>
                arg.StartsWith("/bl:", StringComparison.OrdinalIgnoreCase) || arg.Equals("/bl", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("--binaryLogger:", StringComparison.OrdinalIgnoreCase) || arg.Equals("--binaryLogger", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-bl:", StringComparison.OrdinalIgnoreCase) || arg.Equals("-bl", StringComparison.OrdinalIgnoreCase));
        }

        private static bool BuildOrRestoreProjectOrSolution(string filePath, List<string> arguments, bool hasNoRestore, bool hasNoBuild)
        {
            arguments.Add(filePath);
            arguments.Add("-target:_MTPBuild");

            int result = new RestoringCommand(arguments, hasNoRestore || hasNoBuild).Execute();

            return result == (int)BuildResultCode.Success;
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
    }
}
