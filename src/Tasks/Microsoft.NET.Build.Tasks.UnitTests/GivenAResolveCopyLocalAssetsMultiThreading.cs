// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection("CWD-Dependent")]

    public class GivenAResolveCopyLocalAssetsMultiThreading
    {
        private const string AssetsJson = """
            {
              "version": 3,
              "targets": { ".NETCoreApp,Version=v8.0": {} },
              "libraries": {},
              "packageFolders": {},
              "projectFileDependencyGroups": { ".NETCoreApp,Version=v8.0": [] },
              "project": {
                "version": "1.0.0",
                "frameworks": { "net8.0": {} }
              }
            }
            """;

        [Fact]
        public void AssetsFilePath_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"copyloc-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), AssetsJson);

                var task = new ResolveCopyLocalAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    AssetsFilePath = Path.Combine("obj", "project.assets.json"),
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    RuntimeIdentifier = "",
                    IsSelfContained = false,
                    ResolveRuntimeTargets = false,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                var result = task.Execute();

                result.Should().BeTrue("task should succeed when assets file is found via TaskEnvironment");
                task.ResolvedAssets.Should().BeEmpty("no packages in the lock file means no assets");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"copyloc-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"copyloc-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), AssetsJson);

                var assetsRelPath = Path.Combine("obj", "project.assets.json");
                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var engine1 = new MockBuildEngine();
                var task1 = new ResolveCopyLocalAssets
                {
                    BuildEngine = engine1,
                    AssetsFilePath = assetsRelPath,
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    RuntimeIdentifier = "",
                    IsSelfContained = false,
                    ResolveRuntimeTargets = false,
                    TaskEnvironment = taskEnv,
                };
                var result1 = task1.Execute();

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var engine2 = new MockBuildEngine();
                var task2 = new ResolveCopyLocalAssets
                {
                    BuildEngine = engine2,
                    AssetsFilePath = assetsRelPath,
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    RuntimeIdentifier = "",
                    IsSelfContained = false,
                    ResolveRuntimeTargets = false,
                    TaskEnvironment = taskEnv,
                };
                var result2 = task2.Execute();

                result1.Should().Be(result2,
                    "task should return the same result regardless of CWD");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should be identical in both environments");
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
