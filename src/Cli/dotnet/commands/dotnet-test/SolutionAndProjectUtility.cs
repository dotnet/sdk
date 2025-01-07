// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal static class SolutionAndProjectUtility
    {
        public static bool TryGetSolutionOrProjectFilePath(string directory, out string solutionOrProjectFilePath, out bool isSolution)
        {
            solutionOrProjectFilePath = string.Empty;
            isSolution = false;

            // Get all solution files in the specified directory
            var solutionFiles = GetSolutionFilePaths(directory);
            // Get all project files in the specified directory
            var projectFiles = GetProjectFilePaths(directory);

            // If both solution files and project files are found, return false
            if (solutionFiles.Length > 0 && projectFiles.Length > 0)
            {
                VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorMessage);
                return false;
            }

            // If exactly one solution file is found, return the solution file path
            if (solutionFiles.Length == 1)
            {
                solutionOrProjectFilePath = solutionFiles[0];
                isSolution = true;
                return true;
            }

            // If exactly one project file is found, return the project file path
            if (projectFiles.Length == 1)
            {
                solutionOrProjectFilePath = projectFiles[0];
                return true;
            }

            // If no solution or project files are found, return false
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
            var solutionFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly))
                .ToArray();

            return solutionFiles;
        }

        private static string[] GetProjectFilePaths(string directory)
        {
            var projectFiles = Directory.GetFiles(directory, "*.*proj", SearchOption.TopDirectoryOnly)
                .Where(f => IsProjectFile(f))
                .ToArray();

            return projectFiles;
        }

        private static bool IsProjectFile(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".proj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
