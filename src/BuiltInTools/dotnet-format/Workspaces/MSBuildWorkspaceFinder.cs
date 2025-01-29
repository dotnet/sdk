// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

// Original License:
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// See https://github.com/aspnet/DotNetTools/blob/261b27b70027871143540af10a5cba57ce07ff97/src/dotnet-watch/Internal/MsBuildProjectFinder.cs

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal class MSBuildWorkspaceFinder
    {
        // Used to exclude dnx projects
        private const string DnxProjectExtension = ".xproj";

        /// <summary>
        /// Finds a compatible MSBuild project or solution.
        /// <param name="searchDirectory">The base directory to search</param>
        /// <param name="workspacePath">A specific project or solution file to find</param>
        /// </summary>
        public static (bool isSolution, string workspacePath) FindWorkspace(string searchDirectory, string? workspacePath = null)
        {
            if (!string.IsNullOrEmpty(workspacePath))
            {
                if (!Path.IsPathRooted(workspacePath))
                {
                    workspacePath = Path.GetFullPath(workspacePath, searchDirectory);
                }

                return Directory.Exists(workspacePath)
                    ? FindWorkspace(workspacePath!) // IsNullOrEmpty is not annotated on .NET Core 2.1
                    : FindFile(workspacePath!); // IsNullOrEmpty is not annotated on .NET Core 2.1
            }

            var foundSolution = FindMatchingFile(searchDirectory, FindSolutionFiles, Resources.Multiple_MSBuild_solution_files_found_in_0_Specify_which_to_use_with_the_workspace_argument);
            var foundProject = FindMatchingFile(searchDirectory, FindProjectFiles, Resources.Multiple_MSBuild_project_files_found_in_0_Specify_which_to_use_with_the_workspace_argument);

            if (!string.IsNullOrEmpty(foundSolution) && !string.IsNullOrEmpty(foundProject))
            {
                throw new FileNotFoundException(string.Format(Resources.Both_a_MSBuild_project_file_and_solution_file_found_in_0_Specify_which_to_use_with_the_workspace_argument, searchDirectory));
            }

            if (!string.IsNullOrEmpty(foundSolution))
            {
                return (true, foundSolution!); // IsNullOrEmpty is not annotated on .NET Core 2.1
            }

            if (!string.IsNullOrEmpty(foundProject))
            {
                return (false, foundProject!); // IsNullOrEmpty is not annotated on .NET Core 2.1
            }

            throw new FileNotFoundException(string.Format(Resources.Could_not_find_a_MSBuild_project_or_solution_file_in_0_Specify_which_to_use_with_the_workspace_argument, searchDirectory));
        }

        private static (bool isSolution, string workspacePath) FindFile(string workspacePath)
        {
            var workspaceExtension = Path.GetExtension(workspacePath);
            var isSolution = workspaceExtension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || workspaceExtension.Equals(".slnf", StringComparison.OrdinalIgnoreCase);
            var isProject = !isSolution
                && workspaceExtension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                && !workspaceExtension.Equals(DnxProjectExtension, StringComparison.OrdinalIgnoreCase);

            if (!isSolution && !isProject)
            {
                throw new FileNotFoundException(string.Format(Resources.The_file_0_does_not_appear_to_be_a_valid_project_or_solution_file, Path.GetFileName(workspacePath)));
            }

            if (!File.Exists(workspacePath))
            {
                var message = isSolution
                    ? Resources.The_solution_file_0_does_not_exist
                    : Resources.The_project_file_0_does_not_exist;
                throw new FileNotFoundException(string.Format(message, workspacePath));
            }

            return (isSolution, workspacePath);
        }

        private static IEnumerable<string> FindSolutionFiles(string basePath) => [
            ..Directory.EnumerateFileSystemEntries(basePath, "*.sln", SearchOption.TopDirectoryOnly),
            ..Directory.EnumerateFileSystemEntries(basePath, "*.slnf", SearchOption.TopDirectoryOnly),
            ..Directory.EnumerateFileSystemEntries(basePath, "*.slnx", SearchOption.TopDirectoryOnly)
        ];

        private static IEnumerable<string> FindProjectFiles(string basePath) => Directory.EnumerateFileSystemEntries(basePath, "*.*proj", SearchOption.TopDirectoryOnly)
                    .Where(f => !DnxProjectExtension.Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase));

        private static string? FindMatchingFile(string searchBase, Func<string, IEnumerable<string>> fileSelector, string multipleFilesFoundError)
        {
            if (!Directory.Exists(searchBase))
            {
                return null;
            }

            var files = fileSelector(searchBase).ToList();
            if (files.Count > 1)
            {
                throw new FileNotFoundException(string.Format(multipleFilesFoundError, searchBase));
            }

            return files.Count == 1
                ? files[0]
                : null;
        }
    }
}
