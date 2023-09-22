// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class ToolPackageUninstallerTests : SdkTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackage(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source),
                identifier: testMockBehaviorIsInSync.ToString());

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            package.PackagedShims.Should().ContainSingle(f => f.Value.Contains("demo.exe") || f.Value.Contains("demo"));

            uninstaller.Uninstall(package.PackageDirectory);

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        private static List<MockFeed> GetMockFeedsForSource(string source)
        {
            return new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.ImplicitAdditionalFeed,
                    Uri = source,
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

        private (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader, IToolPackageUninstaller, BufferedReporter, IFileSystem
        ) Setup(
            bool useMock,
            List<MockFeed> feeds = null,
            [CallerMemberName] string testName = "",
            string identifier = null)
        {
            var root = new DirectoryPath(_testAssetsManager.CreateTestDirectory(testName, identifier).Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageDownloader downloader;
            IToolPackageUninstaller uninstaller;
            if (useMock)
            {
                var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
                {
                    [TestPackageId] = new FilePath[] { new FilePath("path/demo.exe") }
                };

                fileSystem = new FileSystemMockBuilder().Build();
                var toolPackageStoreMock = new ToolPackageStoreMock(root, fileSystem);
                store = toolPackageStoreMock;
                storeQuery = toolPackageStoreMock;

                downloader = new ToolPackageDownloaderMock(
                    store: toolPackageStoreMock,
                    fileSystem: fileSystem,
                    reporter: reporter,
                    feeds: feeds,
                    packagedShimsMap: packagedShimsMap);
                uninstaller = new ToolPackageUninstallerMock(fileSystem, toolPackageStoreMock);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                var toolPackageStore = new ToolPackageStoreAndQuery(root);
                store = toolPackageStore;
                storeQuery = toolPackageStore;
                var testRuntimeJsonPath = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");
                downloader = new ToolPackageDownloader(store, testRuntimeJsonPath);
                uninstaller = new ToolPackageUninstaller(store);
            }

            store.Root.Value.Should().Be(Path.GetFullPath(root.Value));

            return (store, storeQuery, downloader, uninstaller, reporter, fileSystem);
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo.with.shim");
        private static readonly VerbosityOptions TestVerbosity = new VerbosityOptions();
        public ToolPackageUninstallerTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
