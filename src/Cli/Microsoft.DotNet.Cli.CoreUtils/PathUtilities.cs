// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

public static class PathUtilities
{
    public static string EnsureTrailingSlash(string path)
        => EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);

    public static string EnsureTrailingForwardSlash(string path)
        => EnsureTrailingCharacter(path, '/');

    private static string EnsureTrailingCharacter(string path, char trailingCharacter)
    {
        // if the path is empty, we want to return the original string instead of a single trailing character.
        if (path.Length == 0 || path[path.Length - 1] == trailingCharacter)
        {
            return path;
        }

        return path + trailingCharacter;
    }
}
