// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Info;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace Microsoft.DotNet.Info.Tests
{
    public class GivenThatIWantToShowInfoForDotnetCommand : SdkTest
    {
        private const string InfoTextRegex =
@"\.NET SDK:\s*(?:.*\n?)+?Runtime Environment:\s*(?:.*\n?)+?\.NET workloads installed:\s*(?:.*\n?)+?";
        private const string RuntimeEnv = "RuntimeEnv";
        private const string Sdk = "Sdk";
        private const string Workloads = "Workloads";

        public GivenThatIWantToShowInfoForDotnetCommand(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenInfoCommandIsPassedToDotnetItPrintsInfo()
        {
            var cmd = new DotnetCommand(Log, "info")
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().MatchRegex(InfoTextRegex);
        }

        [Fact]
        public void WhenInfoCommandWithTextOptionIsPassedToDotnetItPrintsInfo()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--format", "text");

            cmd.Should().Pass();
            cmd.StdOut.Should().MatchRegex(InfoTextRegex);
        }

        [Fact]
        public void WhenInfoCommandWithJsonOptionIsPassedToDotnetItPrintsJsonInfo()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--format", "json");

            cmd.Should().Pass();

            JToken parsedJson = JToken.Parse(cmd.StdOut);

            var expectedKeys = new List<string> { Sdk, RuntimeEnv, Workloads };
            foreach (var key in expectedKeys)
            {
                Assert.True(parsedJson[key] != null, $"Expected key '{key}' not found in JSON output.");
            }

            Assert.True(parsedJson[Sdk].Type == JTokenType.Object, "SDK should be an object.");
            Assert.True(parsedJson[RuntimeEnv].Type == JTokenType.Object, "RuntimeEnvironment should be an object.");
            Assert.True(parsedJson[Workloads].Type == JTokenType.Array, "Workloads should be an array.");
        }

        [Fact]
        public void WhenInvalidCommandIsPassedToDotnetInfoItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--invalid");

            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Unrecognized command or argument");
        }
    }
}
