// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class ToolPackageInstallToManagedLocationInstaller : SdkTest
    {
        public ToolPackageInstallToManagedLocationInstaller(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;

            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            try
            {
                var nugetCacheLocation =
                    new DirectoryPath(testDirectory).WithSubDirectories(Path.GetRandomFileName());

                IToolPackage toolPackage = installer.InstallPackage(
                    packageId: TestPackageId,
                    verbosity: TestVerbosity,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                    targetFramework: _testTargetframework,
                    verifySignatures: false);

                var commands = toolPackage.Commands;
                var expectedPackagesFolder = NuGetGlobalPackagesFolder.GetLocation();
                commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);

                fileSystem.File
                    .Exists(commands[0].Executable.Value)
                    .Should().BeTrue($"{commands[0].Executable.Value} should exist");
            }
            finally
            {
                foreach (var line in reporter.Lines)
                {
                    Log.WriteLine(line);
                }
            }
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;

            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            IToolPackage toolPackage = installer.InstallPackage(
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse("1.0.0-*"),
                packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                targetFramework: _testTargetframework,
                verifySignatures: false);

            var expectedPackagesFolder = NuGetGlobalPackagesFolder.GetLocation();

            var commands = toolPackage.Commands;
            commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);
            toolPackage.Version.Should().Be(NuGetVersion.Parse(TestPackageVersion));
        }

        private static List<MockFeed> GetMockFeedsForConfigFile(FilePath nugetConfig)
        {
            return new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.ExplicitNugetConfig,
                    Uri = nugetConfig.Value,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        private (IToolPackageStore, IToolPackageDownloader, BufferedReporter, IFileSystem) Setup(
            bool useMock,
            string testDirectory,
            List<MockFeed> feeds = null)
        {
            var root = new DirectoryPath(Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName()));
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageDownloader downloader;
            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                store = new ToolPackageStoreMock(root, fileSystem);
                downloader = new ToolPackageDownloaderMock(
                    store: store,
                    fileSystem: fileSystem,
                    reporter: reporter,
                    feeds: feeds);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                store = new ToolPackageStoreAndQuery(root);
                var runtimeJsonPathForTests = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");
                downloader = new ToolPackageDownloader(store, runtimeJsonPathForTests);
            }

            return (store, downloader, reporter, fileSystem);
        }

        private FilePath WriteNugetConfigFileToPointToTheFeed(string testDirectory)
        {
            var nugetConfigName = "NuGet.Config";

            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(testDirectory,
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPathForNugetConfigWithWhiteSpace);

            NuGetConfigWriter.Write(tempPathForNugetConfigWithWhiteSpace, GetTestLocalFeedPath());

            return new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
        }

        private static string GetTestLocalFeedPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly VerbosityOptions TestVerbosity = new VerbosityOptions();
        private static readonly PackageId TestPackageId = new("global.tool.console.demo");
    }
}
