// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal static class TestLockFiles
    {
        private static readonly string s_testAssemblyDirectory =
            Path.GetDirectoryName(typeof(TestLockFiles).Assembly.Location)!;

        public static LockFile GetLockFile(string lockFilePrefix)
        {
            string filePath = Path.Combine(s_testAssemblyDirectory, "LockFiles", $"{lockFilePrefix}.project.lock.json");

            return LockFileUtilities.GetLockFile(filePath, NullLogger.Instance);
        }

        public static LockFile CreateLockFile(string contents, string path = "path/to/project.lock.json")
        {
            return new LockFileFormat().Parse(contents, path);
        }
    }
}
