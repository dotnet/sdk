// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities;

public static class TestPathUtility
{
    /// <summary>
    /// Determines whether a path is absolute on the current platform.
    /// </summary>
    public static bool IsFullyQualifiedPath(string path)
    {
        // Path.IsPathFullyQualified is unavailable on .NET Framework.
        // On Windows, rooted paths such as "\logs" and "C:logs" still depend on the current directory.
        if (Path.DirectorySeparatorChar != '\\')
        {
            return Path.IsPathRooted(path);
        }

        return (path.Length >= 2
            && (path[0] == '\\' || path[0] == '/')
            && (path[1] == '\\' || path[1] == '/'))
            || (path.Length >= 3
                && char.IsLetter(path[0])
                && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/'));
    }

    /// <summary>
    /// Resolves a path against a base directory, including Windows root-relative paths.
    /// </summary>
    public static string ResolveFullPath(string path, string basePath)
    {
        if (Path.DirectorySeparatorChar == '\\'
            && path.Length > 0
            && (path[0] == '\\' || path[0] == '/')
            && (path.Length == 1 || (path[1] != '\\' && path[1] != '/')))
        {
            return Path.GetFullPath(Path.Combine(Path.GetPathRoot(basePath)!, path.TrimStart('\\', '/')));
        }

        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
    }

#if NET
    /// <summary>
    /// Resolves symlinked macOS temporary-directory prefixes, such as <c>/tmp</c> and <c>/var</c>.
    /// </summary>
    public static string ResolveTempPrefixLink(string path)
    {
        if (OperatingSystem.IsMacOS())
        {
            string[] tempPaths = ["/tmp/", "/var/"];
            foreach (string tempPath in tempPaths)
            {
                var tempRoot = new DirectoryInfo(tempPath[..^1]);
                if (path.StartsWith(tempPath, StringComparison.Ordinal)
                    && tempRoot.LinkTarget != null
                    && tempRoot.ResolveLinkTarget(true) is { } linkTarget)
                {
                    return Path.Combine(linkTarget.FullName, path[tempPath.Length..]);
                }
            }
        }

        return path;
    }
#endif
}
