// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAPickBestRidMultiThreading
    {
        private const string RuntimeGraphJson =
            """{"runtimes":{"win-x64":{"#import":["win","any"]},"win":{"#import":["any"]},"any":{}}}""";

        private static (bool result, MockBuildEngine engine, string? matchingRid) RunTask(
            string runtimeGraphRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new PickBestRid
            {
                BuildEngine = engine,
                RuntimeGraphPath = runtimeGraphRelPath,
                TargetRid = "win-x64",
                SupportedRids = new[] { "win-x64", "linux-x64" },
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            var result = task.Execute();
            return (result, engine, task.MatchingRid);
        }

        [Fact]
        public void RuntimeGraphPath_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pickrid-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                File.WriteAllText(Path.Combine(projectDir, "runtime.json"), RuntimeGraphJson);

                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = "runtime.json",
                    TargetRid = "win-x64",
                    SupportedRids = new[] { "win-x64", "linux-x64" },
                };

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                var result = task.Execute();
                result.Should().BeTrue();
                task.MatchingRid.Should().Be("win-x64");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pickrid-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pickrid-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                File.WriteAllText(Path.Combine(projectDir, "runtime.json"), RuntimeGraphJson);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpRid) = RunTask("runtime.json", projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtRid) = RunTask("runtime.json", projectDir);

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
                mpRid.Should().Be(mtRid,
                    "matching RID should be identical in both environments");
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
