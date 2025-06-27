// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateDepsFile
    {
        private readonly string _depsFilePath;

        public GivenAGenerateDepsFile()
        {
            string testTempDir = Path.Combine(Path.GetTempPath(), "dotnetSdkTests");
            Directory.CreateDirectory(testTempDir);
            _depsFilePath = Path.Combine(testTempDir, nameof(GivenAGenerateDepsFile) + ".deps.json");
            if (File.Exists(_depsFilePath))
            {
                File.Delete(_depsFilePath);
            }
        }

        [Fact]
        public void ItDoesNotOverwriteFileWithSameContent()
        {
            // Execute task first time
            var task = CreateTestTask();
            task.PublicExecuteCore();
            var firstWriteTime = File.GetLastWriteTimeUtc(_depsFilePath);

            // Wait a bit to ensure timestamp would change if file is rewritten
            Thread.Sleep(100);

            // Execute task again with same configuration
            var task2 = CreateTestTask();
            task2.PublicExecuteCore();
            var secondWriteTime = File.GetLastWriteTimeUtc(_depsFilePath);

            // File should not have been rewritten when content is the same
            secondWriteTime.Should().Be(firstWriteTime, "file should not be rewritten when content is unchanged");
        }

        private TestableGenerateDepsFile CreateTestTask()
        {
            return new TestableGenerateDepsFile
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                ProjectPath = "TestProject.csproj",
                DepsFilePath = _depsFilePath,
                TargetFramework = "net8.0",
                AssemblyName = "TestProject",
                AssemblyExtension = ".dll",
                AssemblyVersion = "1.0.0.0",
                IncludeMainProject = true,
                CompileReferences = new MockTaskItem[0],
                ResolvedNuGetFiles = new MockTaskItem[0],
                ResolvedRuntimeTargetsFiles = new MockTaskItem[0],
                RuntimeGraphPath = ""
            };
        }

        private class TestableGenerateDepsFile : GenerateDepsFile
        {
            public void PublicExecuteCore()
            {
                base.ExecuteCore();
            }
        }
    }
}