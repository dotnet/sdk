// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetDependsOnNETStandardMultiThreading
    {
        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new GetDependsOnNETStandard();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GetDependsOnNETStandard).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ReferencePath_IsResolvedRelativeToProjectDirectory()
        {
            // Create a unique temp directory to act as the project directory
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"netstandard-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Place a copy of this test assembly at a relative path under the project dir
                var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardMultiThreading).Assembly.Location;
                var relativeRefDir = Path.Combine(projectDir, "refs");
                Directory.CreateDirectory(relativeRefDir);
                var assemblyFileName = Path.GetFileName(thisAssemblyPath);
                var destPath = Path.Combine(relativeRefDir, assemblyFileName);
                File.Copy(thisAssemblyPath, destPath);

                var task = new GetDependsOnNETStandard
                {
                    BuildEngine = new MockBuildEngine(),
                    References = new ITaskItem[]
                    {
                        new MockTaskItem { ItemSpec = $"refs\\{assemblyFileName}" }
                    }
                };

                // Set TaskEnvironment via reflection (property may not exist yet)
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                var result = task.Execute();

                result.Should().BeTrue("task should succeed");
                // This test assembly references System.Runtime, so DependsOnNETStandard should be true
                // when the relative path is resolved via TaskEnvironment (project dir), not CWD
                task.DependsOnNETStandard.Should().BeTrue(
                    "the assembly at the relative path (resolved via TaskEnvironment) references System.Runtime");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
