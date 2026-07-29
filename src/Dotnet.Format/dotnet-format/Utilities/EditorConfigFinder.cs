// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class EditorConfigFinder
    {
        public static ImmutableArray<string> GetEditorConfigPathsForFiles(ImmutableArray<string> filePaths)
        {
            var editorConfigPaths = ImmutableArray.CreateBuilder<string>(16);
            var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in filePaths)
            {
                var directoryName = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directoryName))
                {
                    continue;
                }

                var directory = new DirectoryInfo(directoryName);

                // Walk from the file's directory up to the drive root adding .editorconfig files.
                // Stop when reaching a directory already visited for a previous file, since its
                // ancestors have been visited as well.
                while (directory is not null && visitedDirectories.Add(directory.FullName))
                {
                    var editorConfigPath = Path.Combine(directory.FullName, ".editorconfig");
                    if (File.Exists(editorConfigPath))
                    {
                        editorConfigPaths.Add(editorConfigPath);
                    }

                    directory = directory.Parent;
                }
            }

            return editorConfigPaths.ToImmutable();
        }
    }
}
