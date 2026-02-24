// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal static class TestLockFiles
    {
        // Use the assembly location so that tests are not affected by CWD changes
        // from other test classes (e.g., multithreading parity tests that call
        // Directory.SetCurrentDirectory).
        internal static readonly string TestAssemblyDirectory =
            Path.GetDirectoryName(typeof(TestLockFiles).Assembly.Location)!;

        public static LockFile GetLockFile(string lockFilePrefix)
        {
            string filePath = Path.Combine(TestAssemblyDirectory, "LockFiles", $"{lockFilePrefix}.project.lock.json");

            return LockFileUtilities.GetLockFile(filePath, NullLogger.Instance);
        }

        public static LockFile CreateLockFile(string contents, string path = "path/to/project.lock.json")
        {
            return new LockFileFormat().Parse(contents, path);
        }
    }
}
