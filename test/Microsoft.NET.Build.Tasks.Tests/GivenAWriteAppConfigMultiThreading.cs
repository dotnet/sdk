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
    public class GivenAWriteAppConfigMultiThreading
    {
        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wac-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wac-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                SetupProjectLayout(projectDir);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1, ex1) = RunTask(projectDir);

                // Clean output for second run
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
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count,
                    "warning count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void OutputAppConfig_WrittenToCorrectLocation()
        {
            // With CWD set to decoyDir, task must write the output file
            // relative to TaskEnvironment's project directory, not CWD.
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wac-out-{Guid.NewGuid():N}"));
            var decoyDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wac-out-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(decoyDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                SetupProjectLayout(projectDir);
                Directory.SetCurrentDirectory(decoyDir);

                var task = CreateTask(projectDir);

                Exception? caughtEx = null;
                bool result = false;
                try { result = task.Execute(); }
                catch (Exception ex) { caughtEx = ex; }

                if (caughtEx != null)
                {
                    caughtEx.Should().NotBeOfType<FileNotFoundException>(
                        "task should resolve paths via TaskEnvironment, not CWD");
                    caughtEx.Should().NotBeOfType<DirectoryNotFoundException>(
                        "task should resolve paths via TaskEnvironment, not CWD");
                }

                result.Should().BeTrue("task must succeed for FilesWritten assertions to be meaningful");

                // Output file should be under projectDir, not decoyDir
                var expectedPath = Path.Combine(projectDir, "obj", "myapp.exe.config");
                File.Exists(expectedPath).Should().BeTrue(
                    "output app.config should be written relative to project directory");

                var decoyPath = Path.Combine(decoyDir, "obj", "myapp.exe.config");
                File.Exists(decoyPath).Should().BeFalse(
                    "output should NOT be written to CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task WriteAppConfig_ConcurrentExecution(int parallelism)
        {
            var decoyDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wac-conc-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(decoyDir);
            var savedCwd = Directory.GetCurrentDirectory();
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
                    "tasks should resolve paths via TaskEnvironment, not CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, true);
            }
        }

        private static void RunConcurrentWorker(
            int idx,
            ManualResetEventSlim startGate,
            ConcurrentBag<(bool success, string exType, string exMsg)> results)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"wac-conc-{idx}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            try
            {
                SetupProjectLayout(projectDir);
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

        private static void SetupProjectLayout(string projectDir)
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
            // Create a minimal app.config source
            var appConfigPath = Path.Combine(projectDir, "app.config");
            File.WriteAllText(appConfigPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n</configuration>");
        }

        private static void CleanOutput(string projectDir)
        {
            var outputFile = Path.Combine(projectDir, "obj", "myapp.exe.config");
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }

        private static WriteAppConfigWithSupportedRuntime CreateTask(string projectDir)
        {
            return new WriteAppConfigWithSupportedRuntime
            {
                BuildEngine = new MockBuildEngine(),
                AppConfigFile = new TaskItem(Path.Combine("app.config")),
                OutputAppConfigFile = new TaskItem(Path.Combine("obj", "myapp.exe.config")),
                TargetFrameworkIdentifier = ".NETFramework",
                TargetFrameworkVersion = "v4.7.2",
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
    }
}
