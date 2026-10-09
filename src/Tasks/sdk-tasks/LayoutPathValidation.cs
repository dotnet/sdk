// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks;

internal static class LayoutPathValidation
{
    public static void RejectLink(string path)
    {
        if (Path.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Owned layout path '{path}' must not be a symbolic link or junction.");
        }
    }

    public static void RejectDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            throw new IOException($"Owned layout file '{path}' must not be a directory.");
        }
    }
}
