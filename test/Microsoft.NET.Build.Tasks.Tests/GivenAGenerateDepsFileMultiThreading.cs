// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection("CWD-Dependent")]
    public class GivenAGenerateDepsFileMultiThreading
    {
        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            try
            {
                SetupOutputDir(projectDir);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1, ex1) = RunTask(projectDir);

                // Clean output so second run can write again
                CleanOutput(projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2, ex2) = RunTask(projectDir);

                result1.Should().Be(result2,
                    "task should return the same success/failure in both environments");
                (ex1 == null).Should().Be(ex2 == null,
                    "both environments should agree on whether an exception is thrown");
                if (ex1 != null && ex2 != null)
                {
                    ex1.GetType().Should().Be(ex2.GetType(),
                        "exception type should be identical in both environments");
                }
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < engine1.Errors.Count; i++)
                {
                    engine1.Errors[i].Message.Should().Be(
                        engine2.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count,
                    "warning count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void FilesWritten_PreservesRelativeDepsFilePath()
        {
            // Migration absolutizes DepsFilePath for File.Create() internally,
            // but FilesWritten output must preserve the original relative form.
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-rel-{Guid.NewGuid():N}"));
            var decoyDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-rel-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(decoyDir);
            try
            {
                SetupOutputDir(projectDir);
                Directory.SetCurrentDirectory(decoyDir);

                var task = CreateTask(projectDir);

                Exception? caughtEx = null;
                bool result = false;
                try { result = task.Execute(); }
                catch (Exception ex) { caughtEx = ex; }

                // Task should find/create files via TaskEnvironment, not CWD
                if (caughtEx != null)
                {
                    caughtEx.Should().NotBeOfType<FileNotFoundException>(
                        "task should resolve paths via TaskEnvironment, not CWD");
                    caughtEx.Should().NotBeOfType<DirectoryNotFoundException>(
                        "task should resolve paths via TaskEnvironment, not CWD");
                }

                // FilesWritten output must preserve the original relative DepsFilePath
                result.Should().BeTrue("task must succeed for FilesWritten assertions to be meaningful");
                var depsRelPath = Path.Combine("output", "myapp.deps.json");
                task.FilesWritten.Should().ContainSingle();
                task.FilesWritten[0].ItemSpec.Should().Be(depsRelPath,
                    "FilesWritten must preserve the original relative DepsFilePath — absolutization is internal only");
                Path.IsPathRooted(task.FilesWritten[0].ItemSpec).Should().BeFalse(
                    "output path should remain relative");
            }
            finally
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, true);
            }
        }

        [Fact]
        public void AssetsFilePath_AbsolutizedBeforeLockFileCache()
        {
            // Exercises the AssetsFilePath absolutization path (GenerateDepsFile.cs:320)
            // that LockFileCache rejects with "Path not rooted" if not absolutized.
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-assets-{Guid.NewGuid():N}"));
            var decoyDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-assets-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(decoyDir);
            try
            {
                SetupOutputDir(projectDir);
                // Create a minimal project.assets.json
                Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
                File.WriteAllText(Path.Combine(projectDir, "obj", "project.assets.json"), MinimalAssetsJson);

                Directory.SetCurrentDirectory(decoyDir);

                var task = CreateTaskWithAssetsFile(projectDir);
                task.Execute();

                var engine = (MockBuildEngine)task.BuildEngine;
                // LockFileCache rejects relative paths with "not rooted" — migration must prevent this
                engine.Errors.Select(e => e.Message ?? "").Should().NotContain(m =>
                    m.Contains("not rooted", StringComparison.OrdinalIgnoreCase),
                    "AssetsFilePath should be absolutized by migration before reaching LockFileCache");
            }
            finally
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task GenerateDepsFile_ConcurrentExecution(int parallelism)
        {
            var decoyDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"deps-conc-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(decoyDir);
            try
            {
                Directory.SetCurrentDirectory(decoyDir);

                var results = new ConcurrentBag<(bool success, string exType, string exMsg)>();
                using var startGate = new ManualResetEventSlim(false);
                var tasks = new System.Threading.Tasks.Task[parallelism];
                for (int i = 0; i < parallelism; i++)
                {
                    int idx = i;
                    tasks[idx] = System.Threading.Tasks.Task.Run(() =>
                        RunConcurrentWorker(idx, startGate, results));
                }
                startGate.Set();
                await System.Threading.Tasks.Task.WhenAll(tasks);

                results.Select(r => r.exType).Distinct().Should().HaveCount(1,
                    "all threads should experience the same failure mode");
                results.Should().NotContain(r => r.exType == nameof(FileNotFoundException)
                    || r.exType == nameof(DirectoryNotFoundException),
                    "tasks should find/create files via TaskEnvironment, not CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, true);
            }
        }

        private static void RunConcurrentWorker(
            int idx,
            ManualResetEventSlim startGate,
            ConcurrentBag<(bool success, string exType, string exMsg)> results)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"deps-conc-{idx}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            try
            {
                SetupOutputDir(projectDir);
                var task = CreateTask(projectDir);

                startGate.Wait();
                var result = task.Execute();
                results.Add((result, "none", ""));
            }
            catch (Exception ex)
            {
                results.Add((false, ex.GetType().Name, ex.Message));
            }
            finally
            {
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
            }
        }

        private static void SetupOutputDir(string projectDir)
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "output"));
        }

        private static void CleanOutput(string projectDir)
        {
            var depsFile = Path.Combine(projectDir, "output", "myapp.deps.json");
            if (File.Exists(depsFile)) File.Delete(depsFile);
        }

        private static GenerateDepsFile CreateTask(string projectDir)
        {
            return new GenerateDepsFile
            {
                BuildEngine = new MockBuildEngine(),
                ProjectPath = Path.Combine("src", "myapp.csproj"),
                DepsFilePath = Path.Combine("output", "myapp.deps.json"),
                TargetFramework = "net8.0",
                AssemblyName = "myapp",
                AssemblyExtension = ".dll",
                AssemblyVersion = "1.0.0",
                IncludeMainProject = true,
                // Skip AssetsFilePath (null) — avoids needing a real lock file
                // Skip RuntimeGraphPath validation — IsSelfContained=false
                IsSelfContained = false,
                CompileReferences = Array.Empty<ITaskItem>(),
                ResolvedNuGetFiles = Array.Empty<ITaskItem>(),
                ResolvedRuntimeTargetsFiles = Array.Empty<ITaskItem>(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };
        }

        private static GenerateDepsFile CreateTaskWithAssetsFile(string projectDir)
        {
            return new GenerateDepsFile
            {
                BuildEngine = new MockBuildEngine(),
                ProjectPath = Path.Combine("src", "myapp.csproj"),
                DepsFilePath = Path.Combine("output", "myapp.deps.json"),
                AssetsFilePath = Path.Combine("obj", "project.assets.json"),
                TargetFramework = "net8.0",
                AssemblyName = "myapp",
                AssemblyExtension = ".dll",
                AssemblyVersion = "1.0.0",
                IncludeMainProject = true,
                IsSelfContained = false,
                CompileReferences = Array.Empty<ITaskItem>(),
                ResolvedNuGetFiles = Array.Empty<ITaskItem>(),
                ResolvedRuntimeTargetsFiles = Array.Empty<ITaskItem>(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };
        }

        private static (bool result, MockBuildEngine engine, Exception? exception) RunTask(string projectDir)
        {
            var task = CreateTask(projectDir);
            var engine = (MockBuildEngine)task.BuildEngine;

            try
            {
                var result = task.Execute();
                return (result, engine, null);
            }
            catch (Exception ex)
            {
                return (false, engine, ex);
            }
        }

        private const string MinimalAssetsJson = """
            {
              "version": 3,
              "targets": { "net8.0": {} },
              "libraries": {},
              "projectFileDependencyGroups": { "net8.0": [] },
              "packageFolders": {},
              "project": {
                "version": "1.0.0",
                "restore": {
                  "projectUniqueName": "test",
                  "projectName": "test",
                  "projectPath": "test.csproj",
                  "packagesPath": "",
                  "outputPath": "",
                  "projectStyle": "PackageReference",
                  "originalTargetFrameworks": ["net8.0"],
                  "sources": {},
                  "frameworks": { "net8.0": { "targetAlias": "net8.0", "projectReferences": {} } }
                },
                "frameworks": { "net8.0": { "targetAlias": "net8.0" } }
              }
            }
            """;
    }
}
