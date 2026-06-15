// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetAssemblyAttributesMultiThreading
    {
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

                var task = new GetAssemblyAttributes
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    PathToTemplateFile = relativeFileName,
                };

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
