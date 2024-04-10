/*// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateAllGlobalOrToolPathCommandTests
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ToolPackageStoreMock _store;

        private readonly PackageId _packageId = new PackageId("global.tool.console.demo");
        private readonly PackageId _packageId2 = new PackageId("global.tool.console.demo2");
        private readonly List<MockFeed> _mockFeeds;
        private const string LowerPackageVersion = "1.0.4";
        private const string HigherPackageVersion = "1.0.5";
        private readonly string _shimsDirectory;
        private readonly string _toolsDirectory;

        public ToolUpdateAllGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            var tempDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _shimsDirectory = Path.Combine(tempDirectory, "shims");
            _toolsDirectory = Path.Combine(tempDirectory, "tools");
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, _shimsDirectory);
            _store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);
            _mockFeeds = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.FeedFromGlobalNugetConfig,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = LowerPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = HigherPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = HigherPreviewPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        [Fact]
        public void GivenAnExistedLowerVersionInstallationItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            CreateUpdateCommand($"-g -v:d").Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerVersionInstallationWhenCallWithPrereleaseVersionItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g --prerelease  --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceededStableVersionNoChange,
                _packageId, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerVersionWhenReinstallThrowsIthasTheFirstLineIndicateUpdateFailure()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");

            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store,
                    new ToolPackageDownloaderMock(
                        store: _store,
                        fileSystem: _fileSystem,
                        reporter: _reporter,
                        feeds: _mockFeeds,
                        downloadCallback: () => throw new ToolConfigurationException("Simulated error")),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            Action a = () => command.Execute();
            a.Should().Throw<GracefulException>().And.Message.Should().Contain(
                string.Format(LocalizableStrings.UpdateToolFailed, _packageId) + Environment.NewLine +
                string.Format(Tools.Tool.Install.LocalizableStrings.InvalidToolConfiguration, "Simulated error"));
        }

        private ToolInstallGlobalOrToolPathCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool install " + options);
            var store = new ToolPackageStoreMock(
                    new DirectoryPath(_toolsDirectory),
                    _fileSystem);

            return new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store, new ToolPackageDownloaderMock(
                    store: _store,
                    fileSystem: _fileSystem,
                    _reporter,
                    _mockFeeds
                    ), new ToolPackageUninstallerMock(_fileSystem, store)),
                (_, _) => GetMockedShellShimRepository(),
                _environmentPathInstructionMock,
                _reporter);
        }

        private ToolUpdateAllCommand CreateUpdateCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool update --all " + options);

            return new ToolUpdateAllCommand(
                result,
                (location, forwardArguments) => (_store, _store, new ToolPackageDownloaderMock(
                    store: _store,
                    fileSystem: _fileSystem,
                    _reporter,
                    _mockFeeds
                    ),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter,
                toolPath => { return _store; });
        }

        private ShellShimRepository GetMockedShellShimRepository()
        {
            return new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem),
                    filePermissionSetter: new ToolInstallGlobalOrToolPathCommandTests.NoOpFilePermissionSetter());
        }
    }
}
*/
