// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal static class SolutionAndProjectUtility
    {
        public static bool TryGetSolutionOrProjectFilePath(string directory, out string solutionOrProjectFilePath, out bool isSolution)
        {
            var solutionFiles = GetSolutionFilePaths(directory);
            var projectFiles = GetProjectFilePaths(directory);

            solutionOrProjectFilePath = string.Empty;
            isSolution = false;

            if (solutionFiles.Length > 0 && projectFiles.Length > 0)
            {
                VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorMessage);
                return false;
            }

            if (solutionFiles.Length == 1)
            {
                solutionOrProjectFilePath = solutionFiles[0];
                isSolution = true;

                return true;
            }

            if (projectFiles.Length == 1)
            {
                solutionOrProjectFilePath = projectFiles[0];

                return true;
            }

            VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdNoProjectOrSolutionFileErrorMessage);
            return false;
        }

        public static IEnumerable<string> GetProjectsFromSolutionFile(string solutionFilePath)
        {
            var solutionFile = SolutionFile.Parse(solutionFilePath);
            return solutionFile.ProjectsInOrder.Select(project => project.AbsolutePath);
        }

        private static string[] GetSolutionFilePaths(string directory)
        {
            var solutionFiles = Directory.GetFiles(directory, "*.sln*", SearchOption.TopDirectoryOnly)
                                                    .Where(f => Regex.IsMatch(f, @"\.(sln|slnx)$")).ToArray();

            return solutionFiles;
        }

        private static string[] GetProjectFilePaths(string directory)
        {
            var projectFiles = Directory.GetFiles(directory, "*.*proj", SearchOption.TopDirectoryOnly)
                                        .Where(f => Regex.IsMatch(f, @"\.(csproj|vbproj|fsproj)$")).ToArray();

            return projectFiles;
        }
    }
}
