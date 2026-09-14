// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class TerminalLoggerDetectorTests
    {
        [TestMethod]
        [DataRow(new[] { "-tl" }, "tl", null)]
        [DataRow(new[] { "-tl:on" }, "tl", "on")]
        [DataRow(new[] { "--tl:off" }, "tl", "off")]
        [DataRow(new[] { "/tl:auto" }, "tl", "auto")]
        [DataRow(new[] { "-TL:OFF" }, "tl", "OFF")]
        [DataRow(new[] { "-terminallogger:on" }, "terminalLogger", "on")]
        [DataRow(new[] { "--TerminalLogger:auto" }, "terminalLogger", "auto")]
        [DataRow(new[] { "-ll:off" }, "ll", "off")]
        [DataRow(new[] { "--livelogger:on" }, "livelogger", "on")]
        public void TryFind_RecognizesTerminalLoggerSwitches(string[] tokens, string expectedName, string? expectedValue)
        {
            var result = TerminalLoggerDetector.TryFind(tokens, "tl", "terminalLogger", "ll", "livelogger");

            result.Should().NotBeNull();
            result!.Name.Should().Be(expectedName);
            result.Value.Should().Be(expectedValue);
        }

        [TestMethod]
        [DataRow("-tlp:default=true")]
        [DataRow("--tlp:default=auto")]
        [DataRow("/tlp:DISABLENODEDISPLAY")]
        [DataRow("-tlpwithnocolon")]
        [DataRow("-tlasm")]
        [DataRow("--llextra")]
        public void TryFind_DoesNotMatchTlpOrLookalikesWhenSearchingForTl(string token)
        {
            // Regression: before the fix, TryFind used StartsWith and would incorrectly match
            // "-tlp:default=true" (and other look-alikes that share a prefix with the searched
            // names) when searching for "tl", returning a Switch with a misleading name/value
            // that downstream callers interpreted as an invalid terminal logger argument.
            var result = TerminalLoggerDetector.TryFind([token], "tl", "terminalLogger", "ll", "livelogger");

            result.Should().BeNull();
        }

        [TestMethod]
        [DataRow(new[] { "-tlp:default=true" }, "tlp", "default=true")]
        [DataRow(new[] { "--terminalLoggerParameters:default=auto" }, "terminalLoggerParameters", "default=auto")]
        [DataRow(new[] { "/TLP:DISABLENODEDISPLAY" }, "tlp", "DISABLENODEDISPLAY")]
        public void TryFind_RecognizesTerminalLoggerParametersSwitches(string[] tokens, string expectedName, string? expectedValue)
        {
            var result = TerminalLoggerDetector.TryFind(tokens, "tlp", "terminalLoggerParameters");

            result.Should().NotBeNull();
            result!.Name.Should().Be(expectedName);
            result.Value.Should().Be(expectedValue);
        }

        [TestMethod]
        public void TryFind_PrefersLongFormOverShortFormWhenBothPresent()
        {
            // Iteration order checks "--" before "-" so the long form wins when both are present.
            var result = TerminalLoggerDetector.TryFind(["-tl:on", "--tl:off"], "tl", "terminalLogger", "ll", "livelogger");

            result.Should().NotBeNull();
            result!.Name.Should().Be("tl");
            result.Value.Should().Be("off");
        }

        [TestMethod]
        public void TryFind_ReturnsNullWhenNoMatch()
        {
            var result = TerminalLoggerDetector.TryFind(["--no-build", "-bl", "foo.csproj"], "tl", "terminalLogger");

            result.Should().BeNull();
        }
    }
}
