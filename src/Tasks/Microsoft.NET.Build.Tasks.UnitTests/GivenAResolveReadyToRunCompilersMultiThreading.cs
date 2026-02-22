// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveReadyToRunCompilersMultiThreading
    {
        [Fact]
        public void EmptyRuntimePacks_LogsErrorGracefully()
        {
            var engine = new MockBuildEngine();
            var task = new ResolveReadyToRunCompilers
            {
                BuildEngine = engine,
                RuntimePacks = new ITaskItem[] { new TaskItem("SomePack") },
                TargetingPacks = Array.Empty<ITaskItem>(),
                RuntimeGraphPath = "nonexistent.json",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                ReadyToRunUseCrossgen2 = false,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            // RuntimePacks has no matching NETCore.App pack, so it should log error gracefully
            var result = task.Execute();

            result.Should().BeFalse("no valid NETCore.App runtime pack exists");
            engine.Errors.Should().NotBeEmpty("should log an error about missing runtime pack");
            // Verify no NullReferenceException — error is about missing pack, not a crash
            engine.Errors.Select(e => e.Message).Should().NotContain(
                e => e.Contains("NullReference", StringComparison.OrdinalIgnoreCase),
                "should not crash with NullReferenceException");
        }

        [Fact]
        public void TaskEnvironmentProperty_IsWirable()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"resolve-r2r-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new ResolveReadyToRunCompilers
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimePacks = new ITaskItem[] { new TaskItem("SomePack") },
                    TargetingPacks = Array.Empty<ITaskItem>(),
                    RuntimeGraphPath = "runtime.json",
                    NETCoreSdkRuntimeIdentifier = "win-x64",
                };

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                // The task should have TaskEnvironment set
                task.TaskEnvironment.Should().NotBeNull();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
