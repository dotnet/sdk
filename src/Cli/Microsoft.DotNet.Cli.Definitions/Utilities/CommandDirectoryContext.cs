// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

internal static class CommandDirectoryContext
{
    [ThreadStatic]
    private static string? _basePath;

    /// <summary>
    /// Expands a path similar to Path.GetFullPath() but gives unit tests a hook to inject an overwrite to the
    /// base path.
    /// </summary>
    /// <param name="path">A relative or absolute path specifier</param>
    /// <returns>The full path to the target</returns>
    public static string GetFullPath(string path)
        => _basePath != null
            ? Path.GetFullPath(path, _basePath)
            : Path.GetFullPath(path);

    internal static string? CurrentBaseDirectory_TestOnly
    {
        get => _basePath;
        set => _basePath = value;
    }
}
