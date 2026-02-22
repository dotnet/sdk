// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetPackageDirectoryMultiThreading
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

        private static (bool result, MockBuildEngine engine) RunTask(
            string assetsRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new GetPackageDirectory
            {
                BuildEngine = engine,
                AssetsFileWithAdditionalPackageFolders = assetsRelPath,
                Items = Array.Empty<ITaskItem>(),
                PackageFolders = Array.Empty<string>(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            var result = task.Execute();
            return (result, engine);
        }

        [Fact]
        public void AssetsFile_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pkgdir-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var objDir = Path.Combine(projectDir, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "project.assets.json"), AssetsJson);

                var task = new GetPackageDirectory
                {
                    BuildEngine = new MockBuildEngine(),
                    AssetsFileWithAdditionalPackageFolders = "obj\\project.assets.json",
                    Items = Array.Empty<ITaskItem>(),
                    PackageFolders = Array.Empty<string>(),
                };

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

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pkgdir-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pkgdir-decoy-{Guid.NewGuid():N}"));
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
                for (int i = 0; i < mpEngine.Errors.Count; i++)
                {
                    mpEngine.Errors[i].Message.Should().Be(
                        mtEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
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
    }
}
