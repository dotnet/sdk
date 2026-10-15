// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    [TestClass]
    public class ToolUninstallGlobalOrToolPathCommandTests
    {
        public TestContext TestContext { get; set; }

        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;

        private const string PackageId = "global.tool.console.demo";
        private const string PackageVersion = "1.0.4";
        private readonly string _shimsDirectory;
        private readonly string _toolsDirectory;

        public ToolUninstallGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            var tempDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _shimsDirectory = Path.Combine(tempDirectory, "shims");
            _toolsDirectory = Path.Combine(tempDirectory, "tools");
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, _shimsDirectory);
        }

        [TestMethod]
        public void GivenANonExistentPackageItErrors()
        {
            var packageId = "does.not.exist";
            var command = CreateUninstallCommand($"-g {packageId}");

            Action a = () => command.Execute();

            a.Should().Throw<GracefulException>()
                .And
                .Message
                .Should()
                .Be(string.Format(CliCommandStrings.ToolUninstallToolNotInstalled, packageId));
        }

        [TestMethod]
        public void GivenAPackageItUninstalls()
        {
            CreateInstallCommand($"-g {PackageId} --verbosity minimal").Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
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

            _reporter.Lines.Clear();

            CreateUninstallCommand($"-g {PackageId}").Execute().Should().Be(0);

            _reporter
                .Lines
                .Single()
                .Should()
                .Contain(string.Format(
                    CliCommandStrings.ToolUninstallUninstallSucceeded,
                    PackageId,
                    PackageVersion));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeFalse();
            _fileSystem.File.Exists(shimPath).Should().BeFalse();
        }

        [TestMethod]
        public async Task GivenUninstallInProgressAnotherInstallWaitsBeforeReinstalling()
        {
            CreateInstallCommand($"-g {PackageId} --verbosity minimal").Execute().Should().Be(0);
            _reporter.Lines.Clear();

            var store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);
            using var blockingUninstaller = new BlockingToolPackageUninstaller(
                new ToolPackageUninstallerMock(_fileSystem, store));
            var uninstallCommand = new ToolUninstallGlobalOrToolPathCommand(
                Parser.Parse($"dotnet tool uninstall -g {PackageId}"),
                _ => (store, store, blockingUninstaller),
                (_, _) => new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem)),
                new BufferedReporter());

            Task<int> uninstall = Task.Run(uninstallCommand.Execute, TestContext.CancellationToken);
            await blockingUninstaller.UninstallStarted;
            Task<int> reinstall = Task.Run(
                () => CreateInstallCommand($"-g {PackageId} --verbosity minimal").Execute(),
                TestContext.CancellationToken);

            Task firstCompleted = await Task.WhenAny(
                reinstall,
                Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.CancellationToken));
            blockingUninstaller.Continue();

            (await uninstall).Should().Be(0);
            (await reinstall).Should().Be(0);
            firstCompleted.Should().NotBe(
                reinstall,
                "install must not observe a package that uninstall has not committed");

            var packageDirectory = new DirectoryPath(Path.GetFullPath(_toolsDirectory))
                .WithSubDirectories(PackageId, PackageVersion);
            string shimPath = Path.Combine(
                _shimsDirectory,
                ProjectRestorerMock.DefaultToolCommandName +
                    (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));
            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();
        }

        [TestMethod]
        public async Task GivenPackageMutationInProgressUninstallWaitsBeforeReadingInstalledState()
        {
            CreateInstallCommand($"-g {PackageId} --verbosity minimal").Execute().Should().Be(0);

            var store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);
            IToolPackage package = store.EnumeratePackageVersions(new PackageId(PackageId)).Single();
            FileStream mutationLock = ToolPackageDownloaderBase.AcquireToolPackageMutationLock(
                store.Root,
                package.Id);
            new ToolPackageUninstallerMock(_fileSystem, store).Uninstall(package.PackageDirectory);

            try
            {
                Task<int> uninstall = Task.Run(
                    () => CreateUninstallCommand($"-g {PackageId}").Execute(),
                    TestContext.CancellationToken);
                Task firstCompleted = await Task.WhenAny(
                    uninstall,
                    Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.CancellationToken));

                firstCompleted.Should().NotBe(
                    uninstall,
                    "uninstall must acquire the mutation lock before reading transient package state");
                mutationLock.Dispose();

                Func<Task> awaitUninstall = async () => await uninstall;
                (await awaitUninstall.Should().ThrowAsync<GracefulException>())
                    .Which.Message.Should().Be(
                        string.Format(CliCommandStrings.ToolUninstallToolNotInstalled, PackageId));
            }
            finally
            {
                mutationLock.Dispose();
            }
        }

        [TestMethod]
        public void GivenAPackageWhenCallFromUninstallRedirectCommandItUninstalls()
        {
            CreateInstallCommand($"-g {PackageId}  --verbosity minimal").Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
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

            _reporter.Lines.Clear();


            ParseResult result = Parser.Parse("dotnet tool uninstall " + $"-g {PackageId}");

            (IToolPackageStore, IToolPackageStoreQuery, IToolPackageUninstaller) CreateToolPackageStoreAndUninstaller(
                DirectoryPath? directoryPath)
            {
                var store = new ToolPackageStoreMock(
                    new DirectoryPath(_toolsDirectory),
                    _fileSystem);
                var packageUninstaller = new ToolPackageUninstallerMock(_fileSystem, store);
                return (store, store, packageUninstaller);
            }

            var toolUninstallGlobalOrToolPathCommand = new ToolUninstallGlobalOrToolPathCommand(
                result,
                CreateToolPackageStoreAndUninstaller,
                (_, _) => new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem)),
                _reporter);

            var uninstallCommand
                = new ToolUninstallCommand(
                    result,
                    toolUninstallGlobalOrToolPathCommand: toolUninstallGlobalOrToolPathCommand);

            uninstallCommand.Execute().Should().Be(0);

            _reporter
                .Lines
                .Single()
                .Should()
                .Contain(string.Format(
                    CliCommandStrings.ToolUninstallUninstallSucceeded,
                    PackageId,
                    PackageVersion));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeFalse();
            _fileSystem.File.Exists(shimPath).Should().BeFalse();
        }

        [TestMethod]
        public void GivenAFailureToUninstallItLeavesItInstalled()
        {
            CreateInstallCommand($"-g {PackageId} --verbosity minimal").Execute().Should().Be(0);

            _reporter
                .Lines
                .Last()
                .Should()
                .Contain(string.Format(
                    CliCommandStrings.ToolInstallInstallationSucceeded,
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

            Action a = () => CreateUninstallCommand(
                options: $"-g {PackageId}",
                uninstallCallback: () => throw new IOException("simulated error"))
                .Execute();

            a.Should().Throw<GracefulException>()
                .And
                .Message
                .Should()
                .Be(string.Format(
                    CliStrings.FailedToUninstallToolPackage,
                    PackageId,
                    "simulated error"));

            _fileSystem.Directory.Exists(packageDirectory.Value).Should().BeTrue();
            _fileSystem.File.Exists(shimPath).Should().BeTrue();
        }

        [TestMethod]
        public void GivenAnInvalidToolPathItThrowsException()
        {
            var toolPath = "tool-path-does-not-exist";

            var uninstallCommand = CreateUninstallCommand($"--tool-path {toolPath} {PackageId}");

            Action a = () => uninstallCommand.Execute();

            a.Should().Throw<GracefulException>()
                .And
                .Message
                .Should()
                .Be(string.Format(CliCommandStrings.ToolUninstallInvalidToolPathOption, toolPath));
        }

        private ToolInstallGlobalOrToolPathCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Parse("dotnet tool install " + options);

            var store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);


            var toolPackageUninstallerMock = new ToolPackageUninstallerMock(_fileSystem, store);

            var toolPackageDownloaderMock = new ToolPackageDownloaderMock2(store,
                runtimeJsonPathForTests: SdkTestContext.GetRuntimeGraphFilePath(),
                currentWorkingDirectory: null,
                fileSystem: _fileSystem);


            return new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments, currentWorkingDirectory) => (store, store, toolPackageDownloaderMock, toolPackageUninstallerMock),
                (_, _) => new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem)),
                _environmentPathInstructionMock,
                _reporter);
        }

        private ToolUninstallGlobalOrToolPathCommand CreateUninstallCommand(string options, Action uninstallCallback = null)
        {
            ParseResult result = Parser.Parse("dotnet tool uninstall " + options);

            (IToolPackageStore, IToolPackageStoreQuery, IToolPackageUninstaller) createToolPackageStoreAndUninstaller(
                DirectoryPath? directoryPath)
            {
                var store = new ToolPackageStoreMock(
                    new DirectoryPath(_toolsDirectory),
                    _fileSystem);
                var packageUninstaller = new ToolPackageUninstallerMock(_fileSystem, store, uninstallCallback);
                return (store, store, packageUninstaller);
            }

            return new ToolUninstallGlobalOrToolPathCommand(
                result,
                createToolPackageStoreAndUninstaller,
                (_, _) => new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem)),
                _reporter);
        }

        private sealed class BlockingToolPackageUninstaller(IToolPackageUninstaller inner) : IToolPackageUninstaller, IDisposable
        {
            private readonly TaskCompletionSource _uninstallStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ManualResetEventSlim _continue = new(initialState: false);

            public Task UninstallStarted => _uninstallStarted.Task;

            public void Uninstall(DirectoryPath packageDirectory)
            {
                _uninstallStarted.TrySetResult();
                _continue.Wait();
                inner.Uninstall(packageDirectory);
            }

            public void Continue() => _continue.Set();

            public void Dispose() => _continue.Dispose();
        }
    }
}
