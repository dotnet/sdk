// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateToolsSettingsFileMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new GenerateToolsSettingsFile();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GenerateToolsSettingsFile).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItSavesFileRelativeToTaskEnvironmentProjectDir()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "gtsf-test-" + Guid.NewGuid().ToString("N"));
            var outputDir = Path.Combine(projectDir, "output");
            Directory.CreateDirectory(outputDir);
            try
            {
                var task = new GenerateToolsSettingsFile
                {
                    EntryPointRelativePath = "tool.dll",
                    CommandName = "mytool",
                    ToolsSettingsFilePath = "output/settings.xml",
                };
                task.BuildEngine = new MockBuildEngine();

                // Set TaskEnvironment via reflection to avoid compile-time coupling.
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                task.Execute().Should().BeTrue();

                var expectedFile = Path.Combine(projectDir, "output", "settings.xml");
                File.Exists(expectedFile).Should().BeTrue("the file should be written under the project dir, not the CWD");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
