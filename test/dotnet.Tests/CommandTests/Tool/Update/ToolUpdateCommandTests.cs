// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateCommandTests
    {
        private readonly BufferedReporter _reporter;
        private const string PackageId = "global.tool.console.demo";


        public ToolUpdateCommandTests()
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool update -g --tool-path /tmp/folder {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CliCommandStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool update --local --tool-path /tmp/folder {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(CliCommandStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Parse($"dotnet tool update -g --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(CliCommandStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var result =
                Parser.Parse(
                    $"dotnet tool update --tool-path /tmp/folder --tool-manifest folder/my-manifest.format {PackageId}");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(CliCommandStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithAllAndVersionShowErrorMessage()
        {
            var result =
                Parser.Parse(
                    $"dotnet tool update --all --version 1.0.0");

            var toolUpdateCommand = new ToolUpdateCommand(
                result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(
                        CliCommandStrings.UpdateToolCommandInvalidAllAndVersion, "--all --version")
                );
        }

        [Fact]
        public void WhenRunWithoutAllOrPackageIdShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool update");

            var toolUpdateCommand = new ToolUpdateCommand(result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    CliCommandStrings.UpdateToolCommandInvalidAllAndPackageId
                );
        }

        [Fact]
        public void WhenRunWithBothAllAndPackageIdShowErrorMessage()
        {
            var result = Parser.Parse($"dotnet tool update packageId --all");

            var toolUpdateCommand = new ToolUpdateCommand(result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    CliCommandStrings.UpdateToolCommandInvalidAllAndPackageId
                );
        }
    }
}
