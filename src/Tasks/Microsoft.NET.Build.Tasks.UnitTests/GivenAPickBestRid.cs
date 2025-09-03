// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAPickBestRid
    {
        private const string RuntimeGraphContent = @"{
            ""runtimes"": {
                ""any"": {
                    ""#import"": [""base""]
                },
                ""base"": {
                    ""#import"": []
                },
                ""win"": {
                    ""#import"": [""any""]
                },
                ""win-x64"": {
                    ""#import"": [""win""]
                },
                ""win-x86"": {
                    ""#import"": [""win""]
                },
                ""linux"": {
                    ""#import"": [""any""]
                },
                ""linux-x64"": {
                    ""#import"": [""linux""]
                },
                ""osx"": {
                    ""#import"": [""any""]
                },
                ""osx-x64"": {
                    ""#import"": [""osx""]
                }
            }
        }";

        [Fact]
        public void ItPicksBestMatchingRid()
        {
            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);

            try
            {
                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = runtimeGraphPath,
                    CurrentRid = "win-x64",
                    SupportedRids = new[] { "any", "win", "win-x64" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().Be("win-x64");
            }
            finally
            {
                File.Delete(runtimeGraphPath);
            }
        }

        [Fact]
        public void ItPicksBestMatchingRidFallback()
        {
            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);

            try
            {
                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = runtimeGraphPath,
                    CurrentRid = "win-x64",
                    SupportedRids = new[] { "any", "win" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().Be("win");
            }
            finally
            {
                File.Delete(runtimeGraphPath);
            }
        }

        [Fact]
        public void ItPicksBestMatchingRidAnyFallback()
        {
            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);

            try
            {
                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = runtimeGraphPath,
                    CurrentRid = "win-x64",
                    SupportedRids = new[] { "any" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().Be("any");
            }
            finally
            {
                File.Delete(runtimeGraphPath);
            }
        }

        [Fact]
        public void ItHandlesNonExistentRuntimeGraphFile()
        {
            var task = new PickBestRid
            {
                BuildEngine = new MockBuildEngine(),
                RuntimeGraphPath = "non-existent-file.json",
                CurrentRid = "win-x64",
                SupportedRids = new[] { "any" }
            };

            task.Execute().Should().BeFalse();
            task.MatchingRid.Should().BeNull();

            var buildEngine = (MockBuildEngine)task.BuildEngine;
            buildEngine.Errors.Should().HaveCount(1);
            buildEngine.Errors[0].Message.Should().Contain("non-existent-file.json");
            buildEngine.Errors[0].Message.Should().Contain("does not exist");
        }

        [Fact]
        public void ItHandlesUnknownCurrentRid()
        {
            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);

            try
            {
                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = runtimeGraphPath,
                    CurrentRid = "unknown-rid",
                    SupportedRids = new[] { "any", "win" }
                };

                task.Execute().Should().BeFalse();
                task.MatchingRid.Should().BeNull();

                var buildEngine = (MockBuildEngine)task.BuildEngine;
                buildEngine.Errors.Should().HaveCount(1);
                buildEngine.Errors[0].Message.Should().Contain("unknown-rid");
                buildEngine.Errors[0].Message.Should().Contain("Unable to find a matching RID");
            }
            finally
            {
                File.Delete(runtimeGraphPath);
            }
        }

        [Fact]
        public void ItHandlesNoSupportedRidsMatch()
        {
            var runtimeGraphPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);

            try
            {
                var task = new PickBestRid
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimeGraphPath = runtimeGraphPath,
                    CurrentRid = "win-x64",
                    SupportedRids = new[] { "linux", "osx" }
                };

                task.Execute().Should().BeTrue();
                task.MatchingRid.Should().BeNull();
            }
            finally
            {
                File.Delete(runtimeGraphPath);
            }
        }
    }
}