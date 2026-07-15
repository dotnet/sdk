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

                // A non-global .editorconfig only affects files in its own directory subtree, so collect
                // configs by walking up from each included file rather than scanning the whole workspace
                // tree (which in large repos may contain sizable non-.NET subtrees such as node_modules).
                // An .editorconfig marked is_global is not directory-scoped; one outside the walked
                // ancestors would not be discovered (see PR notes).
                var editorConfigPaths = EditorConfigFinder.GetEditorConfigPathsForFiles(filePaths);

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
