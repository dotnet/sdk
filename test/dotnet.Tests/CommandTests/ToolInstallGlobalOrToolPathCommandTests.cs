// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;
using Microsoft.NET.TestFramework;
using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallGlobalOrToolPathCommandTests: SdkTest
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly IToolPackageStoreQuery _toolPackageStoreQuery;
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndDownloader _createToolPackageStoresAndDownloader;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlaceShim;
        private readonly string _pathToPlacePackages;
        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private const string ToolCommandName = "SimulatorCommand";
        private readonly string UnlistedPackageId = "elemental.sysinfotool";

        public ToolInstallGlobalOrToolPathCommandTests(ITestOutputHelper log): base(log)
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _pathToPlaceShim = Path.Combine(_temporaryDirectory, "pathToPlace");
            _fileSystem.Directory.CreateDirectory(_pathToPlaceShim);
            _pathToPlacePackages = _pathToPlaceShim + "Packages";
            var toolPackageStoreMock = new ToolPackageStoreMock(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;
            _toolPackageStoreQuery = toolPackageStoreMock;
            _createShellShimRepository =
                (_, nonGlobalLocation) => new ShellShimRepository(
                    new DirectoryPath(_pathToPlaceShim),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem),
                    filePermissionSetter: new NoOpFilePermissionSetter());
            _environmentPathInstructionMock =
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim);
            _createToolPackageStoresAndDownloader = (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, CreateToolPackageDownloader());


            _parseResult = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --verbosity minimal");
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldCreateValidShim()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath()).Should().BeTrue();
            var deserializedFakeShim = JsonSerializer.Deserialize<AppHostShellShimMakerMock.FakeShim>(
                _fileSystem.File.ReadAllText(ExpectedCommandPath()));

            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunFromToolInstallRedirectCommandWithPackageIdItShouldCreateValidShim()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            var toolInstallCommand = new ToolInstallCommand(
                _parseResult,
                toolInstallGlobalOrToolPathCommand);

            toolInstallCommand.Execute().Should().Be(0);

            _fileSystem.File.Exists(ExpectedCommandPath()).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdWithSourceItShouldCreateValidShim()
        {
            const string sourcePath = "http://mysource.com";
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --add-source {sourcePath}");

            var toolToolPackageDownloader = CreateToolPackageDownloader(
            feeds: new List<MockFeed> {
                    new MockFeed
                    {
                        Type = MockFeedType.ImplicitAdditionalFeed,
                        Uri = sourcePath,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = PackageId,
                                Version = PackageVersion,
                                ToolCommandName = ToolCommandName,
                            }
                        }
                    }
            });

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolToolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            // It is hard to simulate shell behavior. Only Assert shim can point to executable dll
            _fileSystem.File.Exists(ExpectedCommandPath())
            .Should().BeTrue();
            var deserializedFakeShim =
                JsonSerializer.Deserialize<AppHostShellShimMakerMock.FakeShim>(
                    _fileSystem.File.ReadAllText(ExpectedCommandPath()));
            _fileSystem.File.Exists(deserializedFakeShim.ExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowPathInstruction()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void WhenRunWithPackageIdPackageFormatIsNotFullySupportedItShouldShowPathInstruction()
        {
            const string Warning = "WARNING";
            var injectedWarnings = new Dictionary<PackageId, IEnumerable<string>>()
            {
                [new PackageId(PackageId)] = new List<string>() { Warning }
            };

            var toolPackageDownloader = new ToolPackageDownloaderMock(
                 fileSystem: _fileSystem,
                store: _toolPackageStore,
                warningsMap: injectedWarnings);

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(Warning.Yellow());
            _reporter.Lines.Skip(1).First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void GivenFailedPackageInstallWhenRunWithPackageIdItShouldFail()
        {
            const string ErrorMessage = "Simulated error";

            var toolPackageDownloader =
                CreateToolPackageDownloader(
                    downloadCallback: () => throw new ToolPackageException(ErrorMessage));

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    ErrorMessage +
                    Environment.NewLine +
                    string.Format(LocalizableStrings.ToolInstallationFailedWithRestoreGuidance, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenCreateShimItShouldHaveNoBrokenFolderOnDisk()
        {
            _fileSystem.File.CreateEmptyFile(ExpectedCommandPath()); // Create conflict shim

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CommonLocalizableStrings.ShellShimConflict,
                    ToolCommandName));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenInCorrectToolConfigurationWhenRunWithPackageIdItShouldFail()
        {
            var toolPackageDownloader =
            CreateToolPackageDownloader(
                downloadCallback: () => throw new ToolConfigurationException("Simulated error"));

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.InvalidToolConfiguration,
                        "Simulated error") + Environment.NewLine +
                    string.Format(LocalizableStrings.ToolInstallationFailedContactAuthor, PackageId)
                );
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithPackageIdWithQuietItShouldShowNoSuccessMessage()
        {
            var parseResultQuiet = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --verbosity quiet");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResultQuiet,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .NotContain(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithInvalidVersionItShouldThrow()
        {
            const string invalidVersion = "!NotValidVersion!";
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version {invalidVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action action = () => toolInstallGlobalOrToolPathCommand.Execute();

            action
                .Should().Throw<GracefulException>()
                .WithMessage(string.Format(
                    LocalizableStrings.InvalidNuGetVersionRange,
                    invalidVersion));
        }

        [Fact]
        public void WhenRunWithExactVersionItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion} --verbosity minimal");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithValidVersionRangeItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version [1.0,2.0] --verbosity minimal");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithValidUnlistedVersionRangeItShouldSucceed()
        {
            const string nugetSourcePath = "https://api.nuget.org/v3/index.json";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            var toolInstallGlobalOrToolPathCommand = new DotnetCommand(Log, "tool", "install", "-g", UnlistedPackageId, "--version", "[0.5.0]", "--add-source", nugetSourcePath)
                .WithEnvironmentVariable("DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK", "true")
                .WithWorkingDirectory(testDir);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Pass();

            // Uninstall the unlisted package
            var toolUninstallCommand = new DotnetCommand(Log, "tool", "uninstall", "-g", UnlistedPackageId);
            toolUninstallCommand.Execute().Should().Pass();
        }

        [Fact]
        public void WhenRunWithValidBareVersionItShouldInterpretAsNuGetExactVersion()
        {
            const string nugetSourcePath = "https://api.nuget.org/v3/index.json";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            var toolInstallGlobalOrToolPathCommand = new DotnetCommand(Log, "tool", "install", "-g", UnlistedPackageId, "--version", "0.5.0", "--add-source", nugetSourcePath)
                .WithEnvironmentVariable("DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK", "true")
                .WithWorkingDirectory(testDir);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Pass();

            // Uninstall the unlisted package
            var toolUninstallCommand = new DotnetCommand(Log, "tool", "uninstall", "-g", UnlistedPackageId);
            toolUninstallCommand.Execute().Should().Pass();
        }

        [Fact]
        public void WhenRunWithoutValidVersionUnlistedToolItShouldThrow()
        {
            const string nugetSourcePath = "https://api.nuget.org/v3/index.json";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            var toolInstallGlobalOrToolPathCommand = new DotnetCommand(Log, "tool", "install", "-g", UnlistedPackageId, "--add-source", nugetSourcePath)
                .WithEnvironmentVariable("DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK", "true")
                .WithWorkingDirectory(testDir);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Fail();
        }

        [Fact]
        public void WhenRunWithPrereleaseItShouldSucceed()
        {
            IToolPackageDownloader toolToolPackageDownloader = GetToolToolPackageDownloaderWithPreviewInFeed();

            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --prerelease --verbosity minimal");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolToolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Contain(l => l == string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    "2.0.1-preview1").Green());
        }

        [Fact]
        public void WhenRunWithPrereleaseAndPackageVersionItShouldThrow()
        {
            IToolPackageDownloader toolToolPackageDownloader = GetToolToolPackageDownloaderWithPreviewInFeed();

            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version 2.0 --prerelease");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, toolToolPackageDownloader),
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();
            a.Should().Throw<GracefulException>();
        }

        private IToolPackageDownloader GetToolToolPackageDownloaderWithPreviewInFeed()
        {
            var toolToolPackageDownloader = CreateToolPackageDownloader(
                feeds: new List<MockFeed>
                {
                    new MockFeed
                    {
                        Type = MockFeedType.ImplicitAdditionalFeed,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = PackageId,
                                Version = "1.0.4",
                                ToolCommandName = "SimulatorCommand"
                            },
                            new MockFeedPackage
                            {
                                PackageId = PackageId,
                                Version = "2.0.1-preview1",
                                ToolCommandName = "SimulatorCommand"
                            }
                        }
                    }
                });
            return toolToolPackageDownloader;
        }

        [Fact]
        public void WhenRunWithoutAMatchingRangeItShouldFail()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version [5.0,10.0]");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    LocalizableStrings.ToolInstallationRestoreFailed +
                    Environment.NewLine + string.Format(LocalizableStrings.ToolInstallationFailedWithRestoreGuidance, PackageId));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void WhenRunWithValidVersionWildcardItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install -g {PackageId} --version 1.0.* --verbosity minimal");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    LocalizableStrings.InstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithPackageIdAndBinPathItShouldNoteHaveEnvironmentPathInstruction()
        {
            var result = Parser.Instance.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().NotContain(l => l.Contains(EnvironmentPathInstructionMock.MockInstructionText));
        }

        [Fact]
        public void AndPackagedShimIsProvidedWhenRunWithPackageIdItCreateShimUsingPackagedShim()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            var prepackagedShimPath = Path.Combine(_temporaryDirectory, ToolCommandName + extension);
            var tokenToIdentifyPackagedShim = "packagedShim";
            _fileSystem.File.WriteAllText(prepackagedShimPath, tokenToIdentifyPackagedShim);

            var result = Parser.Instance.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");

            var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
            {
                [new PackageId(PackageId)] = new[] { new FilePath(prepackagedShimPath) }
            };

            var installCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_toolPackageStore, _toolPackageStoreQuery, new ToolPackageDownloaderMock(
                    fileSystem: _fileSystem,
                    store: _toolPackageStore,
                    packagedShimsMap: packagedShimsMap,
                    reporter: _reporter)),
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim),
                _reporter);

            installCommand.Execute().Should().Be(0);

            _fileSystem.File.ReadAllText(ExpectedCommandPath()).Should().Be(tokenToIdentifyPackagedShim);
        }


        [Fact]
        public void WhenRunWithArchOptionItErrorsOnInvalidRids()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse($"dotnet tool install -g {PackageId} -a invalid");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            var exceptionThrown = Assert.Throws<AggregateException>(() => toolInstallGlobalOrToolPathCommand.Execute());
            exceptionThrown.Message.Should().Contain("-invalid is invalid");
        }

        [WindowsOnlyFact]
        public void WhenRunWithArchOptionItDownloadsAppHostTemplate()
        {
            var nugetPackageDownloader = new MockNuGetPackageDownloader();
            var parseResult = Parser.Instance.Parse($"dotnet tool install -g {PackageId} -a arm64");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResult,
                _createToolPackageStoresAndDownloader,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter,
                nugetPackageDownloader);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);
            nugetPackageDownloader.DownloadCallParams.Count.Should().Be(1);
            nugetPackageDownloader.ExtractCallParams.Count.Should().Be(1);
            nugetPackageDownloader.DownloadCallParams.First().Item1.Should().Be(new PackageId("microsoft.netcore.app.host.win-arm64"));
        }

        private IToolPackageDownloader CreateToolPackageDownloader(
            List<MockFeed> feeds = null,
            Action downloadCallback = null)
        {
            return new ToolPackageDownloaderMock(fileSystem: _fileSystem,
                store: _toolPackageStore,
                reporter: _reporter,
                feeds: feeds,
                downloadCallback: downloadCallback);
        }

        private string ExpectedCommandPath()
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            return Path.Combine(
                _pathToPlaceShim,
                ToolCommandName + extension);
        }

        internal class NoOpFilePermissionSetter : IFilePermissionSetter
        {
            public void SetUserExecutionPermission(string path)
            {
            }

            public void SetPermission(string path, string chmodArgument)
            {
            }
        }
    }
}



