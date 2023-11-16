// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Info;

namespace Microsoft.DotNet.Info.Tests
{
    public class GivenThatIWantToShowInfoForDotnetCommand : SdkTest
    {
        private const string InfoText =
@"Usage: ";

        public GivenThatIWantToShowInfoForDotnetCommand(ITestOutputInfoer log) : base(log)
        {
        }

        [Theory]
        [InlineData("--Info")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenInfoOptionIsPassedToDotnetItPrintsUsage(string InfoArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(InfoArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(InfoText);
        }

        [Fact]
        public void WhenInfoCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(InfoText);
        }

        [Fact]
        public void WhenInfoCommandIsPassedToDotnetItPrintsUsage()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--format", "text");

            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(InfoText);
        }

        [Fact]
        public void WhenInvalidCommandIsPassedToDotnetInfoItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "info")
                  .Execute("--invalid");

            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(string.Format(LocalizableStrings.CommandDoesNotExist, "invalid"));
        }
    }
}
