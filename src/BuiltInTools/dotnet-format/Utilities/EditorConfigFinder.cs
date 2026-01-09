// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    }
}
