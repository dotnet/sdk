// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAPickBestRidMultiThreading
    {
        private const string RuntimeGraphContent = @"{
            ""runtimes"": {
                ""any"": { ""#import"": [""base""] },
                ""base"": { ""#import"": [] },
                ""win"": { ""#import"": [""any""] },
                ""win-x64"": { ""#import"": [""win""] }
            }
        }";

        // Test 1: Proves the migrated task resolves a relative RuntimeGraphPath against the
        // TaskEnvironment's project directory rather than the process current working directory.
        [Fact]
        public void ItResolvesRelativeRuntimeGraphPathAgainstProjectDirectory()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "pickbestrid-relpath-" + Guid.NewGuid().ToString("N"));
            var decoyCwd = Path.Combine(Path.GetTempPath(), "pickbestrid-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectDir, "sub"));
            Directory.CreateDirectory(decoyCwd);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var relativePath = Path.Combine("sub", "runtime.json");
                File.WriteAllText(Path.Combine(projectDir, relativePath), RuntimeGraphContent);

                // Set CWD to a directory that does NOT contain the runtime graph file.
                // If the task incorrectly used CWD, File.Exists would fail.
                Directory.SetCurrentDirectory(decoyCwd);

                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    RuntimeGraphPath = relativePath,
                    TargetRid = "win-x64",
                    SupportedRids = new[] { "any", "win", "win-x64" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().Be("win-x64");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(decoyCwd)) Directory.Delete(decoyCwd, true);
            }
        }

        // Test 3: Guards against Sin 1 (output property contamination). MatchingRid must contain
        // only the RID string, never the absolutized path that the task computes internally.
        [Fact]
        public void MatchingRidOutputContainsOnlyRidNoPathPrefix()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "pickbestrid-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectDir, "sub"));
            try
            {
                var relativePath = Path.Combine("sub", "runtime.json");
                File.WriteAllText(Path.Combine(projectDir, relativePath), RuntimeGraphContent);

                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    RuntimeGraphPath = relativePath,
                    TargetRid = "win-x64",
                    SupportedRids = new[] { "any", "win", "win-x64" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().Be("win-x64");
                task.MatchingRid.Should().NotContain(projectDir, "the absolutized path must not leak into the RID output");
                task.MatchingRid.Should().NotContain(Path.DirectorySeparatorChar.ToString(), "a RID is never a path");
            }
            finally
            {
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
            }
        }

        // Test 4: Guards against Sin 2 (error message path inflation). The logged error for a
        // missing graph file must keep the user's original (relative) RuntimeGraphPath, not the
        // absolutized form computed by TaskEnvironment.GetAbsolutePath.
        [Fact]
        public void MissingFileErrorContainsOriginalRelativePathNotAbsolutized()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "pickbestrid-err-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var relativePath = "definitely-missing.json";

                var mockEngine = new MockBuildEngine();
                var task = new PickBestRid
                {
                    BuildEngine = mockEngine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    RuntimeGraphPath = relativePath,
                    TargetRid = "win-x64",
                    SupportedRids = new[] { "any", "win" }
                };

                task.Execute().Should().BeFalse();
                task.MatchingRid.Should().BeNull();

                mockEngine.Errors.Should().HaveCount(1);
                var message = mockEngine.Errors[0].Message;
                message.Should().Contain(relativePath, "the original user-supplied path must appear in the error");
                message.Should().NotContain(projectDir, "the absolutized path must not leak into the error message");
            }
            finally
            {
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
            }
        }

        // Test 6: Behavior parity across the two TaskEnvironment execution models.
        //   Multi-process mode  -> TaskEnvironment.Fallback (reads live CWD)
        //   Multi-threaded mode -> CreateWithProjectDirectoryAndEnvironment(projectDir)
        // Both must produce byte-identical outcome for the success path.
        [Fact]
        public void ItProducesIdenticalOutcomeInFallbackAndIsolatedModes_Success()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "pickbestrid-parity-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "pickbestrid-parity-other-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectDir, "sub"));
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var relativePath = Path.Combine("sub", "runtime.json");
                File.WriteAllText(Path.Combine(projectDir, relativePath), RuntimeGraphContent);

                // Multi-process mode: CWD == projectDir, Fallback reads live CWD.
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpRid, mpErrors) = RunPickBestRid(relativePath, TaskEnvironment.Fallback);

                // Multi-threaded mode: CWD == otherDir, isolated TaskEnvironment carries projectDir.
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtRid, mtErrors) = RunPickBestRid(
                    relativePath,
                    TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir));

                mpResult.Should().BeTrue();
                mtResult.Should().Be(mpResult, "Execute() return value must be identical across modes");
                mtRid.Should().Be(mpRid, "MatchingRid must be identical across modes");
                mtErrors.Should().Be(mpErrors, "error count must be identical across modes");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        private static (bool result, string? matchingRid, int errorCount) RunPickBestRid(
            string runtimeGraphPath, TaskEnvironment taskEnvironment)
        {
            var mockEngine = new MockBuildEngine();
            var task = new PickBestRid
            {
                BuildEngine = mockEngine,
                TaskEnvironment = taskEnvironment,
                RuntimeGraphPath = runtimeGraphPath,
                TargetRid = "win-x64",
                SupportedRids = new[] { "any", "win", "win-x64" }
            };
            var result = task.Execute();
            return (result, task.MatchingRid, mockEngine.Errors.Count);
        }
    }
}
