// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using CreateShellShimRepository = Microsoft.DotNet.Cli.Commands.Tool.Install.CreateShellShimRepository;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallGlobalOrToolPathCommandTests: SdkTest
    {
        private readonly PackageId _packageId;
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly IToolPackageStoreQuery _toolPackageStoreQuery;
        private readonly ToolPackageUninstallerMock _toolPackageUninstallerMock;
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
        private readonly ToolPackageDownloaderMock2 _toolPackageDownloader;
        private readonly string _toolsDirectory;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlaceShim;
        private readonly string _pathToPlacePackages;
        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private const string HigherPackageVersion = "2.0.0";
        private const string LowerPackageVersion = "1.0.0";
        private const string ToolCommandName = "SimulatorCommand";
        private readonly string UnlistedPackageId = "elemental.sysinfotool";

        public ToolInstallGlobalOrToolPathCommandTests(ITestOutputHelper log): base(log)
        {
            _packageId = new PackageId(PackageId);
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _toolsDirectory = Path.Combine(_temporaryDirectory, "tools");
            _pathToPlaceShim = Path.Combine(_temporaryDirectory, "pathToPlace");
            _fileSystem.Directory.CreateDirectory(_pathToPlaceShim);
            _pathToPlacePackages = _pathToPlaceShim + "Packages";
            var toolPackageStoreMock = new ToolPackageStoreAndQuery(new DirectoryPath(_pathToPlacePackages), _fileSystem);
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
            var store = new ToolPackageStoreMock(
                    new DirectoryPath(_toolsDirectory),
                    _fileSystem);
            _toolPackageUninstallerMock = new ToolPackageUninstallerMock(_fileSystem, store);
            _toolPackageDownloader = new ToolPackageDownloaderMock2(_toolPackageStore,
                runtimeJsonPathForTests: TestContext.GetRuntimeGraphFilePath(),
                currentWorkingDirectory: null,
                fileSystem: _fileSystem);

            _createToolPackageStoreDownloaderUninstaller = (location, forwardArguments, workingDirectory) => (_toolPackageStore, _toolPackageStoreQuery, _toolPackageDownloader, _toolPackageUninstallerMock);


            _parseResult = Parser.Parse($"dotnet tool install -g {PackageId}");
        }

        [Fact]
        public void WhenPassingRestoreActionConfigOptions()
        {
            var parseResult = Parser.Parse($"dotnet tool install -g {PackageId} --ignore-failed-sources");
            var toolInstallCommand = new ToolInstallGlobalOrToolPathCommand(parseResult);
            toolInstallCommand._restoreActionConfig.IgnoreFailedSources.Should().BeTrue();
        }

        [Fact]
        public void WhenPassingIgnoreFailedSourcesItShouldNotThrow()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, "nuget.config"), _nugetConfigWithInvalidSources);

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                Parser.Parse($"dotnet tool install -g {PackageId} --ignore-failed-sources"),
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);
            _fileSystem.File.Delete(Path.Combine(_temporaryDirectory, "nuget.config"));
        }

        [Fact]
        public void WhenDuplicateSourceIsPassedIgnore()
        {
            var duplicateSource = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json";
            var testAsset = _testAssetsManager
                .CopyTestAsset("NuGetConfigRandomPackageSources", allowCopyIfPresent: true)
                .WithSource();

            var packageSourceLocation = new PackageSourceLocation(
                nugetConfig: new FilePath(Path.Combine(testAsset.Path, "NuGet.config")),
                rootConfigDirectory: new DirectoryPath(testAsset.Path),
                additionalSourceFeeds: [duplicateSource]);
            var nuGetPackageDownloader = new NuGetPackageDownloader(new DirectoryPath(testAsset.Path));

            var sources = nuGetPackageDownloader.LoadNuGetSources(new Cli.ToolPackage.PackageId(PackageId), packageSourceLocation);
            // There should only be one source
            sources.Where(s => s.SourceUri == new Uri(duplicateSource))
                .Should().HaveCount(1);
            // It should be the source from the NuGet.config file
            sources.Where(s => s.SourceUri == new Uri(duplicateSource)).Single().Name
                .Should().Be("example_source");
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldCreateValidShim()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
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
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
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
        public void WhenRunWithSourceItShouldFindOnlyTheProvidedSource()
        {
            const string sourcePath1 = "https://sourceOne.com";
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --source {sourcePath1}");

            _toolPackageDownloader.MockFeedWithNoPackages = sourcePath1;

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            // Should not find the package because it is in the wrong feed
            var ex = Assert.Throws<NuGetPackageNotFoundException>(() => toolInstallGlobalOrToolPathCommand.Execute());
            ex.Message.Should().Contain(PackageId);
        }

        [Fact]
        public void WhenRunWithPackageIdWithSourceItShouldCreateValidShim()
        {
            const string sourcePath = "http://mysource.com";
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --add-source {sourcePath}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
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
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void WhenRunWithPackageIdPackageFormatIsNotFullySupportedItShouldShowPathInstruction()
        {
            _toolPackageDownloader.AddMockPackage(new MockFeedPackage()
            {
                PackageId = PackageId,
                Version = PackageVersion,
                ToolCommandName = ToolCommandName,
                ToolFormatVersion = "42",
            });

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.First().Should().Be(CliStrings.FormatVersionIsHigher.Yellow());
            _reporter.Lines.Skip(1).First().Should().Be(EnvironmentPathInstructionMock.MockInstructionText);
        }

        [Fact]
        public void GivenFailedPackageInstallWhenRunWithPackageIdItShouldFail()
        {
            const string ErrorMessage = "Simulated error";

            _toolPackageDownloader.DownloadCallback = () => throw new ToolPackageException(ErrorMessage);

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(ErrorMessage);

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenCreateShimItShouldHaveNoBrokenFolderOnDisk()
        {
            _fileSystem.File.CreateEmptyFile(ExpectedCommandPath()); // Create conflict shim

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(
                    CliStrings.ShellShimConflict,
                    ToolCommandName));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void GivenInCorrectToolConfigurationWhenRunWithPackageIdItShouldFail()
        {

            _toolPackageDownloader.DownloadCallback = () => throw new ToolConfigurationException("Simulated error");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(
                        CliCommandStrings.InvalidToolConfiguration,
                        "Simulated error") + Environment.NewLine +
                    string.Format(CliCommandStrings.ToolInstallationFailedContactAuthor, PackageId)
                );
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                _parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithPackageIdWithQuietItShouldShowNoSuccessMessage()
        {
            var parseResultQuiet = Parser.Parse($"dotnet tool install -g {PackageId} --verbosity quiet");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResultQuiet,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .NotContain(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithInvalidVersionItShouldThrow()
        {
            const string invalidVersion = "!NotValidVersion!";
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {invalidVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action action = () => toolInstallGlobalOrToolPathCommand.Execute();

            action
                .Should().Throw<GracefulException>()
                .WithMessage(string.Format(
                    CliCommandStrings.ToolInstallInvalidNuGetVersionRange,
                    invalidVersion));
        }

        [Fact]
        public void WhenRunWithExactVersionItShouldSucceed()
        {
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenInstallTheSpecificSameVersionTwiceItShouldNoop()
        {
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
            _reporter.Clear();

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(string.Format(CliCommandStrings.ToolAlreadyInstalled, PackageId, PackageVersion).Green());
        }

        [Fact]
        public void WhenInstallWithHigherVersionItShouldUpdate()
        {
            AddHigherToolPackageVersionToFeed();

            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
            _reporter.Clear();

            ParseResult result2 = Parser.Parse($"dotnet tool install -g {PackageId} --version {HigherPackageVersion}");

            var toolInstallGlobalOrToolPathCommand2 = new ToolInstallGlobalOrToolPathCommand(
                result2,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand2.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolUpdateUpdateSucceeded,
                    PackageId,
                    PackageVersion,
                    HigherPackageVersion).Green());
        }

        [Fact]
        public void WhenInstallWithLowerVersionWithAllowDowngradeOptionItShouldDowngrade()
        {
            AddLowerToolPackageVersionToFeed();

            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
            _reporter.Clear();

            ParseResult result2 = Parser.Parse($"dotnet tool install -g {PackageId} --version {LowerPackageVersion} --allow-downgrade");

            var toolInstallGlobalOrToolPathCommand2 = new ToolInstallGlobalOrToolPathCommand(
                result2,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand2.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolUpdateUpdateSucceeded,
                    PackageId,
                    PackageVersion,
                    LowerPackageVersion).Green());
        }

        [Fact]
        public void WhenInstallWithLowerVersionItShouldFail()
        {
            AddLowerToolPackageVersionToFeed();

            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version {PackageVersion}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
            _reporter.Clear();

            ParseResult result2 = Parser.Parse($"dotnet tool install -g {PackageId} --version {LowerPackageVersion}");

            var toolInstallGlobalOrToolPathCommand2 = new ToolInstallGlobalOrToolPathCommand(
                result2,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand2.Execute();
            a.Should().Throw<GracefulException>();
        }

        [Fact]
        public void WhenRunWithValidVersionRangeItShouldSucceed()
        {
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version [1.0,2.0]");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Theory]
        [InlineData("0.5.0")]
        [InlineData("[0.5.0]")]
        public void WhenRunWithValidVersionItShouldInterpretAsNuGetExactVersion(string version)
        {
            const string nugetSourcePath = "https://api.nuget.org/v3/index.json";
            var testDir = _testAssetsManager.CreateTestDirectory().Path;
            var ridGraphPath = TestContext.GetRuntimeGraphFilePath();

            var toolInstallCommand = new ToolInstallGlobalOrToolPathCommand(Parser.Parse($"dotnet tool install -g {UnlistedPackageId} --version {version} --add-source {nugetSourcePath}"),
                createToolPackageStoreDownloaderUninstaller: (nonGlobalLocation, _, _) =>
                {
                    ToolPackageStoreAndQuery toolPackageStore = ToolPackageFactory.CreateToolPackageStoreQuery(nonGlobalLocation);
                    var toolPackageDownloader = new ToolPackageDownloader(toolPackageStore, ridGraphPath, currentWorkingDirectory: testDir);
                    var toolPackageUninstaller = new ToolPackageUninstaller(
                        toolPackageStore);

                    return (toolPackageStore, toolPackageStore, toolPackageDownloader, toolPackageUninstaller);
                },
                createShellShimRepository: _createShellShimRepository,
                nugetPackageDownloader: new NuGetPackageDownloader(new DirectoryPath(PathUtilities.CreateTempSubdirectory()), verifySignatures: false, currentWorkingDirectory: testDir),
                currentWorkingDirectory: testDir,
                verifySignatures: false);

            toolInstallCommand.Execute().Should().Be(0);

            // Uninstall the unlisted package
            var toolUninstallCommand = new ToolUninstallGlobalOrToolPathCommand(Parser.Parse("dotnet tool uninstall -g " + UnlistedPackageId),
                // This is technically not _createShellShimRepository because that is a Microsoft.DotNet.Tools.Tool.Install.CreateShellShimRepository.
                // This is a Microsoft.DotNet.Tools.Tool.Uninstall.CreateShellShimRepository.
                createShellShimRepository: (_, nonGlobalLocation) => new ShellShimRepository(
                    new DirectoryPath(_pathToPlaceShim),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem),
                    filePermissionSetter: new NoOpFilePermissionSetter()));
            toolUninstallCommand.Execute().Should().Be(0);
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
            AddPreviewToolPackageVersionToFeed();

            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --prerelease");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Contain(l => l == string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    "2.0.1-preview1").Green());
        }

        [Fact]
        public void WhenRunWithPrereleaseAndPackageVersionItShouldThrow()
        {
            AddPreviewToolPackageVersionToFeed();

            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version 2.0 --prerelease");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();
            a.Should().Throw<GracefulException>();
        }

        private void AddPreviewToolPackageVersionToFeed()
        {
            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "1.0.4",
                ToolCommandName = "SimulatorCommand"
            });

            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "2.0.1-preview1",
                ToolCommandName = "SimulatorCommand"
            });
        }


        private void AddLowerToolPackageVersionToFeed()
        {
            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "1.0.4",
                ToolCommandName = "SimulatorCommand"
            });

            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "1.0.0",
                ToolCommandName = "SimulatorCommand"
            });
        }


        private void AddHigherToolPackageVersionToFeed()
        {
            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "1.0.4",
                ToolCommandName = "SimulatorCommand"
            });

            _toolPackageDownloader.AddMockPackage(new MockFeedPackage
            {
                PackageId = PackageId,
                Version = "2.0.0",
                ToolCommandName = "SimulatorCommand"
            });
        }

        [Fact]
        public void WhenRunWithoutAMatchingRangeItShouldFail()
        {
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version [5.0,10.0]");

            _toolPackageDownloader.AddMockPackage(new MockFeedPackage()
            {
                PackageId = PackageId,
                Version = PackageVersion,
                ToolCommandName = ToolCommandName,
            });

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            Action a = () => toolInstallGlobalOrToolPathCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(string.Format(CliStrings.IsNotFoundInNuGetFeeds, $"Version 5.0 of {PackageId}", "{MockFeeds}"));

            _fileSystem.Directory.Exists(Path.Combine(_pathToPlacePackages, PackageId)).Should().BeFalse();
        }

        [Fact]
        public void WhenRunWithValidVersionWildcardItShouldSucceed()
        {
            ParseResult result = Parser.Parse($"dotnet tool install -g {PackageId} --version 1.0.*");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Should()
                .Equal(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
                    ToolCommandName,
                    PackageId,
                    PackageVersion).Green());
        }

        [Fact]
        public void WhenRunWithPackageIdAndBinPathItShouldNoteHaveEnvironmentPathInstruction()
        {
            var result = Parser.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");

            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim),
                _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().NotContain(l => l.Contains(EnvironmentPathInstructionMock.MockInstructionText));
        }

        [Fact]
        public void WhenInstallItDoesNotSkipNuGetPackageVerfication()
        {
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                 _parseResult,
                 createToolPackageStoreDownloaderUninstaller: _createToolPackageStoreDownloaderUninstaller,
                 createShellShimRepository: _createShellShimRepository,
                 environmentPathInstruction: new EnvironmentPathInstructionMock(_reporter, _pathToPlaceShim, true),
                 reporter: _reporter);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);
            _reporter.Lines.Should().NotContain(l => l.Contains(CliStrings.NuGetPackageSignatureVerificationSkipped));
        }

        [Fact]
        public void AndPackagedShimIsProvidedWhenRunWithPackageIdItCreateShimUsingPackagedShim()
        {

            string toolTargetRuntimeIdentifier = OperatingSystem.IsWindows() ? "win-x64" : RuntimeInformation.RuntimeIdentifier;

            var tokenToIdentifyPackagedShim = $"{toolTargetRuntimeIdentifier}-tool";

            var result = Parser.Parse($"dotnet tool install --tool-path /tmp/folder {PackageId}");


            var mockPackage = new MockFeedPackage()
            {
                PackageId = PackageId,
                Version = PackageVersion,
                ToolCommandName = ToolCommandName
            };

            mockPackage.AdditionalFiles[$"tools/{ToolPackageDownloaderMock2.DefaultTargetFramework}/any/shims/{toolTargetRuntimeIdentifier}/{ToolCommandName}{EnvironmentInfo.ExecutableExtension}"] = tokenToIdentifyPackagedShim;

            _toolPackageDownloader.AddMockPackage(mockPackage);

            var installCommand = new ToolInstallGlobalOrToolPathCommand(
                result,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
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
            var parseResult = Parser.Parse($"dotnet tool install -g {PackageId} -a invalid");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
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
            var parseResult = Parser.Parse($"dotnet tool install -g {PackageId} -a arm64");
            var toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResult,
                _packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                _environmentPathInstructionMock,
                _reporter,
                nugetPackageDownloader);

            toolInstallGlobalOrToolPathCommand.Execute().Should().Be(0);
            nugetPackageDownloader.DownloadCallParams.Count.Should().Be(1);
            nugetPackageDownloader.ExtractCallParams.Count.Should().Be(1);
            nugetPackageDownloader.DownloadCallParams.First().Item1.Should().Be(new PackageId("microsoft.netcore.app.host.win-arm64"));
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



