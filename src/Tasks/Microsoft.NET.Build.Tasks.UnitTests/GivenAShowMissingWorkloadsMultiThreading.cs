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
    public class GivenAShowMissingWorkloadsMultiThreading
    {
        [Fact]
        public void EmptyMissingWorkloadPacks_DoesNotCrash()
        {
            var task = new ShowMissingWorkloads
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                MissingWorkloadPacks = Array.Empty<ITaskItem>(),
                NetCoreRoot = "",
                NETCoreSdkVersion = "8.0.100",
            };

            var act = () => task.Execute();
            act.Should().NotThrow("empty missing workload packs should be handled gracefully");
        }

        [Fact]
        public void ProjectDirectory_UsedInsteadOfCwd()
        {
            // ShowMissingWorkloads replaced Environment.CurrentDirectory with
            // TaskEnvironment.ProjectDirectory for global.json search.
            var projectDir = Path.Combine(Path.GetTempPath(), "smw-mt-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "smw-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();

            try
            {
                // Multiprocess: CWD == projectDir
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1) = RunTask(projectDir);

                // Multithreaded: CWD == otherDir
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2) = RunTask(projectDir);

                result1.Should().Be(result2,
                    "task result should match regardless of CWD");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should match");
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count,
                    "warning count should match");
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
        public async System.Threading.Tasks.Task ShowMissingWorkloads_ConcurrentExecution(int parallelism)
        {
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
                    var task = new ShowMissingWorkloads
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                        MissingWorkloadPacks = Array.Empty<ITaskItem>(),
                        NetCoreRoot = "",
                        NETCoreSdkVersion = "8.0.100",
                    };
                    startGate.Wait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
        }

        private static (bool result, MockBuildEngine engine) RunTask(string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new ShowMissingWorkloads
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                MissingWorkloadPacks = Array.Empty<ITaskItem>(),
                NetCoreRoot = "",
                NETCoreSdkVersion = "8.0.100",
            };

            bool result;
            try { result = task.Execute(); }
            catch { result = false; }
            return (result, engine);
        }
    }
}
