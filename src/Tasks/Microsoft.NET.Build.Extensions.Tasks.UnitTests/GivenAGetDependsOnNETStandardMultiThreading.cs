// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetDependsOnNETStandardMultiThreading
    {
        [Fact]
        public void ReferencePath_IsResolvedRelativeToProjectDirectory()
        {
            using var env = new TaskTestEnvironment();

            // Place a copy of this test assembly at a relative path under the project dir
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardMultiThreading).Assembly.Location;
            var assemblyFileName = Path.GetFileName(thisAssemblyPath);
            env.CreateProjectDirectory("refs");
            File.Copy(thisAssemblyPath, Path.Combine(env.ProjectDirectory, "refs", assemblyFileName));

            var task = new GetDependsOnNETStandard
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = env.TaskEnvironment,
                References = new ITaskItem[]
                {
                    new MockTaskItem { ItemSpec = Path.Combine("refs", assemblyFileName) }
                }
            };

            var result = task.Execute();

            result.Should().BeTrue("task should succeed");
            task.DependsOnNETStandard.Should().BeTrue(
                "the assembly at the relative path (resolved via TaskEnvironment) references System.Runtime");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DependsOnNETStandard_IsConsistent_RegardlessOfCwd(bool useDifferentCwd)
        {
            // Use the test assembly itself as input — it references System.Runtime.
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardMultiThreading).Assembly.Location;

            string projectDir = useDifferentCwd
                ? Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"netstandard-mt-{Guid.NewGuid():N}"))
                : Directory.GetCurrentDirectory();

            if (useDifferentCwd)
            {
                Directory.CreateDirectory(projectDir);
            }

            try
            {
                var task = new GetDependsOnNETStandard
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    References = new ITaskItem[]
                    {
                        new MockTaskItem { ItemSpec = thisAssemblyPath }
                    }
                };

                var result = task.Execute();

                result.Should().BeTrue("task should succeed");
                task.DependsOnNETStandard.Should().BeTrue(
                    "test assembly references System.Runtime, so DependsOnNETStandard should be true");
            }
            finally
            {
                if (useDifferentCwd && Directory.Exists(projectDir))
                {
                    Directory.Delete(projectDir, true);
                }
            }
        }
    }
}
