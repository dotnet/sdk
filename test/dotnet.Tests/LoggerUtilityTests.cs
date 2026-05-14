// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests
{
    public class LoggerUtilityTests
    {
        [Theory]
        [InlineData("-tl")]
        [InlineData("--tl")]
        [InlineData("/tl")]
        [InlineData("-tl:off")]
        [InlineData("--tl:off")]
        [InlineData("/tl:on")]
        [InlineData("-TL:Off")]
        [InlineData("-terminallogger")]
        [InlineData("--terminalLogger")]
        [InlineData("/terminallogger")]
        [InlineData("-terminallogger:auto")]
        [InlineData("--TerminalLogger:on")]
        [InlineData("-ll")]
        [InlineData("--ll:off")]
        [InlineData("/ll")]
        [InlineData("-livelogger")]
        [InlineData("--livelogger:off")]
        [InlineData("-tlp:default=true")]
        [InlineData("--tlp:default=auto")]
        [InlineData("/tlp:DISABLENODEDISPLAY")]
        [InlineData("-terminalloggerparameters:default=true")]
        [InlineData("--terminalLoggerParameters:default=true")]
        public void IsTerminalLoggerArgument_RecognizesTerminalLoggerArguments(string arg)
        {
            LoggerUtility.IsTerminalLoggerArgument(arg).Should().BeTrue();
        }

        [Theory]
        [InlineData("--no-build")]
        [InlineData("-bl")]
        [InlineData("--binaryLogger")]
        [InlineData("-bl:foo.binlog")]
        [InlineData("-tlapropertythatstartslikethis")]
        [InlineData("--tlpwithnocolon")]
        [InlineData("--terminallogger-something")]
        [InlineData("-llextra")]
        [InlineData("foo.csproj")]
        [InlineData("")]
        public void IsTerminalLoggerArgument_RejectsNonTerminalLoggerArguments(string arg)
        {
            LoggerUtility.IsTerminalLoggerArgument(arg).Should().BeFalse();
        }

        [Theory]
        [InlineData("--tl:off")]
        [InlineData("--terminalLogger:auto")]
        [InlineData("--tlp:default=true")]
        [InlineData("/tl:off")]
        [InlineData("/terminalLogger:auto")]
        [InlineData("/tlp:default=true")]
        public void GetBuildOptions_ForwardsTerminalLoggerArgsToMSBuild_NotToTestApplication(string terminalLoggerArg)
        {
            // Parse a `dotnet test` command line that includes a terminal logger argument
            // against the MTP test command definition. The argument is unknown to the parser
            // and lands in UnmatchedTokens.
            var mtpCommand = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = mtpCommand.Parse(["--no-build", terminalLoggerArg]);

            var buildOptions = MSBuildUtility.GetBuildOptions(parseResult);

            buildOptions.MSBuildArgs.Should().Contain(terminalLoggerArg,
                "terminal logger arguments must be forwarded to the underlying MSBuild build invocation (https://github.com/dotnet/sdk/issues/52229).");
            buildOptions.TestApplicationArguments.Should().NotContain(terminalLoggerArg,
                "terminal logger arguments must not be passed to the test application, which doesn't recognize them.");
        }

        [Fact]
        public void GetBuildOptions_LeavesUnknownArgumentsAsTestApplicationArguments()
        {
            var mtpCommand = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = mtpCommand.Parse(["--no-build", "--my-test-arg"]);

            var buildOptions = MSBuildUtility.GetBuildOptions(parseResult);

            buildOptions.TestApplicationArguments.Should().Contain("--my-test-arg");
            buildOptions.MSBuildArgs.Should().NotContain("--my-test-arg");
        }

        [Fact]
        public void GetBuildOptions_ExtractsTerminalLoggerArgs_BeforePositionalArgumentDetection()
        {
            // Regression test: verify that interspersing terminal logger args with positional arguments
            // (project paths, test modules) and test application arguments doesn't break positional detection
            // or argument routing. See https://github.com/dotnet/sdk/issues/52229.
            var mtpCommand = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = mtpCommand.Parse(["--no-build", "--tl:off", "--my-test-arg", "--tlp:default=true"]);

            var buildOptions = MSBuildUtility.GetBuildOptions(parseResult);

            buildOptions.MSBuildArgs.Should().Contain("--tl:off").And.Contain("--tlp:default=true");
            buildOptions.MSBuildArgs.Should().NotContain("--my-test-arg");
            buildOptions.TestApplicationArguments.Should().Contain("--my-test-arg");
            buildOptions.TestApplicationArguments.Should().NotContain("--tl:off").And.NotContain("--tlp:default=true");
        }
    }
}
