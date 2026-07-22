// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests
{
    [TestClass]
    public class LoggerUtilityTests
    {
        [TestMethod]
        [DataRow("-tl", "-tl:auto")]
        [DataRow("--tl", "--tl:auto")]
        [DataRow("/tl", "/tl:auto")]
        [DataRow("-tl:off", "-tl:off")]
        [DataRow("-TL:off", "-TL:off")]
        [DataRow("-TL:Off", "-TL:Off")]
        [DataRow("--tl:off", "--tl:off")]
        [DataRow("/tl:on", "/tl:on")]
        [DataRow("/tl:off", "/tl:off")]
        [DataRow("-terminallogger", "-terminallogger:auto")]
        [DataRow("--terminalLogger", "--terminalLogger:auto")]
        [DataRow("/terminallogger", "/terminallogger:auto")]
        [DataRow("-terminallogger:auto", "-terminallogger:auto")]
        [DataRow("--TerminalLogger:on", "--TerminalLogger:on")]
        [DataRow("--terminalLogger:off", "--terminalLogger:off")]
        [DataRow("-ll", "-ll:auto")]
        [DataRow("--ll:off", "--ll:off")]
        [DataRow("/ll", "/ll:auto")]
        [DataRow("-livelogger", "-livelogger:auto")]
        [DataRow("--livelogger:off", "--livelogger:off")]
        [DataRow("-tlp:default=true", "-tlp:default=true")]
        [DataRow("--tlp:default=auto", "--tlp:default=auto")]
        [DataRow("-tlp:verbosity=quiet", "-tlp:verbosity=quiet")]
        [DataRow("/tlp:DISABLENODEDISPLAY", "/tlp:DISABLENODEDISPLAY")]
        [DataRow("-terminalloggerparameters:default=true", "-terminalloggerparameters:default=true")]
        [DataRow("--terminalLoggerParameters:default=true", "--terminalLoggerParameters:default=true")]
        [DataRow("--terminalLoggerParameters:verbosity=quiet", "--terminalLoggerParameters:verbosity=quiet")]
        [DataRow("-clp:NoSummary", "-clp:NoSummary")]
        [DataRow("--consoleLoggerParameters:NoSummary", "--consoleLoggerParameters:NoSummary")]
        [DataRow("-noconsolelogger", "-noconsolelogger")]
        [DataRow("-noConsoleLogger", "-noConsoleLogger")]
        [DataRow("/noconsolelogger", "/noconsolelogger")]
        public void LoggerArgument_ArgumentForms(string arg, string expectedArg)
        {
            LoggerUtility.SeparateLoggerArguments([arg], out var loggerArgs, out var nonLoggerArgs);

            loggerArgs.Should().Equal(expectedArg);
            nonLoggerArgs.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("-tl:invalid")]
        [DataRow("-tlp")]
        [DataRow("-clp")]
        [DataRow("-noconsolelogger:false")]
        [DataRow("--noconsolelogger")]
        [DataRow("--unknownLogger:off")]
        [DataRow("--no-build")]
        [DataRow("-tlapropertythatstartslikethis")]
        [DataRow("--tlpwithnocolon")]
        [DataRow("--terminallogger-something")]
        [DataRow("-llextra")]
        [DataRow("foo.csproj")]
        [DataRow("")]
        public void LoggerArgument_InvalidFormsAreNotRecognized(string arg)
        {
            LoggerUtility.SeparateLoggerArguments([arg], out var loggerArgs, out var nonLoggerArgs);

            loggerArgs.Should().BeEmpty();
            nonLoggerArgs.Should().Equal(arg);
        }

        [TestMethod]
        [DataRow("--tl:off")]
        [DataRow("--terminalLogger:auto")]
        [DataRow("--tlp:default=true")]
        [DataRow("/tl:off")]
        [DataRow("/terminalLogger:auto")]
        [DataRow("/tlp:default=true")]
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

        [TestMethod]
        public void GetBuildOptions_LeavesUnknownArgumentsAsTestApplicationArguments()
        {
            var mtpCommand = new TestCommandDefinition.MicrosoftTestingPlatform();
            var parseResult = mtpCommand.Parse(["--no-build", "--my-test-arg"]);

            var buildOptions = MSBuildUtility.GetBuildOptions(parseResult);

            buildOptions.TestApplicationArguments.Should().Contain("--my-test-arg");
            buildOptions.MSBuildArgs.Should().NotContain("--my-test-arg");
        }

        [TestMethod]
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
