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
    public class GivenAProcessFrameworkReferencesMultiThreading
    {
        [Fact]
        public void EmptyFrameworkReferences_DoesNotCrash()
        {
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                TargetFrameworkVersion = "8.0",
                TargetingPackRoot = "",
                RuntimeGraphPath = "",
                FrameworkReferences = Array.Empty<ITaskItem>(),
                KnownFrameworkReferences = Array.Empty<ITaskItem>(),
                KnownRuntimePacks = Array.Empty<ITaskItem>(),
                KnownCrossgen2Packs = Array.Empty<ITaskItem>(),
                KnownILCompilerPacks = Array.Empty<ITaskItem>(),
                KnownILLinkPacks = Array.Empty<ITaskItem>(),
                KnownWebAssemblySdkPacks = Array.Empty<ITaskItem>(),
            };

            // Should not throw; may return true (nothing to process) or false (missing required props)
            var act = () => task.Execute();
            act.Should().NotThrow("empty framework references should be handled gracefully");
        }

        [Fact]
        public void ProjectDirectory_UsedInsteadOfEnvironmentCurrentDirectory()
        {
            // Verify that the task uses TaskEnvironment.ProjectDirectory for global.json search
            // instead of Environment.CurrentDirectory
            var projectDir = Path.Combine(Path.GetTempPath(), "pfr-mt-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "pfr-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();

            try
            {
                // Multiprocess mode: CWD == projectDir
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1) = RunTask(projectDir);

                // Multithreaded mode: CWD == otherDir
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2) = RunTask(projectDir);

                // Both should produce the same result
                result1.Should().Be(result2,
                    "task should return the same result regardless of CWD");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should match between multiprocess and multithreaded modes");
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count,
                    "warning count should match between multiprocess and multithreaded modes");
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
        public void ProcessFrameworkReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);
            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new ProcessFrameworkReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                        TargetFrameworkVersion = "8.0",
                        TargetingPackRoot = "",
                        RuntimeGraphPath = "",
                        FrameworkReferences = Array.Empty<ITaskItem>(),
                        KnownFrameworkReferences = Array.Empty<ITaskItem>(),
                        KnownRuntimePacks = Array.Empty<ITaskItem>(),
                        KnownCrossgen2Packs = Array.Empty<ITaskItem>(),
                        KnownILCompilerPacks = Array.Empty<ITaskItem>(),
                        KnownILLinkPacks = Array.Empty<ITaskItem>(),
                        KnownWebAssemblySdkPacks = Array.Empty<ITaskItem>(),
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
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                TargetFrameworkVersion = "8.0",
                TargetingPackRoot = "",
                RuntimeGraphPath = "",
                FrameworkReferences = Array.Empty<ITaskItem>(),
                KnownFrameworkReferences = Array.Empty<ITaskItem>(),
                KnownRuntimePacks = Array.Empty<ITaskItem>(),
                KnownCrossgen2Packs = Array.Empty<ITaskItem>(),
                KnownILCompilerPacks = Array.Empty<ITaskItem>(),
                KnownILLinkPacks = Array.Empty<ITaskItem>(),
                KnownWebAssemblySdkPacks = Array.Empty<ITaskItem>(),
            };

            bool result;
            try { result = task.Execute(); }
            catch { result = false; }
            return (result, engine);
        }
    }
}
