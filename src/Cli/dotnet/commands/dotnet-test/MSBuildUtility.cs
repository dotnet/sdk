// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
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
            SolutionModel solutionModel = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath, includeSolutionFilterFiles: true, includeSolutionXmlFiles: true);

            bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(solutionFilePath, buildOptions);

            string rootDirectory = solutionFilePath.HasExtension(".slnf") ?
                    Path.GetDirectoryName(solutionModel.Description) :
                    SolutionAndProjectUtility.GetRootDirectory(solutionFilePath);

            ConcurrentBag<Module> projects = GetProjectsProperties(new ProjectCollection(), solutionModel.SolutionProjects.Select(p => Path.Combine(rootDirectory, p.FilePath)), buildOptions);

            isBuiltOrRestored |= !projects.IsEmpty;

            return (projects, isBuiltOrRestored);
        }

        public static (IEnumerable<Module> Projects, bool IsBuiltOrRestored) GetProjectsFromProject(string projectFilePath, BuildOptions buildOptions)
        {
            bool isBuiltOrRestored = BuildOrRestoreProjectOrSolution(projectFilePath, buildOptions);

            IEnumerable<Module> projects = SolutionAndProjectUtility.GetProjectProperties(projectFilePath, GetGlobalProperties(buildOptions.BuildProperties), new ProjectCollection());

            isBuiltOrRestored |= projects.Any();

            return (projects, isBuiltOrRestored);
        }

        public static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
        {
            IEnumerable<string> propertyTokens = GetPropertyTokens(parseResult.UnmatchedTokens);
            IEnumerable<string> binaryLoggerTokens = GetBinaryLoggerTokens(parseResult.UnmatchedTokens);

            var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
                .Concat(propertyTokens)
                .Concat(binaryLoggerTokens)
                .ToList();

            List<string> unmatchedTokens = [.. parseResult.UnmatchedTokens];
            unmatchedTokens.RemoveAll(arg => propertyTokens.Contains(arg));
            unmatchedTokens.RemoveAll(arg => binaryLoggerTokens.Contains(arg));

            PathOptions pathOptions = new(parseResult.GetValue(
                TestingPlatformOptions.ProjectOption),
                parseResult.GetValue(TestingPlatformOptions.SolutionOption),
                parseResult.GetValue(TestingPlatformOptions.DirectoryOption));

            BuildProperties buildProperties = new(
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                ResolveRuntimeIdentifier(parseResult),
                parseResult.GetValue(TestingPlatformOptions.FrameworkOption));

            return new BuildOptions(
                pathOptions,
                buildProperties,
                parseResult.GetValue(CommonOptions.NoRestoreOption),
                parseResult.GetValue(TestingPlatformOptions.NoBuildOption),
                degreeOfParallelism,
                unmatchedTokens,
                msbuildArgs);
        }

        private static string ResolveRuntimeIdentifier(ParseResult parseResult)
        {
            if (parseResult.HasOption(CommonOptions.RuntimeOption))
            {
                return parseResult.GetValue(CommonOptions.RuntimeOption);
            }

            if (!parseResult.HasOption(CommonOptions.OperatingSystemOption) && !parseResult.HasOption(CommonOptions.ArchitectureOption))
            {
                return string.Empty;
            }

            return CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(parseResult.GetValue(CommonOptions.OperatingSystemOption), parseResult.GetValue(CommonOptions.ArchitectureOption));
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

        private static bool BuildOrRestoreProjectOrSolution(string filePath, BuildOptions buildOptions)
        {
            List<string> msbuildArgs = buildOptions.MSBuildArgs;

            msbuildArgs.Add(filePath);
            msbuildArgs.Add($"-target:{CliConstants.MTPTarget}");

            int result = new RestoringCommand(msbuildArgs, buildOptions.HasNoRestore || buildOptions.HasNoBuild).Execute();

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
                    IEnumerable<Module> projectsMetadata = SolutionAndProjectUtility.GetProjectProperties(project, GetGlobalProperties(buildOptions.BuildProperties), projectCollection);
                    foreach (var projectMetadata in projectsMetadata)
                    {
                        allProjects.Add(projectMetadata);
                    }
                });

            return allProjects;
        }

        private static Dictionary<string, string> GetGlobalProperties(BuildProperties buildProperties)
        {
            var globalProperties = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(buildProperties.Configuration))
            {
                globalProperties[CliConstants.Configuration] = buildProperties.Configuration;
            }

            if (!string.IsNullOrEmpty(buildProperties.RuntimeIdentifier))
            {
                globalProperties[CliConstants.RuntimeIdentifier] = buildProperties.RuntimeIdentifier;
            }

            if (!string.IsNullOrEmpty(buildProperties.TargetFramework))
            {
                globalProperties[CliConstants.TargetFramework] = buildProperties.TargetFramework;
            }

            return globalProperties;
        }
    }
}
