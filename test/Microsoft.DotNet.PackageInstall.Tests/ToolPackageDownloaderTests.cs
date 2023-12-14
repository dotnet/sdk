// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using NuGet.Configuration;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    internal class DotnetEnvironmentTestFixture : IDisposable
    {
        private readonly string _originalPath;
        private const string _PATH_VAR_NAME = "PATH";

        public DotnetEnvironmentTestFixture()
        {
            string dotnetRootUnderTest = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            _originalPath = Environment.GetEnvironmentVariable(_PATH_VAR_NAME);
            Environment.SetEnvironmentVariable(_PATH_VAR_NAME, dotnetRootUnderTest + Path.PathSeparator + _originalPath);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_PATH_VAR_NAME, _originalPath);
    }

    public class ToolPackageDownloaderTests : SdkTest, IClassFixture<DotnetEnvironmentTestFixture>
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath,
                identiifer: testMockBehaviorIsInSync.ToString());

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceedsInTransaction(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            IToolPackage package = null;
            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                    packageId: TestPackageId,
                    verbosity: TestVerbosity,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    targetFramework: _testTargetframework,
                    isGlobalTool: true);

                transactionScope.Complete();
            }

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallCreatesAnAssetFile(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            /*
              From mytool.dll to project.assets.json
               <root>/packageid/version/packageid/version/tools/framework/rid/mytool.dll
                                       /project.assets.json
             */
            var assetJsonPath = package.Commands[0].Executable
                .GetDirectoryPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .WithFile("project.assets.json").Value;

            fileSystem.File.Exists(assetJsonPath).Should().BeTrue();

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir(
            bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var subDirUnderNugetConfigPath = nugetConfigPath.GetDirectoryPath().WithSubDirectories("sub");

            var onlyNugetConfigInParentDirHasPackagesFeed = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.FeedFromLookUpNugetConfig,
                    Uri = nugetConfigPath.Value,
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

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath,
                feeds: onlyNugetConfigInParentDirHasPackagesFeed);

            fileSystem.Directory.CreateDirectory(subDirUnderNugetConfigPath.Value);

            var package = downloader.InstallPackage(
                new PackageLocation(rootConfigDirectory: subDirUnderNugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(
                new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenASourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenARelativeSourcePathInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[]
                    {Path.GetRelativePath(Directory.GetCurrentDirectory(), source)}), packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAUriSourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { new Uri(source).AbsoluteUri }), packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath,
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailureAfterRestoreInstallWillRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            static void FailedStepAfterSuccessRestore() => throw new GracefulException("simulated error");

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true);

                    FailedStepAfterSuccessRestore();
                    t.Complete();
                }
            };

            a.Should().Throw<GracefulException>().WithMessage("simulated error");

            AssertInstallRollBack(fileSystem, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenSecondInstallInATransactionTheFirstInstallShouldRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    Action first = () => downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true);

                    first.Should().NotThrow();

                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true);

                    t.Complete();
                }
            };

            a.Should().Throw<ToolPackageException>().Where(
                ex => ex.Message ==
                      string.Format(
                          CommonLocalizableStrings.ToolPackageConflictPackageId,
                          TestPackageId,
                          TestPackageVersion));

            AssertInstallRollBack(fileSystem, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailureWhenInstallLocalToolsItWillRollbackPackageVersion(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            static void FailedStepAfterSuccessDownload() => throw new GracefulException("simulated error");
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            var localToolDownloadDir = Path.Combine(new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings)).ToString().Trim('"'), TestPackageId.ToString());

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);

                    fileSystem
                    .Directory
                        .Exists(localToolDownloadDir)
                        .Should()
                        .BeTrue();

                    FailedStepAfterSuccessDownload();
                    t.Complete();
                }
            };

            a.Should().Throw<GracefulException>().WithMessage("simulated error");
            
            fileSystem
            .Directory
                .Exists(localToolDownloadDir)
                .Should()
                .BeTrue();

            var localToolVersionDir = Path.Combine(localToolDownloadDir, TestPackageVersion.ToString());
            fileSystem
                .Directory
                .Exists(localToolVersionDir)
                .Should()
                .BeFalse();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenSecondInstallOfLocalToolItShouldNotThrowException(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);


                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);

                    t.Complete();
                }
            };
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenSecondInstallWithoutATransactionTheFirstShouldNotRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            Action secondCall = () => downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            reporter.Lines.Should().BeEmpty();

            secondCall.Should().Throw<ToolPackageException>().Where(
                ex => ex.Message ==
                      string.Format(
                          CommonLocalizableStrings.ToolPackageConflictPackageId,
                          TestPackageId,
                          TestPackageVersion));

            fileSystem
                .Directory
                .Exists(store.Root.WithSubDirectories(TestPackageId.ToString()).Value)
                .Should()
                .BeTrue();

            uninstaller.Uninstall(package.PackageDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackage(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source),
                identiifer: testMockBehaviorIsInSync.ToString());

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRollsbackWhenTransactionFails(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                uninstaller.Uninstall(package.PackageDirectory);

                storeQuery.EnumeratePackages().Should().BeEmpty();
            }

            package = storeQuery.EnumeratePackageVersions(TestPackageId).First();

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackageWhenTransactionCommits(
            bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                uninstaller.Uninstall(package.PackageDirectory);
                scope.Complete();
            }

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAPackageNameWithDifferentCaseItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: new PackageId("GlObAl.TooL.coNsoLe.DemO"),
                verbosity: TestVerbosity,
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Fact]
        public void GivenARootWithNonAsciiCharacterInstallSucceeds()
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var surrogate = char.ConvertFromUtf32(int.Parse("2A601", NumberStyles.HexNumber));
            string nonAscii = "ab Ṱ̺̺̕o 田中さん åä," + surrogate;

            var root = _testAssetsManager.CreateTestDirectory(testName: nonAscii, identifier: "root");
            var reporter = new BufferedReporter();
            var fileSystem = new FileSystemWrapper();
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(root.Path));
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);
            var testRuntimeJsonPath = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");

            var downloader = new ToolPackageDownloader(
                store: store,
                testRuntimeJsonPath
                );

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, store);

            new ToolPackageUninstaller(store).Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        // repro https://github.com/dotnet/cli/issues/9409
        public void GivenAComplexVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath,
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse("1.0.0-rc*"),
                targetFramework: _testTargetframework,
                isGlobalTool: true);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [UnixOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        // repro https://github.com/dotnet/cli/issues/10101
        public void GivenAPackageWithCasingAndenUSPOSIXInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var packageId = new PackageId("Global.Tool.Console.Demo.With.Casing");
            var packageVersion = "2.0.4";
            var feed = new MockFeed
            {
                Type = MockFeedType.ImplicitAdditionalFeed,
                Uri = nugetConfigPath.Value,
                Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = packageId.ToString(),
                            Version = packageVersion,
                            ToolCommandName = "DemoWithCasing",
                        }
                    }
            };

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: new List<MockFeed> { feed },
                writeLocalFeedToNugetConfig: nugetConfigPath);

            CultureInfo currentCultureBefore = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US-POSIX");
                IToolPackage package = null;
                Action action = () => package = downloader.InstallPackage(
                    new PackageLocation(
                        nugetConfig: nugetConfigPath,
                        additionalFeeds: new[] { emptySource }),
                    packageId: packageId,
                    verbosity: TestVerbosity,
                    versionRange: VersionRange.Parse(packageVersion),
                    targetFramework: _testTargetframework,
                    isGlobalTool: true);

                action.Should().NotThrow<ToolConfigurationException>();

                fileSystem.File.Exists(package.Commands[0].Executable.Value).Should().BeTrue($"{package.Commands[0].Executable.Value} should exist");

                uninstaller.Uninstall(package.PackageDirectory);
            }
            finally
            {
                CultureInfo.CurrentCulture = currentCultureBefore;
            }
        }

        private static void AssertPackageInstall(
            BufferedReporter reporter,
            IFileSystem fileSystem,
            IToolPackage package,
            IToolPackageStore store,
            IToolPackageStoreQuery storeQuery)
        {
            reporter.Lines.Should().BeEmpty();

            package.Id.Should().Be(TestPackageId);
            package.Version.ToNormalizedString().Should().Be(TestPackageVersion);
            package.PackageDirectory.Value.Should().Contain(store.Root.Value);
            package.Frameworks.Should().BeEquivalentTo(TestFrameworks);

            storeQuery.EnumeratePackageVersions(TestPackageId)
                .Select(p => p.Version.ToNormalizedString())
                .Should()
                .Equal(TestPackageVersion);

            package.Commands.Count.Should().Be(1);
            fileSystem.File.Exists(package.Commands[0].Executable.Value).Should()
                .BeTrue($"{package.Commands[0].Executable.Value} should exist");
            package.Commands[0].Executable.Value.Should().Contain(store.Root.Value);
        }

        private static void AssertInstallRollBack(IFileSystem fileSystem, IToolPackageStore store)
        {
            if (!fileSystem.Directory.Exists(store.Root.Value))
            {
                return;
            }

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.Value)
                .Should()
                .NotContain(e => Path.GetFileName(e) != ToolPackageStoreAndQuery.StagingDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        }

        private static List<MockFeed> GetMockFeedsForConfigFile(FilePath? nugetConfig)
        {
            if (nugetConfig.HasValue)
            {
                return new List<MockFeed>
                {
                    new MockFeed
                    {
                        Type = MockFeedType.ExplicitNugetConfig,
                        Uri = nugetConfig.Value.Value,
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
            else
            {
                return new List<MockFeed>();
            }
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

        private static List<MockFeed> GetOfflineMockFeed()
        {
            return new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.ImplicitAdditionalFeed,
                    Uri = GetTestLocalFeedPath(),
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
                FilePath? writeLocalFeedToNugetConfig = null,
                [CallerMemberName] string callingMethod = "",
                string identiifer = null)
        {
            var root = new DirectoryPath(_testAssetsManager.CreateTestDirectory(callingMethod, identifier: useMock.ToString() + identiifer).Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageDownloader downloader;
            IToolPackageUninstaller uninstaller;
            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                var frameworksMap = new Dictionary<PackageId, IEnumerable<NuGetFramework>>()
                        { {TestPackageId, TestFrameworks } };
                WriteNugetConfigFileToPointToTheFeed(fileSystem, writeLocalFeedToNugetConfig);
                var toolPackageStoreMock = new ToolPackageStoreMock(root, fileSystem, frameworksMap);
                store = toolPackageStoreMock;
                storeQuery = toolPackageStoreMock;
                downloader = new ToolPackageDownloaderMock(
                    store: toolPackageStoreMock,
                    fileSystem: fileSystem,
                    reporter: reporter,
                    feeds: feeds == null
                            ? GetMockFeedsForConfigFile(writeLocalFeedToNugetConfig)
                            : feeds.Concat(GetMockFeedsForConfigFile(writeLocalFeedToNugetConfig)).ToList(),
                    frameworksMap: frameworksMap);
                uninstaller = new ToolPackageUninstallerMock(fileSystem, toolPackageStoreMock);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                WriteNugetConfigFileToPointToTheFeed(fileSystem, writeLocalFeedToNugetConfig);
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

        private static void WriteNugetConfigFileToPointToTheFeed(IFileSystem fileSystem, FilePath? filePath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                localFeedPath: GetTestLocalFeedPath()));
        }

        public static string FormatNuGetConfig(string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
</configuration>";
            return string.Format(template, localFeedPath);
        }

        private static FilePath GenerateRandomNugetConfigFilePath()
        {
            const string nugetConfigName = "nuget.config";
            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());

            FilePath nugetConfigFullPath =
                new(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
            return nugetConfigFullPath;
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new("global.tool.console.demo");
        private static readonly IEnumerable<NuGetFramework> TestFrameworks = new NuGetFramework[] { NuGetFramework.Parse("netcoreapp2.1") };
        private static readonly VerbosityOptions TestVerbosity = new VerbosityOptions();
        public ToolPackageDownloaderTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
