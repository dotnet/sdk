// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

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

        public static async Task<IEnumerable<string>> ParseSolution(string solutionFilePath)
        {
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                VSTestTrace.SafeWriteTrace(() => $"Solution file path cannot be null or empty: {solutionFilePath}");
                return [];
            }

            var projectsPaths = new List<string>();
            SolutionModel solution = null;

            try
            {
                using var stream = new FileStream(solutionFilePath, FileMode.Open, FileAccess.Read);
                string extension = Path.GetExtension(solutionFilePath);

                solution = extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ? await SolutionSerializers.SlnFileV12.OpenAsync(stream, CancellationToken.None)
                    : extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                        ? await SolutionSerializers.SlnXml.OpenAsync(stream, CancellationToken.None)
                        : null;
            }
            catch (Exception ex)
            {
                VSTestTrace.SafeWriteTrace(() => $"Failed to parse solution file '{solutionFilePath}': {ex.Message}");
                return [];
            }

            if (solution is not null)
            {
                projectsPaths = [.. solution.SolutionProjects.Select(project => project.FilePath)];
            }

            return projectsPaths;
        }

        private static string[] GetSolutionFilePaths(string directory)
        {
            string[] solutionFiles = [
                    ..Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
                    ..Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly)];

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
