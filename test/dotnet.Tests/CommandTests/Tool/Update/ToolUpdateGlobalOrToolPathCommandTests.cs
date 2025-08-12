// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateGlobalOrToolPathCommandTests : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ToolPackageStoreMock _store;
        private readonly PackageId _packageId = new("global.tool.console.demo");
        private readonly PackageId _packageId2 = new("global.tool.console.demo.second.one");
        private readonly List<MockFeed> _mockFeeds;
        private const string LowerPackageVersion = "1.0.4";
        private const string HigherPackageVersion = "1.0.5";
        private const string HigherPreviewPackageVersion = "1.0.5-preview3";
        private readonly string _shimsDirectory;
        private readonly string _toolsDirectory;
        private readonly string _tempDirectory;
        private readonly ToolPackageDownloaderMock2 _toolPackageDownloader;

        public ToolUpdateGlobalOrToolPathCommandTests(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _tempDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _shimsDirectory = Path.Combine(_tempDirectory, "shims");
            _toolsDirectory = Path.Combine(_tempDirectory, "tools");
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
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId2.ToString(),
                            Version = LowerPackageVersion,
                            ToolCommandName = "SimulatorCommand2"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId2.ToString(),
                            Version = HigherPackageVersion,
                            ToolCommandName = "SimulatorCommand2"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId2.ToString(),
                            Version = HigherPreviewPackageVersion,
                            ToolCommandName = "SimulatorCommand2"
                        }
                    }
                }
            };

            _toolPackageDownloader = new ToolPackageDownloaderMock2(_store,
                runtimeJsonPathForTests: TestContext.GetRuntimeGraphFilePath(),
                currentWorkingDirectory: null,
                fileSystem: _fileSystem);

            foreach (var package in _mockFeeds.SelectMany(p => p.Packages))
            {
                _toolPackageDownloader.AddMockPackage(package);
            }
        }

        [Fact]
        public void WhenPassingRestoreActionConfigOptions()
        {
            var parseResult = Parser.Parse($"dotnet tool update -g {_packageId} --ignore-failed-sources");
            var toolUpdateCommand = new ToolUpdateGlobalOrToolPathCommand(parseResult);
            toolUpdateCommand._toolInstallGlobalOrToolPathCommand._restoreActionConfig.IgnoreFailedSources.Should().BeTrue();
        }

        [Fact]
        public void WhenPassingIgnoreFailedSourcesItShouldNotThrow()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_tempDirectory, "nuget.config"), _nugetConfigWithInvalidSources);

            var command = CreateUpdateCommand($"-g {_packageId} --ignore-failed-sources");

            command.Execute().Should().Be(0);
            _fileSystem.File.Delete(Path.Combine(_tempDirectory, "nuget.config"));
        }

        [Fact]
        public void GivenANonFeedExistentPackageItErrors()
        {
            var packageId = "does.not.exist";
            var command = CreateUpdateCommand($"-g {packageId}");

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                   string.Format(CliStrings.IsNotFoundInNuGetFeeds, packageId, MockNuGetPackageDownloader.MOCK_FEEDS_TEXT));
        }

        [Fact]
        public void GivenANonExistentPackageItInstallTheLatest()
        {
            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }


        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerVersionInstallationItCanUpdateAllThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            CreateInstallCommand($"-g {_packageId2} --version {LowerPackageVersion}", _packageId2.ToString()).Execute();

            CreateUpdateCommand($"--all -g -v:d").Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
            _store.EnumeratePackageVersions(_packageId2).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallFromRedirectorItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            ParseResult result = Parser.Parse("dotnet tool update " + $"-g {_packageId}");

            var toolUpdateGlobalOrToolPathCommand = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments, currentWorkingDirectory) => (_store, _store, _toolPackageDownloader, new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            var toolUpdateCommand = new ToolUpdateCommand(
                 result,
                 _reporter,
                 toolUpdateGlobalOrToolPathCommand,
                 new ToolUpdateLocalCommand(result));

            toolUpdateCommand.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenUpdateAllItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g --all --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedPreviewVersionInstallationWhenUpdateToHigherVersionItSucceeds()
        {
            var installCommand = CreateInstallCommand($"-g {_packageId} --version {HigherPreviewPackageVersion} --verbosity minimal");
            installCommand.Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {HigherPackageVersion} --verbosity minimal");
            command.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, HigherPreviewPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedHigherversionInstallationWhenUpdateToLowerVersionItErrors()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {LowerPackageVersion} --verbosity minimal");

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                  string.Format(CliCommandStrings.UpdateToLowerVersion, LowerPackageVersion, HigherPackageVersion));
        }

       [Fact]
        public void GivenAnExistedHigherversionInstallationWithDowngradeFlagWhenUpdateToLowerVersionItSucceeds()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {LowerPackageVersion} --verbosity minimal --allow-downgrade");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, HigherPackageVersion, LowerPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallWithWildCardVersionItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version 1.0.5-* --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallWithPrereleaseVersionItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --prerelease  --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolUpdateUpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedHigherVersionInstallationWhenCallWithLowerVersionItThrowsAndRollsBack()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {LowerPackageVersion}");

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(CliCommandStrings.UpdateToLowerVersion,
                        LowerPackageVersion,
                        HigherPackageVersion));

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolAlreadyInstalled,
                _packageId, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallWithPrereleaseItUsesAPrereleaseSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPreviewPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {HigherPreviewPackageVersion} --verbosity minimal");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                CliCommandStrings.ToolAlreadyInstalled,
                _packageId, HigherPreviewPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsIthasTheFirstLineIndicateUpdateFailure()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Parse("dotnet tool update " + $"-g {_packageId}");

            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments, currentWorkingDirectory) => (_store, _store,
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
                string.Format(CliCommandStrings.UpdateToolFailed, _packageId) + Environment.NewLine +
                string.Format(CliCommandStrings.InvalidToolConfiguration, "Simulated error"));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsItRollsBack()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Parse("dotnet tool update " + $"-g {_packageId}");

            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments, currentWorkingDirectory) => (_store, _store,
                    new ToolPackageDownloaderMock(
                        store: _store,
                        fileSystem: _fileSystem,
                        reporter: _reporter,
                        feeds: _mockFeeds,
                        downloadCallback:  () => throw new ToolConfigurationException("Simulated error")),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            Action a = () => command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(LowerPackageVersion);
        }

        [Fact]
        public void GivenPackagedShimIsProvidedWhenRunWithPackageIdItCreatesShimUsingPackagedShim()
        {

            Log.WriteLine($"Current RuntimeIdentifier: {RuntimeInformation.RuntimeIdentifier}");

            string toolTargetRuntimeIdentifier = OperatingSystem.IsWindows() ? "win-x64" : RuntimeInformation.RuntimeIdentifier;

            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            var tokenToIdentifyPackagedShim = "packagedShim";
            var mockPackage = _mockFeeds[0].Packages.Single(p => p.PackageId == _packageId.ToString() && p.Version == HigherPackageVersion);
            mockPackage.AdditionalFiles[$"tools/{ToolPackageDownloaderMock2.DefaultTargetFramework}/any/shims/{toolTargetRuntimeIdentifier}/{mockPackage.ToolCommandName}{extension}"] = tokenToIdentifyPackagedShim;

            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            string options = $"-g {_packageId}";
            ParseResult result = Parser.Parse("dotnet tool update " + options);

            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (_, _, _) => (_store, _store, _toolPackageDownloader, new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            command.Execute();

            _fileSystem.File.ReadAllText(ExpectedCommandPath()).Should().Be(tokenToIdentifyPackagedShim);

            string ExpectedCommandPath()
            {

                return Path.Combine(
                    _shimsDirectory,
                    "SimulatorCommand" + extension);
            }
        }

        private ToolInstallGlobalOrToolPathCommand CreateInstallCommand(string options, string packageId = null)
        {
            ParseResult result = Parser.Parse("dotnet tool install " + options);

            return new ToolInstallGlobalOrToolPathCommand(
                result,
                packageId is null ? _packageId : new PackageId(packageId) ,
                (location, forwardArguments, currentWorkingDirectory) => (_store, _store, _toolPackageDownloader, new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _environmentPathInstructionMock,
                _reporter);
        }

        private ToolUpdateGlobalOrToolPathCommand CreateUpdateCommand(string options)
        {
            ParseResult result = Parser.Parse("dotnet tool update " + options);

            return new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments, currentWorkingDirectory) => (_store, _store, _toolPackageDownloader, new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter,
                _store);
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

        private string _nugetConfigWithInvalidSources = @"{
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""invalid_source"" value=""https://api.nuget.org/v3/invalid.json"" />
  </packageSources>
</configuration>
}";
    }
}

