// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities;

public static class TestPathUtility
{
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
