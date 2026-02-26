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
        public void ItSavesFileRelativeToTaskEnvironmentProjectDir()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "gtsf-test-" + Guid.NewGuid().ToString("N"));
            var outputDir = Path.Combine(projectDir, "output");
            Directory.CreateDirectory(outputDir);
            try
            {
                var task = new GenerateToolsSettingsFile
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    EntryPointRelativePath = "tool.dll",
                    CommandName = "mytool",
                    ToolsSettingsFilePath = "output/settings.xml",
                };

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
