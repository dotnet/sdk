// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAFilterResolvedFilesMultiThreading
    {
        [Fact]
        public void AssetsFilePath_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"filter-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create a minimal lock file at a relative path
                var assetsDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(assetsDir);
                File.WriteAllText(Path.Combine(assetsDir, "project.assets.json"),
                    """
                    {
                      "version": 3,
                      "targets": { ".NETCoreApp,Version=v8.0": {} },
                      "libraries": {},
                      "projectFileDependencyGroups": { ".NETCoreApp,Version=v8.0": [] },
                      "project": {
                        "version": "1.0.0",
                        "frameworks": { "net8.0": {} }
                      }
                    }
                    """);

                var task = new FilterResolvedFiles
                {
                    BuildEngine = new MockBuildEngine(),
                    AssetsFilePath = "obj\\project.assets.json",
                    ResolvedFiles = Array.Empty<ITaskItem>(),
                    PackagesToPrune = Array.Empty<ITaskItem>(),
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                };

                // Set TaskEnvironment via reflection
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                var result = task.Execute();
                result.Should().BeTrue("task should succeed when assets file is found via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
