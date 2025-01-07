// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.ProjectExtensions;
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

            // Get solution file in the specified directory
            var solutionFile = SlnFileExtensions.GetSlnFileFullPath(directory);
            // Get project file in the specified directory
            var projectFile = ProjectExtensions.GetProjectFileFullPath(directory);

            if (string.IsNullOrEmpty(solutionFile) && string.IsNullOrEmpty(projectFile))
            {
                // If no solution or project files are found, return false
                VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdNoProjectOrSolutionFileErrorMessage);
                return false;
            }

            // If the solution file is found, return the solution file path
            if (!string.IsNullOrEmpty(solutionFile))
            {
                solutionOrProjectFilePath = solutionFile;
                isSolution = true;
                return true;
            }

            // If the project file is found, return the project file path
            if (projectFile.Length == 1)
            {
                solutionOrProjectFilePath = projectFile;
                return true;
            }

            // If both solution file and project file are found, return false
            VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorMessage);
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
                projectsPaths = [.. solution.SolutionProjects.Select(project => Path.GetFullPath(project.FilePath))];
            }

            return projectsPaths;
        }
    }
}
