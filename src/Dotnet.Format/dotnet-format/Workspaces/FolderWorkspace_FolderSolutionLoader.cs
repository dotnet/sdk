// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

                var hasConcreteIncludePaths = fileMatcher.Exclude.IsDefaultOrEmpty && AreAllFilePaths(fileMatcher.Include);
                var filePaths = GetMatchingFilePaths(absoluteFolderPath, fileMatcher, hasConcreteIncludePaths);

                // When formatting an explicit list of files that all live under the workspace
                // folder there is no need to search the entire workspace for .editorconfig files.
                // Only configs in the files' ancestor directories can apply to them. Included
                // files resolving outside the workspace folder keep the original scan so their
                // behavior is unchanged.
                var folderPrefix = absoluteFolderPath.EndsWith(Path.DirectorySeparatorChar)
                    ? absoluteFolderPath
                    : absoluteFolderPath + Path.DirectorySeparatorChar;
                var editorConfigPaths = hasConcreteIncludePaths
                        && filePaths.All(filePath => filePath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                    ? EditorConfigFinder.GetEditorConfigPathsForFiles(filePaths)
                    : EditorConfigFinder.GetEditorConfigPaths(folderPath);

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

            private static ImmutableArray<string> GetMatchingFilePaths(string folderPath, SourceFileMatcher fileMatcher, bool hasConcreteIncludePaths)
            {
                // If only file paths were given to be included, then avoid matching against all
                // the files beneath the folderPath and instead check if the specified files exist.
                if (hasConcreteIncludePaths)
                {
                    return ValidateFilePaths(folderPath, fileMatcher.Include);
                }

                return fileMatcher.GetResultsInFullPath(folderPath).ToImmutableArray();

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

            private static bool AreAllFilePaths(ImmutableArray<string> globs)
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
        }
    }
}
