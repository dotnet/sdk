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
        public static (bool SolutionOrProjectFileFound, string Message) TryGetProjectOrSolutionFilePath(string directory, out string projectOrSolutionFilePath, out bool isSolution)
        {
            projectOrSolutionFilePath = string.Empty;
            isSolution = false;

            if (!Directory.Exists(directory))
            {
                return (false, string.Format(LocalizableStrings.CmdNonExistentDirectoryErrorDescription, directory));
            }

            var possibleSolutionPaths = GetSolutionFilePaths(directory);

            // If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
            if (possibleSolutionPaths.Length > 1)
            {
                return (false, string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
            }

            if (possibleSolutionPaths.Length == 1)
            {
                var possibleProjectPaths = GetProjectFilePaths(directory);

                if (possibleProjectPaths.Length == 0)
                {
                    projectOrSolutionFilePath = possibleSolutionPaths[0];
                    isSolution = true;
                    return (true, string.Empty);
                }

                return (false, LocalizableStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }
            else  // If no solutions are found, look for a project file
            {
                string[] possibleProjectPath = GetProjectFilePaths(directory);

                if (possibleProjectPath.Length == 0)
                {
                    return (false, LocalizableStrings.CmdNoProjectOrSolutionFileErrorDescription);
                }

                if (possibleProjectPath.Length == 1)
                {
                    projectOrSolutionFilePath = possibleProjectPath[0];
                    return (true, string.Empty);
                }

                return (false, string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, directory));
            }
        }

        private static string[] GetSolutionFilePaths(string directory)
        {
            return Directory.EnumerateFiles(directory, CliConstants.SolutionExtensionPattern, SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, CliConstants.SolutionXExtensionPattern, SearchOption.TopDirectoryOnly))
                .ToArray();
        }

        private static string[] GetProjectFilePaths(string directory) => [.. Directory.EnumerateFiles(directory, CliConstants.ProjectExtensionPattern, SearchOption.TopDirectoryOnly).Where(IsProjectFile)];

        private static bool IsProjectFile(string filePath) => CliConstants.ProjectExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

        public static async Task<IEnumerable<string>> ParseSolution(string solutionFilePath, string directory)
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
                projectsPaths.AddRange(solution.SolutionProjects.Select(project => Path.Combine(directory, project.FilePath)));
            }

            return projectsPaths;
        }
    }
}
