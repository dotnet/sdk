// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Tools.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    /// <summary>
    /// Enumerates the code files that would be part of an MSBuild workspace without loading
    /// MSBuild itself. Used to provide a fast, MSBuild-free whitespace formatting path.
    /// </summary>
    internal static class WorkspaceFileEnumerator
    {
        private static readonly Regex s_solutionProjectPattern = new(
            @"Project\s*\([^)]*\)\s*=\s*""(?<name>[^""]*)""\s*,\s*""(?<path>[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Gets the absolute paths of the code files that an MSBuild workspace for
        /// <paramref name="workspaceFilePath"/> would contain.
        /// </summary>
        public static ImmutableArray<string> GetFormattableFiles(string workspaceFilePath, WorkspaceType workspaceType, SourceFileMatcher fileMatcher)
        {
            var formattableFiles = ImmutableArray.CreateBuilder<string>();

            if (workspaceType == WorkspaceType.Solution)
            {
                foreach (var projectFile in GetProjectFilesFromSolution(workspaceFilePath))
                {
                    formattableFiles.AddRange(GetFilesForProject(projectFile, fileMatcher));
                }
            }
            else
            {
                formattableFiles.AddRange(GetFilesForProject(workspaceFilePath, fileMatcher));
            }

            return formattableFiles.ToImmutable();
        }

        private static IEnumerable<string> GetProjectFilesFromSolution(string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

            foreach (Match match in s_solutionProjectPattern.Matches(File.ReadAllText(solutionPath)))
            {
                var projectPath = Path.GetFullPath(match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar), solutionDirectory);
                if (File.Exists(projectPath))
                {
                    projectPaths.Add(projectPath);
                }
            }

            return projectPaths;
        }

        private static IEnumerable<string> GetFilesForProject(string projectPathOrDirectory, SourceFileMatcher fileMatcher)
        {
            var projectDirectory = Directory.Exists(projectPathOrDirectory)
                ? projectPathOrDirectory
                : Path.GetDirectoryName(projectPathOrDirectory);

            if (string.IsNullOrEmpty(projectDirectory))
            {
                return Array.Empty<string>();
            }

            return fileMatcher.GetFormattableResultsInFullPath(projectDirectory)
                .Where(fileMatcher.HasMatches);
        }
    }
}