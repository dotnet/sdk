// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal static class PathUtilities
{
    public static readonly IEqualityComparer<string> OSSpecificPathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool ContainsPath(IReadOnlySet<string> directories, string fullPath)
    {
        fullPath = Path.TrimEndingDirectorySeparator(fullPath);

        while (true)
        {
            if (directories.Contains(fullPath))
            {
                return true;
            }

            var containingDir = Path.GetDirectoryName(fullPath);
            if (containingDir == null)
            {
                return false;
            }

            fullPath = containingDir;
        }
    }

    public static IEnumerable<string> GetContainingDirectories(string path)
    {
        while (true)
        {
            var containingDir = Path.GetDirectoryName(path);
            if (containingDir == null)
            {
                yield break;
            }

            yield return containingDir;
            path = containingDir;
        }
    }
}
