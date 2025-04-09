// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet;

static class PathUtilities
{
    public static string CreateTempSubdirectory()
    {
#if NETFRAMEWORK
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
#else
        return Directory.CreateTempSubdirectory().FullName;
#endif
    }
}
