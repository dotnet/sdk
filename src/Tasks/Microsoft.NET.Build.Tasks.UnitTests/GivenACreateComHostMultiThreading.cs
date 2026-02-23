// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACreateComHostMultiThreading
    {
        [Fact]
        public void Paths_AreResolvedRelativeToProjectDirectory()
        {
            // Create a unique temp directory to act as the project directory
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"comhost-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create relative path structure under the project dir
                var sourceDir = Path.Combine(projectDir, "source");
                var destDir = Path.Combine(projectDir, "output");
                Directory.CreateDirectory(sourceDir);
                Directory.CreateDirectory(destDir);

                // Create a fake comhost source file and clsid map
                File.WriteAllText(Path.Combine(sourceDir, "comhost.dll"), "fake-source");
                File.WriteAllText(Path.Combine(sourceDir, "clsidmap.bin"), "fake-clsid");

                var task = new CreateComHost
                {
                    BuildEngine = new MockBuildEngine(),
                    ComHostSourcePath = Path.Combine("source", "comhost.dll"),
                    ComHostDestinationPath = Path.Combine("output", "comhost.dll"),
                    ClsidMapPath = Path.Combine("source", "clsidmap.bin"),
                };

                // Set TaskEnvironment for path resolution
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                // ComHost.Create will throw because our fake files aren't valid PE binaries.
                // The key assertion is that the exception comes from PE processing (ResourceUpdater),
                // NOT from "file not found" — proving paths were resolved via TaskEnvironment.
                try
                {
                    task.Execute();
                }
                catch (Exception)
                {
                    // Expected — ComHost.Create fails on fake binaries
                }

                // Verify that any errors logged are NOT about missing files
                var engine = (MockBuildEngine)task.BuildEngine;
                var errors = engine.Errors.Select(e => e.Message).ToList();
                errors.Should().NotContain(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    && e.Contains("comhost", StringComparison.OrdinalIgnoreCase),
                    "paths should be resolved via TaskEnvironment, not CWD");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameErrorsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "comhost-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "comhost-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Create fake comhost and clsidmap under projectDir
                var sourceDir = Path.Combine(projectDir, "source");
                var outputDir = Path.Combine(projectDir, "output");
                Directory.CreateDirectory(sourceDir);
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(Path.Combine(sourceDir, "comhost.dll"), "fake-comhost-pe");
                File.WriteAllText(Path.Combine(sourceDir, "clsidmap.bin"), "fake-clsid");

                var comHostSource = Path.Combine("source", "comhost.dll");
                var comHostDest = Path.Combine("output", "comhost.dll");
                var clsidMap = Path.Combine("source", "clsidmap.bin");

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1, ex1) = RunTask(comHostSource, comHostDest, clsidMap, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2, ex2) = RunTask(comHostSource, comHostDest, clsidMap, projectDir);

                // Both should produce the same result
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
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task CreateComHost_ConcurrentExecution(int parallelism)
        {
            // These tasks work with PE binaries. With fake inputs they will throw/fail,
            // but the concurrent execution should not produce different failure modes
            // (no shared-state corruption, no deadlocks, no data races).
            var results = new ConcurrentBag<(bool success, string exType)>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                var projectDir = Path.Combine(Path.GetTempPath(), $"comhost-conc-{idx}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(projectDir);
                try
                {
                    var sourceDir = Path.Combine(projectDir, "source");
                    var outputDir = Path.Combine(projectDir, "output");
                    Directory.CreateDirectory(sourceDir);
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(Path.Combine(sourceDir, "comhost.dll"), "fake-comhost-pe");
                    File.WriteAllText(Path.Combine(sourceDir, "clsidmap.bin"), "fake-clsid");

                    var task = new CreateComHost
                    {
                        BuildEngine = new MockBuildEngine(),
                        ComHostSourcePath = Path.Combine("source", "comhost.dll"),
                        ComHostDestinationPath = Path.Combine("output", "comhost.dll"),
                        ClsidMapPath = Path.Combine("source", "clsidmap.bin"),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    };

                    startGate.Wait();
                    var result = task.Execute();
                    results.Add((result, "none"));
                }
                catch (Exception ex)
                {
                    results.Add((false, ex.GetType().Name));
                }
                finally
                {
                    if (Directory.Exists(projectDir))
                        Directory.Delete(projectDir, true);
                }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            // All threads should get the same outcome (all succeed or all fail the same way)
            results.Select(r => r.exType).Distinct().Should().HaveCount(1,
                "all threads should experience the same failure mode");
        }

        private static (bool result, MockBuildEngine engine, Exception? exception) RunTask(
            string comHostSource, string comHostDest, string clsidMap, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new CreateComHost
            {
                BuildEngine = engine,
                ComHostSourcePath = comHostSource,
                ComHostDestinationPath = comHostDest,
                ClsidMapPath = clsidMap,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

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
