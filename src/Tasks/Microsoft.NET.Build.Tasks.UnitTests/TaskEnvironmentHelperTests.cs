// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class TaskEnvironmentHelperTests
    {
        [Fact]
        public void CreateForTest_WithProjectDirectory_SetsProjectDirectory()
        {
            var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var env = TaskEnvironmentHelper.CreateForTest(tempDir);
            Assert.Equal(tempDir, env.ProjectDirectory.Value);
        }

        [Fact]
        public void CreateForTest_GetAbsolutePath_ResolvesRelativeToProjectDir()
        {
            var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var env = TaskEnvironmentHelper.CreateForTest(tempDir);
            var resolved = env.GetAbsolutePath("subdir/file.txt");
            Assert.StartsWith(tempDir, resolved.Value);
            Assert.Contains("subdir", resolved.Value);
        }

        [Fact]
        public void CreateForTest_GetAbsolutePath_ReturnsAbsolutePathUnchanged()
        {
            var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var env = TaskEnvironmentHelper.CreateForTest(tempDir);
            var absoluteInput = Path.Combine(tempDir, "some", "path.txt");
            var resolved = env.GetAbsolutePath(absoluteInput);
            Assert.Equal(absoluteInput, resolved.Value);
        }
    }
}
