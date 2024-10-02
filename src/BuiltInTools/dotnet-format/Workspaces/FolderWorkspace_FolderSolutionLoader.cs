// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Tools.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private static class FolderSolutionLoader
        {
            private static ImmutableArray<ProjectLoader> ProjectLoaders
                => ImmutableArray.Create<ProjectLoader>(new CSharpProjectLoader(), new VisualBasicProjectLoader());

            public static SolutionInfo LoadSolutionInfo(string folderPath, SourceFileMatcher fileMatcher)
            {
                var absoluteFolderPath = Path.GetFullPath(folderPath, Directory.GetCurrentDirectory());

                var filePaths = GetMatchingFilePaths(absoluteFolderPath, fileMatcher);
                var editorConfigPaths = EditorConfigFinder.GetEditorConfigPaths(folderPath);

                var projectInfos = ImmutableArray.CreateBuilder<ProjectInfo>(ProjectLoaders.Length);

                // Create projects for each of the supported languages.
                foreach (var loader in ProjectLoaders)
                {
                    var projectInfo = loader.LoadProjectInfo(folderPath, filePaths, editorConfigPaths);
                    if (projectInfo is null)
                    {
                        continue;
                    }

                    projectInfos.Add(projectInfo);
                }

                // Construct workspace from loaded project infos.
                return SolutionInfo.Create(
                    SolutionId.CreateNewId(debugName: absoluteFolderPath),
                    version: default,
                    absoluteFolderPath,
                    projectInfos);
            }

            private static ImmutableArray<string> GetMatchingFilePaths(string folderPath, SourceFileMatcher fileMatcher)
            {
                // If only file paths were given to be included, then avoid matching against all
                // the files beneath the folderPath and instead check if the specified files exist.
                if (fileMatcher.Exclude.IsDefaultOrEmpty && AreAllFilePaths(fileMatcher.Include))
                {
                    return ValidateFilePaths(folderPath, fileMatcher.Include);
                }

                return fileMatcher.GetResultsInFullPath(folderPath).ToImmutableArray();

                static bool AreAllFilePaths(ImmutableArray<string> globs)
                {
                    for (var index = 0; index < globs.Length; index++)
                    {
                        // The FileSystemGlobbing.Matcher only supports the '*' wildcard and paths
                        // ending in a directory separator are treated as folder paths.
                        if (globs[index].Contains('*') ||
                            globs[index].EndsWith('\\') ||
                            globs[index].EndsWith('/'))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                static ImmutableArray<string> ValidateFilePaths(string folderPath, ImmutableArray<string> paths)
                {
                    var filePaths = ImmutableArray.CreateBuilder<string>(paths.Length);
                    for (var index = 0; index < paths.Length; index++)
                    {
                        var filePath = Path.GetFullPath(paths[index], folderPath);
                        if (File.Exists(filePath))
                        {
                            filePaths.Add(filePath);
                        }
                    }

                    return filePaths.ToImmutable();
                }
            }
        }
    }
}
