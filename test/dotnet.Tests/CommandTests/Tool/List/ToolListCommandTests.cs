// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolListCommandTests
    {
        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool list -g --tool-path /test/path");

            var toolInstallCommand = new ToolListCommand(
                result);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CliCommandStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool list --local --tool-path /test/path");

            var toolInstallCommand = new ToolListCommand(
                result);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(CliCommandStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }
    }
}
