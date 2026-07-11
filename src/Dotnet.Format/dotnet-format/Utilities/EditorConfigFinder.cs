// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class EditorConfigFinder
    {
        public static ImmutableArray<string> GetEditorConfigPaths(string path)
        {
            // If the path is to a file then remove the file name and process the
            // folder path.
            var startPath = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);

            if (!Directory.Exists(startPath))
            {
                return ImmutableArray<string>.Empty;
            }

            var editorConfigPaths = ImmutableArray.CreateBuilder<string>(16);

            var directory = new DirectoryInfo(path);

            // Find .editorconfig files contained unders the folder path.
            var files = directory.GetFiles(".editorconfig", SearchOption.AllDirectories);
            for (var index = 0; index < files.Length; index++)
            {
                editorConfigPaths.Add(files[index].FullName);
            }

            // Walk from the folder path up to the drive root addings .editorconfig files.
            while (directory.Parent != null)
            {
                directory = directory.Parent;

                files = directory.GetFiles(".editorconfig", SearchOption.TopDirectoryOnly);
                if (files.Length == 1)
                {
                    editorConfigPaths.Add(files[0].FullName);
                }
            }

            return editorConfigPaths.ToImmutable();
        }

        public static ImmutableArray<string> GetEditorConfigPathsForFiles(ImmutableArray<string> filePaths)
        {
            var editorConfigPaths = ImmutableArray.CreateBuilder<string>(16);
            var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in filePaths)
            {
                var directoryName = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
                {
                    continue;
                }

                var directory = new DirectoryInfo(directoryName);

                // Walk from the file's directory up to the drive root adding .editorconfig files.
                // Stop when reaching a directory already visited for a previous file, since its
                // ancestors have been visited as well.
                while (directory is not null && visitedDirectories.Add(directory.FullName))
                {
                    var files = directory.GetFiles(".editorconfig", SearchOption.TopDirectoryOnly);
                    if (files.Length == 1)
                    {
                        editorConfigPaths.Add(files[0].FullName);
                    }

                    directory = directory.Parent;
                }
            }

            return editorConfigPaths.ToImmutable();
        }
    }
}
