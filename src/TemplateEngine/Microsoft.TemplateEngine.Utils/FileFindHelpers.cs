// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    public static class FileFindHelpers
    {
        // Walks up the directory path looking for files that match the matchPattern and the secondary filter (if provided)
        // Returns all the matching files in the first directory that has any matched files.
        public static IReadOnlyList<string> FindFilesAtOrAbovePath(IPhysicalFileSystem fileSystem, string startPath, string matchPattern, Func<string, bool> secondaryFilter = null)
        {
            string directory;
            if (fileSystem.DirectoryExists(startPath))
            {
                directory = startPath;
            }
            else
            {
                directory = Path.GetDirectoryName(startPath);
            }

            do
            {
                List<string> filesInDir = fileSystem.EnumerateFileSystemEntries(directory, matchPattern, SearchOption.TopDirectoryOnly).ToList();
                List<string> matches = new List<string>();

                if (secondaryFilter == null)
                {
                    matches = filesInDir;
                }
                else
                {
                    matches = filesInDir.Where(x => secondaryFilter(x)).ToList();
                }

                if (matches.Count > 0)
                {
                    return matches;
                }

                if (Path.GetPathRoot(directory) != directory)
                {
                    directory = Directory.GetParent(directory).FullName;
                }
                else
                {
                    directory = null;
                }
            }
            while (directory != null);

            return new List<string>();
        }
    }
}
