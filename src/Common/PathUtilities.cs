// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet;

static class PathUtilities
{
    public static string CreateTempSubdirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);

#if NET
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
// #else is only used by Microsoft.NET.TestFramework.csproj for netframework support. nothing to do on unix.
#endif

        return path;
    }
}
