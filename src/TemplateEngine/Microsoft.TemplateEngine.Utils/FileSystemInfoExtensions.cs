// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Utils
{
    public static class FileSystemInfoExtensions
    {
        public static void CopyTo(this IDirectory source, string target)
        {
            source.MountPoint.EnvironmentSettings.Host.FileSystem.CreateDirectory(target);

            foreach (IFile file in source.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                using (Stream f = source.MountPoint.EnvironmentSettings.Host.FileSystem.CreateFile(Path.Combine(target, file.Name)))
                using (Stream s = file.OpenRead())
                {
                    s.CopyTo(f);
                    f.Flush();
                }
            }

            foreach(IDirectory dir in source.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                dir.CopyTo(Path.Combine(target, dir.Name));
            }
        }

        public static string NormalizePath(this string path)
        {
            return path.Replace('\\', '/');
        }

        public static string CombinePaths(this string basePath, params string[] paths)
        {
            Stack<string> partStack = new Stack<string>();

            ProcessPath(basePath, partStack);

            for (int i = 0; i < paths.Length; ++i)
            {
                ProcessPath(paths[i], partStack);
            }

            return "/" + string.Join("/", partStack.Reverse());
        }

        private static void ProcessPath(string path, Stack<string> partStack)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string[] parts = path.Split('/');

            for (int i = 0; i < parts.Length; ++i)
            {
                switch (parts[i])
                {
                    case "":
                        if (i == 0)
                        {
                            partStack.Clear();
                        }
                        break;
                    case ".":
                        break;
                    case "..":
                        if (partStack.Count == 0)
                        {
                            throw new IOException($"Failed to combine paths, stack underflow at {string.Join("/", parts.Skip(i))}");
                        }

                        partStack.Pop();
                        break;
                    default:
                        partStack.Push(parts[i]);
                        break;
                }
            }
        }

        public static IFile FileInfo(this IFileSystemInfo info, string path)
        {
            string fullPath = info.FullPath.CombinePaths(path);
            return info.MountPoint.FileInfo(fullPath);
        }

        public static IDirectory DirectoryInfo(this IFileSystemInfo info, string path)
        {
            string fullPath = info.FullPath.CombinePaths(path);
            return info.MountPoint.DirectoryInfo(fullPath);
        }

        public static IFileSystemInfo FileSystemInfo(this IFileSystemInfo info, string path)
        {
            string fullPath = info.FullPath.CombinePaths(path);
            return info.MountPoint.FileSystemInfo(fullPath);
        }

        public static string PathRelativeTo(this IFileSystemInfo info, IFileSystemInfo relativeTo)
        {
            //The path should be relative to either source itself (in the case that it's a folder) or the parent of source)
            IDirectory relTo = relativeTo as IDirectory ?? relativeTo.Parent;

            //If the thing to be relative to is the root (or a file in the root), just use the full path of the item
            if (relTo == null)
            {
                return info.FullPath;
            }

            //Get all the path segments for the thing we're relative to
            Dictionary<string, int> sourceSegments = new Dictionary<string, int> { { relTo.FullPath, 0 } };
            IDirectory current = relTo.Parent;
            int index = 0;
            while (current != null)
            {
                sourceSegments[current.FullPath] = ++index;
                current = current.Parent;
            }

            current = info.Parent;
            List<string> segments = new List<string> { info.Name };

            //Walk back the set of parents of this item until one is contained by our source, building up a list as we go

#pragma warning disable IDE0018 // Inline variable declaration
            //If inlined, this breaks compilation for use of an unassigned variable
            int revIndex = 0;
#pragma warning restore IDE0018 // Inline variable declaration
            while (current != null && !sourceSegments.TryGetValue(current.FullPath, out revIndex))
            {
                segments.Insert(0, current.Name);
                current = current.Parent;
            }

            //Now that we've found our common point (and the index of the common segment _from the end_ of the source's parent chain)
            //  the number of levels up we need to go is the value of revIndex
            segments.InsertRange(0, Enumerable.Repeat("..", revIndex));
            return string.Join("/", segments);
        }
    }
}
