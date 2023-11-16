// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Info.Tests
{
    public class GivenThatIWantToShowInfoForDotnetCommand : SdkTest
    {
        private const string InfoText =
@"Description:
  .NET CLI Info utility

Usage:
  dotnet Info [<COMMAND_NAME>] [options]

Arguments:
  <COMMAND_NAME>  The SDK command to launch online Info for.

Options:
  -?, -h, --Info  Show command line Info.";

        public GivenThatIWantToShowInfoForDotnetInfoCommand(ITestOutputInfoer log) : base(log)
        {
        }

        [Theory]
        [InlineData("--Info")]
        [InlineData("-h")]
        [InlineData("-?")]
        [InlineData("/?")]
        public void WhenInfoOptionIsPassedToDotnetInfoCommandItPrintsUsage(string InfoArg)
        {
            var cmd = new DotnetCommand(Log, "Info")
                .Execute($"{InfoArg}");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(InfoText);
        }
    }
}
