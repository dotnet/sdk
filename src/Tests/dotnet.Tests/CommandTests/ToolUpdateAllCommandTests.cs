// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Update;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateAllCommandTests
    {
        private readonly BufferedReporter _reporter;


        public ToolUpdateAllCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool update --all -g --tool-path /tmp/folder");

            var ToolUpdateAllCommand = new ToolUpdateAllCommand(
                result);

            Action a = () => ToolUpdateAllCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Instance.Parse($"dotnet tool update --all --local --tool-path /tmp/folder");

            var ToolUpdateAllCommand = new ToolUpdateAllCommand(
                result);

            Action a = () => ToolUpdateAllCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessageInUpdateAll()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool update --all --global --tool-path /tmp/folder");

            var ToolUpdateAllCommand = new ToolUpdateAllCommand(
                result);

            Action a = () => ToolUpdateAllCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--global --tool-path"));
        }


        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse($"dotnet tool update --all -g --tool-manifest folder/my-manifest.format");

            var ToolUpdateAllCommand = new ToolUpdateAllCommand(
                result);

            Action a = () => ToolUpdateAllCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Instance.Parse(
                    $"dotnet tool update --all --tool-path /tmp/folder --tool-manifest folder/my-manifest.format");

            var ToolUpdateAllCommand = new ToolUpdateAllCommand(
                result);

            Action a = () => ToolUpdateAllCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Tools.Tool.Common.LocalizableStrings.OnlyLocalOptionSupportManifestFileOption);
        }
    }
}
