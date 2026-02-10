// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetAssemblyAttributesMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new GetAssemblyAttributes();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GetAssemblyAttributes).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD)
            var projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(projectDir);
            try
            {
                // We need a real .NET assembly file for FileVersionInfo.GetVersionInfo to work.
                // Copy the test assembly itself to the project directory with a relative-path-friendly name.
                var sourceAssembly = typeof(GivenAGetAssemblyAttributesMultiThreading).Assembly.Location;
                var relativeFileName = "template.dll";
                var expectedAbsPath = Path.Combine(projectDir, relativeFileName);
                File.Copy(sourceAssembly, expectedAbsPath);

                var task = new GetAssemblyAttributes();
                task.BuildEngine = new MockBuildEngine();

                // Use reflection to set TaskEnvironment so the test compiles even before migration.
                // If the property doesn't exist, the test fails (unmigrated).
                var taskEnvProp = typeof(GetAssemblyAttributes).GetProperty("TaskEnvironment",
                    BindingFlags.Public | BindingFlags.Instance);
                taskEnvProp.Should().NotBeNull("GetAssemblyAttributes must have a TaskEnvironment property");
                taskEnvProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                task.PathToTemplateFile = relativeFileName;

                // Execute — should resolve the relative path via TaskEnvironment (projectDir),
                // NOT via the process CWD. If the task uses Path.GetFullPath, it would look
                // in CWD and fail to find the file.
                var result = task.Execute();

                result.Should().BeTrue("the task should succeed when the file exists under the project directory");
                task.AssemblyAttributes.Should().NotBeNull();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
