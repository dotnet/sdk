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

            if (!Directory.Exists(directory))
            {
                return false;
            }

            var possibleSolutionPaths = GetSolutionFilePaths(directory);

            // If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
            if (possibleSolutionPaths.Length > 1)
            {
                VSTestTrace.SafeWriteTrace(() => string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
                return false;
            }

            if (possibleSolutionPaths.Length == 1)
            {
                var possibleProjectPaths = GetProjectFilePaths(directory);

                if (possibleProjectPaths.Length == 0)
                {
                    projectOrSolutionFilePath = possibleSolutionPaths[0];
                    isSolution = true;
                    return true;
                }

                VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorMessage);
                return false;
            }

            // If no solutions are found, look for a project file
            else
            {
                string[] possibleProjectPath = GetProjectFilePaths(directory);

                if (possibleProjectPath.Length == 0)
                {
                    VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdNoProjectOrSolutionFileErrorMessage);
                    return false;
                }

                if (possibleProjectPath.Length == 1)
                {
                    projectOrSolutionFilePath = possibleProjectPath[0];
                    return true;
                }

                VSTestTrace.SafeWriteTrace(() => string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, directory));

                return false;
            }
        }

        private static string[] GetSolutionFilePaths(string directory)
        {
            return Directory.GetFiles(directory, CliConstants.SolutionExtensionPattern, SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(directory, CliConstants.SolutionXExtensionPattern, SearchOption.TopDirectoryOnly))
                .ToArray();
        }

        private static string[] GetProjectFilePaths(string directory)
        {
            return [.. Directory.EnumerateFiles(directory, CliConstants.ProjectExtensionPattern, SearchOption.TopDirectoryOnly).Where(IsProjectFile)];
        }

        private static bool IsProjectFile(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return CliConstants.ProjectExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<IEnumerable<string>> ParseSolution(string solutionFilePath, string directory)
        {
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                VSTestTrace.SafeWriteTrace(() => $"Solution file path cannot be null or empty: {solutionFilePath}");
                return Array.Empty<string>();
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
                return Array.Empty<string>();
            }

            if (solution is not null)
            {
                projectsPaths.AddRange(solution.SolutionProjects.Select(project => Path.Combine(directory, project.FilePath)));
            }

            return projectsPaths;
        }
    }
}
