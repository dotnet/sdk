// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAProduceContentAssetsMultiThreading
    {
        [Fact]
        public void ContentPreprocessorOutputDirectory_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"produce-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create a relative output directory under projectDir
                var ppOutputDir = Path.Combine(projectDir, "obj", "pp");
                Directory.CreateDirectory(ppOutputDir);

                var contentFile = new MockTaskItem("path/to/content.cs", new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.0.0" },
                    { "BuildAction", "Compile" },
                    { "CodeLanguage", "any" },
                    { "CopyToOutput", "false" },
                    { "PPOutputPath", "" },
                    { "OutputPath", "" }
                });

                var task = new ProduceContentAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    ContentFileDependencies = new ITaskItem[] { contentFile },
                    ContentPreprocessorOutputDirectory = Path.Combine("obj", "pp"),
                    ProjectLanguage = "C#",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                var result = task.Execute();

                result.Should().BeTrue("task should succeed with relative ContentPreprocessorOutputDirectory resolved via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameResultsRegardlessOfCwd()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"produce-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"produce-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var contentFile1 = new MockTaskItem("path/to/content.cs", new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.0.0" },
                    { "BuildAction", "Compile" },
                    { "CodeLanguage", "any" },
                    { "CopyToOutput", "false" },
                    { "PPOutputPath", "" },
                    { "OutputPath", "" }
                });
                var contentFile2 = new MockTaskItem("path/to/content.cs", new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.0.0" },
                    { "BuildAction", "Compile" },
                    { "CodeLanguage", "any" },
                    { "CopyToOutput", "false" },
                    { "PPOutputPath", "" },
                    { "OutputPath", "" }
                });

                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var engine1 = new MockBuildEngine();
                var task1 = new ProduceContentAssets
                {
                    BuildEngine = engine1,
                    ContentFileDependencies = new ITaskItem[] { contentFile1 },
                    ProjectLanguage = "C#",
                    TaskEnvironment = taskEnv,
                };
                var result1 = task1.Execute();

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var engine2 = new MockBuildEngine();
                var task2 = new ProduceContentAssets
                {
                    BuildEngine = engine2,
                    ContentFileDependencies = new ITaskItem[] { contentFile2 },
                    ProjectLanguage = "C#",
                    TaskEnvironment = taskEnv,
                };
                var result2 = task2.Execute();

                result1.Should().Be(result2,
                    "task should return the same result regardless of CWD");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
