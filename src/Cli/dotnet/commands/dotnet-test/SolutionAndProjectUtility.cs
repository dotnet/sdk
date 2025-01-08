// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Cli
{
    internal static class SolutionAndProjectUtility
    {
        public static bool TryGetProjectOrSolutionFilePath(string directory, out string projectOrSolutionFilePath, out bool isSolution)
        {
            projectOrSolutionFilePath = string.Empty;
            isSolution = false;

            if (Directory.Exists(directory))
            {
                string[] possibleSolutionPaths = [
                    ..Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
                    ..Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly)];

                // If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
                if (possibleSolutionPaths.Count() > 1)
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
                    return false;
                }
                // If a single solution is found, use it.
                else if (possibleSolutionPaths.Length == 1)
                {
                    // Get project file paths to check if there are any projects in the directory
                    string[] possibleProjectPaths = GetProjectFilePaths(directory);

                    if (possibleProjectPaths.Length == 0)
                    {
                        projectOrSolutionFilePath = possibleSolutionPaths[0];
                        isSolution = true;
                        return true;
                    }
                    else // If both solution and project files are found, return false
                    {
                        VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorMessage);
                        return false;
                    }
                }
                // If no solutions are found, look for a project file
                else
                {
                    string[] possibleProjectPath = GetProjectFilePaths(directory);

                    // No projects found throws an error that no sln nor projects were found
                    if (possibleProjectPath.Length == 0)
                    {
                        VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdNoProjectOrSolutionFileErrorMessage);
                        return false;
                    }
                    // A single project found, use it
                    else if (possibleProjectPath.Length == 1)
                    {
                        projectOrSolutionFilePath = possibleProjectPath[0];
                        return true;
                    }
                    // More than one project found. Not sure which one to choose
                    else
                    {
                        VSTestTrace.SafeWriteTrace(() => string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, directory));
                        return false;
                    }
                }
            }

            return false;
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
                solution = SolutionSerializers.GetSerializerByMoniker(solutionFilePath) is ISolutionSerializer serializer
                    ? await serializer.OpenAsync(solutionFilePath, CancellationToken.None)
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
