// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Tools.Info;

namespace Microsoft.DotNet.Info.Tests
{
    public class GivenThatIWantToShowInfoForDotnetCommand : SdkTest
    {
        private const string InfoTextRegex =
@"\.NET SDK:\s*(?:.*\n?)+?Runtime Environment:\s*(?:.*\n?)+?\.NET workloads installed:\s*(?:.*\n?)+?";

        public GivenThatIWantToShowInfoForDotnetCommand(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenInfoCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().MatchRegex(InfoTextRegex);
        }

        [Fact]
        public void WhenInfoCommandWithTextOptionIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--format", "text");

            cmd.Should().Pass();
            cmd.StdOut.Should().MatchRegex(InfoTextRegex);
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
