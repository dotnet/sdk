// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.DotNet.Tools.Tool.Install;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using InstallLocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateAllCommandTests
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;

        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private readonly string _shimsDirectory;
        private readonly string _toolsDirectory;


        public ToolUpdateAllCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            var tempDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _shimsDirectory = Path.Combine(tempDirectory, "shims");
            _toolsDirectory = Path.Combine(tempDirectory, "tools");
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, _shimsDirectory);
        }

        [Fact]
        public void UpdatePackagesSuccessfully()
        {
            CreateInstallCommand($"-g {PackageId} --verbosity minimal --version {PackageVersion}")
                .Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    InstallLocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.DefaultToolCommandName,
                    PackageId,
                    PackageVersion));

            var packageDirectory = new DirectoryPath(Path.GetFullPath(_toolsDirectory))
                .WithSubDirectories(PackageId, PackageVersion);
            var shimPath = Path.Combine(
                _shimsDirectory,
                ProjectRestorerMock.DefaultToolCommandName +
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();

            // _reporter.Lines.Clear();

            var result = Parser.Instance.Parse($"dotnet tool update -g {PackageId} -v:d");
            var toolUpdateCommand = new ToolUpdateCommand(result, _reporter);
            Action a = () => toolUpdateCommand.Execute();

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    InstallLocalizableStrings.InstallationSucceeded,
                    ProjectRestorerMock.DefaultToolCommandName,
                    PackageId,
                    PackageVersion));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();
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

        private ToolInstallGlobalOrToolPathCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool install " + options);

            var store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);

            var packageDownloaderMock = new ToolPackageDownloaderMock(
                    store: store,
                    fileSystem: _fileSystem,
                    _reporter
                    );
            var toolPackageDownloaderMock = new ToolPackageUninstallerMock(_fileSystem, store);

            return new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (store, store, packageDownloaderMock, toolPackageDownloaderMock),
                (_, _) => new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem)),
                _environmentPathInstructionMock,
                _reporter);
        }
    }
}
