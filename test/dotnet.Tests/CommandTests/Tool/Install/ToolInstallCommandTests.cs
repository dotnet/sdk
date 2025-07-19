// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallCommandTests : SdkTest
    {
        private const string PackageId = "global.tool.console.demo";

        public ToolInstallCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenRunWithBothGlobalAndToolPathShowErrorMessage()
        {
            var parseResult = Parser.Parse($"dotnet tool install -g --tool-path /tmp/folder {PackageId}");

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CliCommandStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                    "--global --tool-path"));
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/42346")]
        public void WhenRunWithRoot()
        {
            Directory.CreateDirectory("/tmp/folder/sub");
            var directory = Directory.GetCurrentDirectory();
            var ridGraphPath = TestContext.GetRuntimeGraphFilePath();
            try
            {
                Directory.SetCurrentDirectory("/tmp/folder");

                new DotnetNewCommand(Log, "tool-manifest").WithCustomHive("/tmp/folder").WithWorkingDirectory("/tmp/folder").Execute().Should().Pass();
                var parseResult = Parser.Parse("tool install dotnetsay");
                new ToolInstallLocalCommand(parseResult, runtimeJsonPathForTests: ridGraphPath).Execute().Should().Be(0);

                Directory.SetCurrentDirectory("/tmp/folder/sub");
                new DotnetNewCommand(Log, "tool-manifest").WithCustomHive("/tmp/folder/sub").WithWorkingDirectory("/tmp/folder/sub").Execute().Should().Pass();
                parseResult = Parser.Parse("tool install dotnetsay");
                new ToolInstallLocalCommand(parseResult, runtimeJsonPathForTests: ridGraphPath).Execute().Should().Be(0);

                new ToolRunCommand(Parser.Parse($"tool run dotnetsay")).Execute().Should().Be(0);
            }
            finally
            {
                Directory.SetCurrentDirectory(directory);
            }
        }

        [Fact]
        public void WhenRunWithBothGlobalAndLocalShowErrorMessage()
        {
            var parseResult = Parser.Parse(
                new[] { "dotnet", "tool", "install", "--local", "--tool-path", "/tmp/folder", PackageId });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(CliCommandStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath,
                        "--local --tool-path"));
        }

        [Fact]
        public void WhenRunWithGlobalAndToolManifestShowErrorMessage()
        {
            var parseResult = Parser.Parse(
                new[] { "dotnet", "tool", "install", "-g", "--tool-manifest", "folder/my-manifest.format", "PackageId" });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(CliCommandStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithToolPathAndToolManifestShowErrorMessage()
        {
            var parseResult = Parser.Parse(
                new[]
                {
                    "dotnet", "tool", "install", "--tool-path", "/tmp/folder", "--tool-manifest", "folder/my-manifest.format", PackageId
                });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(CliCommandStrings.OnlyLocalOptionSupportManifestFileOption);
        }

        [Fact]
        public void WhenRunWithLocalAndFrameworkShowErrorMessage()
        {
            var parseResult = Parser.Parse(
                new[]
                {
                    "dotnet", "tool", "install", PackageId, "--framework", ToolsetInfo.CurrentTargetFramework
                });

            var toolInstallCommand = new ToolInstallCommand(
                parseResult);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(CliCommandStrings.LocalOptionDoesNotSupportFrameworkOption);
        }
    }
}
