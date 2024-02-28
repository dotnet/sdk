// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

public static class PathWithVersions
{
    public const string VersionPlaceholder = "{VERSION}";

    public static bool Equal(string path1, string path2)
    {
        if (path1 == path2)
        {
            return true;
        }

        ReadOnlySpan<char> directory = path1;
        ReadOnlySpan<char> directory2 = path2;
        while (TryGetPathLeaf(directory, out var root, out var directoryPart) && TryGetPathLeaf(directory2, out var root2, out var directoryPart2))
        {
            if (!ReplaceVersionString(directoryPart).SequenceEqual(ReplaceVersionString(directoryPart2)))
            {
                return false;
            }
            directory= Path.GetDirectoryName(directory);
            directory2= Path.GetDirectoryName(directory2);
        }
        if (!directory.IsEmpty || !directory2.IsEmpty)
        {
            return false;
        }
        return true;
    }

    public static bool IsVersionString(ReadOnlySpan<char> directoryPart)
    {
        return directoryPart.Length >= 6
            && char.IsDigit(directoryPart[0])
            && directoryPart[1] == '.'
            && char.IsDigit(directoryPart[2])
            && directoryPart[3] == '.'
            && char.IsDigit(directoryPart[4])
            && ((char.IsDigit(directoryPart[5]) && char.IsDigit(directoryPart[6])) || directoryPart[5] == '-');
    }

    static ReadOnlySpan<char> ReplaceVersionString(ReadOnlySpan<char> directoryPart)
    {
        if (IsVersionString(directoryPart))
        {
            return VersionPlaceholder;
        }
        else
        {
            return directoryPart;
        }
    }

    static bool TryGetPathLeaf(ReadOnlySpan<char> path, out ReadOnlySpan<char> root, out ReadOnlySpan<char> leaf)
    {
        if (path.IsEmpty)
        {
            root = default;
            leaf = default;
            return false;
        }
        leaf = Path.GetFileName(path);
        root = Path.GetDirectoryName(path);
        return true;
    }

    public static string GetVersionlessPath(string path)
    {
        return GetVersionlessPath(path.AsSpan()).ToString();
    }

    public static ReadOnlySpan<char> GetVersionlessPath(ReadOnlySpan<char> path)
    {
        StringBuilder sb = new StringBuilder();
        bool altered = false;
        ReadOnlySpan<char> myPath = path;
        while (TryGetPathLeaf(myPath, out var directory, out var directoryPart))
        {
            sb = sb.Insert(0, Path.DirectorySeparatorChar);
            var versionOrDirectory = ReplaceVersionString(directoryPart);
            if (versionOrDirectory == VersionPlaceholder)
            {
                altered = true;
            }
            sb = sb.Insert(0, versionOrDirectory);
            myPath = directory;
        }
        if (!altered)
            return path;
        return sb.ToString();
    }

    public static ReadOnlySpan<char> GetVersionInPath(ReadOnlySpan<char> path)
    {
        ReadOnlySpan<char> myPath = path;
        while (TryGetPathLeaf(myPath, out var directory, out var directoryPart))
        {
            if (IsVersionString(directoryPart))
            {
                return directoryPart;
            }
            myPath = directory;
        }
        throw new ArgumentException("Path does not contain a version");
    }
}
