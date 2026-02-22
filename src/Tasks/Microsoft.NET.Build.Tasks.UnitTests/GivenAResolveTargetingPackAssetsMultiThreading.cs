// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveTargetingPackAssetsMultiThreading
    {
        [Fact]
        public void EmptyTargetingPacks_DoesNotCrash()
        {
            var task = new ResolveTargetingPackAssets
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                FrameworkReferences = Array.Empty<ITaskItem>(),
                ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                RuntimeFrameworks = Array.Empty<ITaskItem>(),
                GenerateErrorForMissingTargetingPacks = false,
            };

            var result = task.Execute();

            result.Should().BeTrue("empty targeting packs should succeed");
            task.ReferencesToAdd.Should().BeEmpty();
        }

        [Fact]
        public void TaskEnvironmentProperty_CanBeSet()
        {
            var task = new ResolveTargetingPackAssets();
            var te = TaskEnvironmentHelper.CreateForTest();

            var act = () => task.TaskEnvironment = te;
            act.Should().NotThrow("TaskEnvironment property should be settable");
            task.TaskEnvironment.Should().Be(te);
        }

        [Fact]
        public void CacheLookup_ReadsFromTaskEnvironment_NotProcessEnvironment()
        {
            // Verify that the cache lookup flag is read from TaskEnvironment,
            // not from the static process environment. This ensures thread-safe
            // per-task configuration.
            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new ResolveTargetingPackAssets
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                FrameworkReferences = Array.Empty<ITaskItem>(),
                ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                RuntimeFrameworks = Array.Empty<ITaskItem>(),
                GenerateErrorForMissingTargetingPacks = false,
            };

            // Task should succeed regardless of ALLOW_TARGETING_PACK_CACHING value
            var result = task.Execute();
            result.Should().BeTrue("task should succeed with empty inputs");
        }

        [Fact]
        public void EmptyTargetingPacks_ProducesSameResultsRegardlessOfCwd()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"rtpa-mt-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"rtpa-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // --- CWD = projectDir (multiprocess mode) ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1) = RunTask(projectDir);

                // --- CWD = otherDir (multithreaded mode) ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2) = RunTask(projectDir);

                result1.Should().Be(result2,
                    "task should return the same success/failure regardless of CWD");
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

        private static (bool result, MockBuildEngine engine) RunTask(string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new ResolveTargetingPackAssets
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                FrameworkReferences = Array.Empty<ITaskItem>(),
                ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                RuntimeFrameworks = Array.Empty<ITaskItem>(),
                GenerateErrorForMissingTargetingPacks = false,
            };

            var result = task.Execute();
            return (result, engine);
        }
    }
}
