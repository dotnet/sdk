// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            Console.WriteLine("1");
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;
            Console.WriteLine("2");
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);
            Console.WriteLine("3");
            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));
            Console.WriteLine("4");
            try
            {
                Console.WriteLine("5");
                var nugetCacheLocation =
                    new DirectoryPath(testDirectory).WithSubDirectories(Path.GetRandomFileName());
                Console.WriteLine("6");
                IToolPackage toolPackage = installer.InstallPackage(
                    packageId: TestPackageId,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                    targetFramework: _testTargetframework);
                Console.WriteLine("7");
                var commands = toolPackage.Commands;
                var expectedPackagesFolder = NuGetGlobalPackagesFolder.GetLocation();
                commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);
                Console.WriteLine("8");
                fileSystem.File
                    .Exists(commands[0].Executable.Value)
                    .Should().BeTrue($"{commands[0].Executable.Value} should exist");
            }
            finally
            {
                Console.WriteLine("9");
                foreach (var line in reporter.Lines)
                {
                    Log.WriteLine(line);
                }
                Console.WriteLine("10");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            Console.WriteLine("11");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            string testDirectory = _testAssetsManager.CreateTestDirectory(identifier: testMockBehaviorIsInSync.ToString()).Path;
            Console.WriteLine("12");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed(testDirectory);
            Console.WriteLine("13");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                testDirectory: testDirectory,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));
            Console.WriteLine("14");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            IToolPackage toolPackage = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.0-*"),
                packageLocation: new PackageLocation(nugetConfig: nugetConfigPath),
                targetFramework: _testTargetframework);
            Console.WriteLine("15");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var expectedPackagesFolder = NuGetGlobalPackagesFolder.GetLocation();
            Console.WriteLine("16");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var commands = toolPackage.Commands;
            Console.WriteLine("17");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            commands[0].Executable.Value.Should().StartWith(expectedPackagesFolder);
            Console.WriteLine("18");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            toolPackage.Version.Should().Be(NuGetVersion.Parse(TestPackageVersion));
            Console.WriteLine("19");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
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
            Console.WriteLine("20");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var root = new DirectoryPath(Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName()));
            Console.WriteLine("21");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var reporter = new BufferedReporter();
            Console.WriteLine("22");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            IFileSystem fileSystem;
            Console.WriteLine("23");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            IToolPackageStore store;
            IToolPackageDownloader downloader;
            if (useMock)
            {
                Console.WriteLine("24");
                Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
                fileSystem = new FileSystemMockBuilder().Build();
                store = new ToolPackageStoreMock(root, fileSystem);
                downloader = new ToolPackageDownloaderMock(
                    store: store,
                    fileSystem: fileSystem,
                    reporter: reporter,
                    feeds: feeds);
                Console.WriteLine("25");
                Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            }
            else
            {
                Console.WriteLine("26");
                Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
                fileSystem = new FileSystemWrapper();
                store = new ToolPackageStoreAndQuery(root);
                var runtimeJsonPathForTests = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");
                downloader = new ToolPackageDownloader(store, runtimeJsonPathForTests);
                Console.WriteLine("27");
                Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            }
            Console.WriteLine("28");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            return (store, downloader, reporter, fileSystem);

        }

        private FilePath WriteNugetConfigFileToPointToTheFeed(string testDirectory)
        {
            Console.WriteLine("29");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            var nugetConfigName = "NuGet.Config";

            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(testDirectory,
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPathForNugetConfigWithWhiteSpace);

            NuGetConfigWriter.Write(tempPathForNugetConfigWithWhiteSpace, GetTestLocalFeedPath());
            Console.WriteLine("30");
            Console.WriteLine(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            return new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
        }

        private static string GetTestLocalFeedPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");
    }
}
