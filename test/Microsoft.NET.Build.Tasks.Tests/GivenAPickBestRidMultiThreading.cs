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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void EmptyRuntimeGraphPathLogsMissingFileError(string? runtimeGraphPath)
        {
            var mockEngine = new MockBuildEngine();
            var task = new PickBestRid
            {
                BuildEngine = mockEngine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                RuntimeGraphPath = runtimeGraphPath!,
                TargetRid = "win-x64",
                SupportedRids = new[] { "any", "win" }
            };

            task.Execute().Should().BeFalse();
            task.MatchingRid.Should().BeNull();

            mockEngine.Errors.Should().HaveCount(1);
            mockEngine.Errors[0].Message.Should().Contain("does not exist");
        }
    }
}
