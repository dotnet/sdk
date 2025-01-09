// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet;

static class PathUtilities
{
    public static string CreateTempSubdirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

#if NETFRAMEWORK
        Directory.CreateDirectory(path);
#else
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(path);
        else
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#endif

        return path;
    }
}
