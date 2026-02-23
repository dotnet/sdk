// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAFilterResolvedFilesMultiThreading
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
        public void FilterResolvedFiles_HasMultiThreadableAttribute()
        {
            typeof(FilterResolvedFiles).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void AssetsFilePath_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"filter-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var assetsDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(assetsDir);
                File.WriteAllText(Path.Combine(assetsDir, "project.assets.json"), AssetsJson);

                var task = new FilterResolvedFiles
                {
                    BuildEngine = new MockBuildEngine(),
                    AssetsFilePath = Path.Combine("obj", "project.assets.json"),
                    ResolvedFiles = Array.Empty<ITaskItem>(),
                    PackagesToPrune = Array.Empty<ITaskItem>(),
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                var result = task.Execute();
                result.Should().BeTrue("task should succeed when assets file is found via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        private static (bool result, MockBuildEngine engine) RunTask(
            string assetsRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new FilterResolvedFiles
            {
                BuildEngine = engine,
                AssetsFilePath = assetsRelPath,
                ResolvedFiles = Array.Empty<ITaskItem>(),
                PackagesToPrune = Array.Empty<ITaskItem>(),
                TargetFramework = ".NETCoreApp,Version=v8.0",
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            var result = task.Execute();
            return (result, engine);
        }

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"filter-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"filter-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), AssetsJson);

                var assetsRelPath = Path.Combine("obj", "project.assets.json");

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine) = RunTask(assetsRelPath, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine) = RunTask(assetsRelPath, projectDir);

                mpResult.Should().Be(mtResult,
                    "task should return the same success/failure in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");
                mpEngine.Warnings.Count.Should().Be(mtEngine.Warnings.Count,
                    "warning count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task FilterResolvedFiles_ConcurrentExecution(int parallelism)
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"filter-concurrent-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), AssetsJson);

                var errors = new ConcurrentBag<string>();
                using var startGate = new ManualResetEventSlim(false);
                var tasks = new System.Threading.Tasks.Task[parallelism];
                for (int i = 0; i < parallelism; i++)
                {
                    int idx = i;
                    tasks[idx] = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var task = new FilterResolvedFiles
                            {
                                BuildEngine = new MockBuildEngine(),
                                AssetsFilePath = Path.Combine("obj", "project.assets.json"),
                                ResolvedFiles = Array.Empty<ITaskItem>(),
                                PackagesToPrune = Array.Empty<ITaskItem>(),
                                TargetFramework = ".NETCoreApp,Version=v8.0",
                                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                            };
                            startGate.Wait();
                            var result = task.Execute();
                            if (!result) errors.Add($"Thread {idx}: Execute returned false");
                        }
                        catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
                    });
                }
                startGate.Set();
                await System.Threading.Tasks.Task.WhenAll(tasks);

                errors.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
