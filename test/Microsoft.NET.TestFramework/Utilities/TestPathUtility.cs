// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities;

public static class TestPathUtility
{
#if NET
    /// <summary>
    /// For path like <c>/tmp/something</c>, returns <c>/private/tmp/something</c> on macOS.
    /// </summary>
    public static string ResolveTempPrefixLink(string path)
    {
        // SDK tests use /tmp for test assets. On macOS, it is a symlink - the app will print the resolved path
        if (OperatingSystem.IsMacOS())
        {
            string tmpPath = "/tmp/";
            var tmp = new DirectoryInfo(tmpPath[..^1]); // No trailing slash in order to properly check the link target
            if (tmp.LinkTarget != null && path.StartsWith(tmpPath) && tmp.ResolveLinkTarget(true) is { } linkTarget)
            {
                return Path.Combine(linkTarget.FullName, path[tmpPath.Length..]);
            }
        }

        return path;
    }
#endif
}
