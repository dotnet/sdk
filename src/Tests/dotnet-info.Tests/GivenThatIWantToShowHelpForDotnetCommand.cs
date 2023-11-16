// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Help;

namespace Microsoft.DotNet.Help.Tests
{
    public class GivenThatIWantToShowHelpForDotnetCommand : SdkTest
    {
        private const string HelpText =
@"Usage: ";

        public GivenThatIWantToShowHelpForDotnetCommand(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenHelpOptionIsPassedToDotnetItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenHelpCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenHelpCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--format", "text");

            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenInvalidCommandIsPassedToDotnetHelpItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--invalid");

            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(string.Format(LocalizableStrings.CommandDoesNotExist, "invalid"));
        }
    }
}
