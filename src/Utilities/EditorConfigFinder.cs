// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class EditorConfigFinder
    {
        public static ImmutableArray<string> GetEditorConfigPaths(string path)
        {
            var startPath = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);

            if (!Directory.Exists(startPath))
            {
                return ImmutableArray<string>.Empty;
            }

            var directory = new DirectoryInfo(path);

            var editorConfigPaths = directory.GetFiles(".editorconfig", SearchOption.AllDirectories)
                .Select(file => file.FullName)
                .ToList();

            while (directory.Parent != null)
            {
                directory = directory.Parent;

                editorConfigPaths.AddRange(
                    directory.GetFiles(".editorconfig", SearchOption.TopDirectoryOnly)
                        .Select(file => file.FullName));
            }

            return editorConfigPaths.ToImmutableArray();
        }
    }
}
