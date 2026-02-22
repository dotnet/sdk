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
        public void ShowMissingWorkloads_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);
            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
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
                    barrier.SignalAndWait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {i}: {ex.Message}"); }
            });
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
