// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
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
            string dotnetRootUnderTest = SdkTestContext.Current.ToolsetUnderTest.DotNetRoot;
            _originalPath = Environment.GetEnvironmentVariable(_PATH_VAR_NAME);
            Environment.SetEnvironmentVariable(_PATH_VAR_NAME, dotnetRootUnderTest + Path.PathSeparator + _originalPath);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_PATH_VAR_NAME, _originalPath);
    }

    [TestClass]
    public class ToolPackageDownloaderTests : SdkTest
    {
        private static DotnetEnvironmentTestFixture _envFixture;
        private static readonly TestToolBuilder ToolBuilder = TestToolBuilder.SharedInstance.Value;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _) => _envFixture = new DotnetEnvironmentTestFixture();

        [ClassCleanup]
        public static void ClassCleanup() => _envFixture?.Dispose();

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true,
                identiifer: testMockBehaviorIsInSync.ToString());

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenNugetConfigInstallSucceedsInTransaction(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            IToolPackage package = null;
            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                    packageId: TestPackageId,
                    verbosity: TestVerbosity,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    targetFramework: _testTargetframework,
                    isGlobalTool: true,
                    verifySignatures: false);

                transactionScope.Complete();
            }

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenNugetConfigInstallCreatesAnAssetFile(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            /*
              From mytool.dll to project.assets.json
               <root>/packageid/version/packageid/version/tools/framework/rid/mytool.dll
                                       /project.assets.json
             */
            var assetJsonPath = package.Command.Executable
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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir(
            bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var subDirectory = testDir.WithSubDirectories("sub");
            fileSystem.Directory.CreateDirectory(subDirectory.Value);

            var package = downloader.InstallPackage(
                new PackageLocation(rootConfigDirectory: subDirectory),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GivenAllButNoPackageVersionItReturnLatestStableVersion(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = (await downloader.GetNuGetVersionAsync(
                new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                cancellationToken: TestContext.CancellationToken)).version;

            package.OriginalVersion.Should().Be(TestPackageVersion);
        }

        [TestMethod]
        [DataRow(false, "1.0.0-rc*", TestPackageVersion)]
        [DataRow(true, "1.0.0-rc*", TestPackageVersion)]
        [DataRow(false, "1.*", TestPackageVersion)]
        [DataRow(true, "1.*", TestPackageVersion)]
        [DataRow(false, TestPackageVersion, TestPackageVersion)]
        [DataRow(true, TestPackageVersion, TestPackageVersion)]
        public async Task GivenASpecificVersionGetCorrectVersion(bool testMockBehaviorIsInSync, string requestedVersion, string expectedVersion)
        {

            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = (await downloader.GetNuGetVersionAsync(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config"),
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(requestedVersion),
                cancellationToken: TestContext.CancellationToken)).version;

            package.OriginalVersion.Should().Be(expectedVersion);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAllButNoPackageVersionItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(
                new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenASourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenARelativeSourcePathInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            //  CI seems to be getting an old version of the global.tool.console.demo package which targets .NET Core 2.1.  This may fix that
            ToolBuilder.RemovePackageFromGlobalPackages(Log, TestPackageId.ToString(), TestPackageVersion);

            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var relativePath = Path.GetRelativePath(testDir.Value, source);

            Log.WriteLine("Root path: " + testDir.Value);
            Log.WriteLine("Relative path: " + relativePath);
            Log.WriteLine("Current Directory: " + Directory.GetCurrentDirectory());

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] {relativePath}),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAUriSourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            //  CI seems to be getting an old version of the global.tool.console.demo package which targets .NET Core 2.1.  This may fix that
            ToolBuilder.RemovePackageFromGlobalPackages(Log, TestPackageId.ToString(), TestPackageVersion);

            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { new Uri(source).AbsoluteUri }), packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config"),
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenFailureAfterRestoreInstallWillRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

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
                        isGlobalTool: true,
                        verifySignatures: false);

                    FailedStepAfterSuccessRestore();
                    t.Complete();
                }
            };

            a.Should().Throw<GracefulException>().WithMessage("simulated error");

            AssertInstallRollBack(fileSystem, store);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenSecondInstallInATransactionTheFirstInstallShouldRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

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
                        isGlobalTool: true,
                        verifySignatures: false);

                    first.Should().NotThrow();

                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true,
                        verifySignatures: false);

                    t.Complete();
                }
            };

            a.Should().Throw<ToolPackageException>().Where(
                ex => ex.Message ==
                      string.Format(
                          CliStrings.ToolPackageConflictPackageId,
                          TestPackageId,
                          TestPackageVersion));

            AssertInstallRollBack(fileSystem, store);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenFailureWhenInstallLocalToolsItWillRollbackPackageVersion(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            static void FailedStepAfterSuccessDownload() => throw new GracefulException("simulated error");
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            var localToolDownloadDir = Path.Combine(new DirectoryPath(SettingsUtility.GetGlobalPackagesFolder(settings)).ToString().Trim('"'), TestPackageId.ToString());
            var localToolVersionDir = Path.Combine(localToolDownloadDir, TestPackageVersion.ToString());

            if (fileSystem.Directory.Exists(localToolVersionDir))
            {
                fileSystem.Directory.Delete(localToolVersionDir, true);
            }

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
                        verifySignatures: false);

                    fileSystem.Directory
                        .Exists(localToolDownloadDir)
                        .Should()
                        .BeTrue();

                    fileSystem.Directory
                        .Exists(localToolVersionDir)
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

            
            fileSystem
                .Directory
                .Exists(localToolVersionDir)
                .Should()
                .BeFalse();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenSecondInstallOfLocalToolItShouldNotThrowException(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

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
                        verifySignatures: false);


                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        verifySignatures: false);

                    t.Complete();
                }
            };

            a();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenSecondInstallWithoutATransactionTheFirstShouldNotRollback(bool testMockBehaviorIsInSync)
        {
            new RunExeCommand(Log, "dotnet", "nuget", "locals", "all", "--list")
                .Execute().Should().Pass();

            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            Action secondCall = () => downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            reporter.Lines.Should().BeEmpty();

            secondCall.Should().Throw<ToolPackageException>().Where(
                ex => ex.Message ==
                      string.Format(
                          CliStrings.ToolPackageConflictPackageId,
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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackage(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false,
                identiifer: testMockBehaviorIsInSync.ToString());

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAnInstalledPackageUninstallRollsbackWhenTransactionFails(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var package = downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackageWhenTransactionCommits(
            bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            var package = downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAPackageNameWithDifferentCaseItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: new PackageId("GlObAl.TooL.coNsoLe.DemO"),
                verbosity: TestVerbosity,
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        public async Task GivenConcurrentInstallationsOnlyOneInstallsThePackage()
        {
            var source = GetTestLocalFeedPath();
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);

            Task<IToolPackage>[] installs = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(
                    () => downloader.InstallPackage(
                        new PackageLocation(additionalFeeds: [source]),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true,
                        verifySignatures: false),
                    TestContext.CancellationToken))
                .ToArray();

            string expectedConflict = string.Format(
                CliStrings.ToolPackageConflictPackageId,
                TestPackageId,
                TestPackageVersion);
            IToolPackage[] results = await Task.WhenAll(installs.Select(async install =>
            {
                try
                {
                    return await install;
                }
                catch (ToolPackageException exception)
                {
                    exception.Message.Should().Be(expectedConflict);
                    return null;
                }
            }));

            IToolPackage package = results.Should().ContainSingle(result => result != null).Subject;
            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);
            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        public async Task GivenPackageExtractionInProgressThePackageIsNotReportedAsInstalled()
        {
            string source = GetTestLocalFeedPath();
            var root = TestAssetsManager.CreateTestDirectory();
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            var downloader = new TestableToolPackageDownloader(
                store,
                Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json"),
                root.Path);
            using var blockingDownloader = new BlockingNuGetPackageDownloader(downloader.CreatePackageDownloader());
            var packageVersion = NuGetVersion.Parse(TestPackageVersion);
            var packageSourceLocation = new PackageSourceLocation(
                additionalSourceFeeds: [source],
                basePath: root.Path);

            Task<NuGetVersion> download = Task.Run(
                () => downloader.DownloadAndExtract(
                    TestPackageId,
                    blockingDownloader,
                    packagesRoot.Value,
                    packageVersion,
                    packageSourceLocation),
                TestContext.CancellationToken);

            try
            {
                Task firstCompleted = await Task.WhenAny(blockingDownloader.ExtractionStarted, download);
                firstCompleted.Should().Be(
                    blockingDownloader.ExtractionStarted,
                    "the package download should reach extraction before completing");

                downloader.IsInstalled(TestPackageId, packageVersion, packagesRoot.Value).Should().BeFalse(
                    "the package completion marker must not be visible before extraction completes");
            }
            finally
            {
                blockingDownloader.ContinueExtraction();
            }

            (await download).Should().Be(packageVersion);
            downloader.IsInstalled(TestPackageId, packageVersion, packagesRoot.Value).Should().BeTrue();
        }

        [TestMethod]
        public void GivenACompleteCachedLocalToolInstallationDoesNotCreateALockFile()
        {
            string source = GetTestLocalFeedPath();
            var root = TestAssetsManager.CreateTestDirectory();
            string packagesRoot = Path.Combine(root.Path, "packages");
            var nugetConfigPath = new FilePath(Path.Combine(root.Path, "NuGet.config"));
            var fileSystem = new FileSystemWrapper();
            fileSystem.File.WriteAllText(nugetConfigPath.Value, FormatNuGetConfig(source, packagesRoot));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            var downloader = new ToolPackageDownloader(
                store,
                Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json"),
                root.Path);
            var packageLocation = new PackageLocation(nugetConfig: nugetConfigPath);
            var packageVersion = VersionRange.Parse(TestPackageVersion);

            downloader.InstallPackage(
                packageLocation,
                TestPackageId,
                TestVerbosity,
                packageVersion,
                _testTargetframework,
                isGlobalTool: false,
                verifySignatures: false);

            string lockDirectory = Path.Combine(packagesRoot, ToolPackageStoreAndQuery.LockDirectory);
            Directory.Delete(lockDirectory, recursive: true);

            UnixFileMode? originalMode = null;
            if (!OperatingSystem.IsWindows())
            {
                originalMode = File.GetUnixFileMode(packagesRoot);
                File.SetUnixFileMode(
                    packagesRoot,
                    UnixFileMode.UserRead | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            try
            {
                downloader.InstallPackage(
                    packageLocation,
                    TestPackageId,
                    TestVerbosity,
                    packageVersion,
                    _testTargetframework,
                    isGlobalTool: false,
                    verifySignatures: false);
            }
            finally
            {
                if (!OperatingSystem.IsWindows() && originalMode is UnixFileMode mode)
                {
                    File.SetUnixFileMode(packagesRoot, mode);
                }
            }

            Directory.Exists(lockDirectory).Should().BeFalse(
                "reusing a complete package must not require write access to the shared package root");
        }

        [TestMethod]
        public void GivenAStaleLockFileInstallationCanAcquireTheLock()
        {
            DirectoryPath root = new(TestAssetsManager.CreateTestDirectory().Path);
            NuGetVersion version = NuGetVersion.Parse(TestPackageVersion);
            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(root, TestPackageId, version);
            Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
            File.WriteAllText(lockFilePath, "stale");

            using FileStream installLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                root,
                TestPackageId,
                version,
                initialWaitTimeout: TimeSpan.Zero,
                waitTimeout: TimeSpan.FromSeconds(1));

            installLock.Should().NotBeNull();
        }

        [TestMethod]
        public void GivenAContendedLockInstallationTimesOut()
        {
            DirectoryPath root = new(TestAssetsManager.CreateTestDirectory().Path);
            NuGetVersion version = NuGetVersion.Parse(TestPackageVersion);
            using FileStream heldLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                root,
                TestPackageId,
                version);
            var reporter = new BufferedReporter();
            Action acquire = () =>
            {
                using FileStream _ = ToolPackageDownloaderBase.AcquireToolInstallLock(
                    root,
                    TestPackageId,
                    version,
                    initialWaitTimeout: TimeSpan.Zero,
                    waitTimeout: TimeSpan.FromMilliseconds(50),
                    errorReporter: reporter);
            };

            acquire.Should().Throw<ToolPackageException>()
                .WithMessage(string.Format(
                    CliStrings.ToolInstallationTimeout,
                    TestPackageId,
                    TestPackageVersion));
            string waitingMessage = reporter.Lines.Should().ContainSingle().Subject;
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                waitingMessage.Should().Contain($"Process {Environment.ProcessId} (");
            }
            else
            {
                waitingMessage.Should().Be(string.Format(
                    CliStrings.ToolInstallationWaiting,
                    TestPackageId,
                    TestPackageVersion));
            }
        }

        [TestMethod]
        public void GivenARootWithNonAsciiCharacterInstallSucceeds()
        {
            var surrogate = char.ConvertFromUtf32(int.Parse("2A601", NumberStyles.HexNumber));
            string nonAscii = "ab Ṱ̺̺̕o 田中さん åä," + surrogate;

            var root = TestAssetsManager.CreateTestDirectory(testName: nonAscii, identifier: "root");
            var reporter = new BufferedReporter();
            var fileSystem = new FileSystemWrapper();
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(root.Path));

            var nugetConfigPath = new FilePath(Path.Combine(root.Path, "NuGet.config"));

            WriteNugetConfigFile(fileSystem, nugetConfigPath, true);

            var testRuntimeJsonPath = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");

            var downloader = new ToolPackageDownloader(
                store: store,
                testRuntimeJsonPath
                );

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, store);

            new ToolPackageUninstaller(store).Uninstall(package.PackageDirectory);
        }


        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        // repro https://github.com/dotnet/cli/issues/9409
        public void GivenAComplexVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.InstallPackage(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config"),
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse("1.0.0-rc*"),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow(false)]
        [DataRow(true)]
        // repro https://github.com/dotnet/cli/issues/10101
        public void GivenAPackageWithCasingAndenUSPOSIXInstallSucceeds(bool testMockBehaviorIsInSync)
        {

            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var packageId = new PackageId("Global.Tool.Console.Demo.With.Casing");
            var packageVersion = "2.0.4";

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            CultureInfo currentCultureBefore = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US-POSIX");
                IToolPackage package = null;
                Action action = () => package = downloader.InstallPackage(
                    new PackageLocation(
                        nugetConfig: testDir.WithFile("NuGet.config"),
                        additionalFeeds: new[] { emptySource }),
                    packageId: packageId,
                    verbosity: TestVerbosity,
                    versionRange: VersionRange.Parse(packageVersion),
                    targetFramework: _testTargetframework,
                    isGlobalTool: true);

                action.Should().NotThrow<ToolConfigurationException>();

                fileSystem.File.Exists(package.Command.Executable.Value).Should().BeTrue($"{package.Command.Executable.Value} should exist");

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

            package.Command.Should().NotBeNull();
            fileSystem.File.Exists(package.Command.Executable.Value).Should()
                .BeTrue($"{package.Command.Executable.Value} should exist");
            package.Command.Executable.Value.Should().Contain(store.Root.Value);
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
                .NotContain(e =>
                    Path.GetFileName(e) != ToolPackageStoreAndQuery.LockDirectory &&
                    Path.GetFileName(e) != ToolPackageStoreAndQuery.StagingDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        private (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader, IToolPackageUninstaller, BufferedReporter, IFileSystem, DirectoryPath testDir
            ) Setup(
                bool useMock,
                bool includeLocalFeedInNugetConfig,
                [CallerMemberName] string callingMethod = "",
                string identiifer = null)
        {
            var root = new DirectoryPath(TestAssetsManager.CreateTestDirectory(callingMethod, identifier: useMock.ToString() + identiifer).Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageDownloader downloader;
            IToolPackageUninstaller uninstaller;

            var nugetConfigPath = new FilePath(Path.Combine(root.Value, "NuGet.config"));

            var toolsRoot = root.WithSubDirectories("tools");


            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                var frameworksMap = new Dictionary<PackageId, IEnumerable<NuGetFramework>>()
                        { {TestPackageId, TestFrameworks } };

                WriteNugetConfigFile(fileSystem, nugetConfigPath, includeLocalFeedInNugetConfig);
                var storeAndQuery = new ToolPackageStoreAndQuery(toolsRoot, fileSystem);
                store = storeAndQuery;
                storeQuery = storeAndQuery;
                downloader = new ToolPackageDownloaderMock2(storeAndQuery,
                    runtimeJsonPathForTests: SdkTestContext.GetRuntimeGraphFilePath(),
                    currentWorkingDirectory: root.Value,
                    fileSystem);

                uninstaller = new ToolPackageUninstallerMock(fileSystem, storeAndQuery);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                WriteNugetConfigFile(fileSystem, nugetConfigPath, includeLocalFeedInNugetConfig);
                var toolPackageStore = new ToolPackageStoreAndQuery(toolsRoot);
                store = toolPackageStore;
                storeQuery = toolPackageStore;
                var testRuntimeJsonPath = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");
                downloader = new ToolPackageDownloader(store, testRuntimeJsonPath, root.Value);
                uninstaller = new ToolPackageUninstaller(store);
            }

            store.Root.Value.Should().Be(Path.GetFullPath(toolsRoot.Value));

            return (store, storeQuery, downloader, uninstaller, reporter, fileSystem, root);
        }

        private static void WriteNugetConfigFile(IFileSystem fileSystem, FilePath? filePath, bool includeLocalFeedPath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                localFeedPath: includeLocalFeedPath ? GetTestLocalFeedPath() : null));
        }

        public static string FormatNuGetConfig(string localFeedPath, string globalPackagesFolder = null)
        {
            string localFeed = string.IsNullOrEmpty(localFeedPath)
                ? string.Empty
                : $"<add key=\"Test Source\" value=\"{localFeedPath}\" />";
            string globalPackagesFolderConfig = string.IsNullOrEmpty(globalPackagesFolder)
                ? string.Empty
                : $"""
                  <config>
                    <add key="globalPackagesFolder" value="{globalPackagesFolder}" />
                  </config>
                  """;

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
{localFeed}
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
{globalPackagesFolderConfig}
</configuration>";
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new("global.tool.console.demo");
        private static readonly IEnumerable<NuGetFramework> TestFrameworks = new NuGetFramework[] { NuGetFramework.Parse(ToolPackageDownloaderMock2.DefaultTargetFramework) };
        private static readonly VerbosityOptions TestVerbosity = new VerbosityOptions();

        public ToolPackageDownloaderTests() { }

        private sealed class TestableToolPackageDownloader : ToolPackageDownloader
        {
            public TestableToolPackageDownloader(
                IToolPackageStore store,
                string runtimeJsonPathForTests,
                string currentWorkingDirectory)
                : base(store, runtimeJsonPathForTests, currentWorkingDirectory)
            {
            }

            public INuGetPackageDownloader CreatePackageDownloader() =>
                CreateNuGetPackageDownloader(
                    verifySignatures: false,
                    TestVerbosity,
                    restoreActionConfig: null);

            public NuGetVersion DownloadAndExtract(
                PackageId packageId,
                INuGetPackageDownloader packageDownloader,
                string packagesRootPath,
                NuGetVersion packageVersion,
                PackageSourceLocation packageSourceLocation) =>
                DownloadAndExtractPackage(
                    packageId,
                    packageDownloader,
                    packagesRootPath,
                    packageVersion,
                    packageSourceLocation,
                    TestVerbosity);

            public bool IsInstalled(PackageId packageId, NuGetVersion packageVersion, string packagesRootPath) =>
                IsPackageInstalled(packageId, packageVersion, packagesRootPath);
        }

        private sealed class BlockingNuGetPackageDownloader : INuGetPackageDownloader, IDisposable
        {
            private readonly INuGetPackageDownloader _inner;
            private readonly ManualResetEventSlim _continueExtraction = new(initialState: false);
            private readonly TaskCompletionSource _extractionStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingNuGetPackageDownloader(INuGetPackageDownloader inner)
            {
                _inner = inner;
            }

            public Task ExtractionStarted => _extractionStarted.Task;

            public void ContinueExtraction() => _continueExtraction.Set();

            public async Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
            {
                _extractionStarted.SetResult();
                _continueExtraction.Wait();
                return await _inner.ExtractPackageAsync(packagePath, targetFolder);
            }

            public Task<string> DownloadPackageAsync(
                PackageId packageId,
                NuGetVersion packageVersion = null,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false,
                bool? includeUnlisted = null,
                DirectoryPath? downloadFolder = null,
                PackageSourceMapping packageSourceMapping = null) =>
                _inner.DownloadPackageAsync(
                    packageId,
                    packageVersion,
                    packageSourceLocation,
                    includePreview,
                    includeUnlisted,
                    downloadFolder,
                    packageSourceMapping);

            public Task<string> GetPackageUrl(
                PackageId packageId,
                NuGetVersion packageVersion = null,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                _inner.GetPackageUrl(packageId, packageVersion, packageSourceLocation, includePreview);

            public Task<NuGetVersion> GetLatestPackageVersion(
                PackageId packageId,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                _inner.GetLatestPackageVersion(packageId, packageSourceLocation, includePreview);

            public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(
                PackageId packageId,
                int numberOfResults,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                _inner.GetLatestPackageVersions(packageId, numberOfResults, packageSourceLocation, includePreview);

            public Task<NuGetVersion> GetBestPackageVersionAsync(
                PackageId packageId,
                VersionRange versionRange,
                PackageSourceLocation packageSourceLocation = null) =>
                _inner.GetBestPackageVersionAsync(packageId, versionRange, packageSourceLocation);

            public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(
                PackageId packageId,
                VersionRange versionRange,
                PackageSourceLocation packageSourceLocation = null,
                CancellationToken cancellationToken = default) =>
                _inner.GetBestPackageVersionAndSourceAsync(
                    packageId,
                    versionRange,
                    packageSourceLocation,
                    cancellationToken);

            public void Dispose() => _continueExtraction.Dispose();
        }

        [TestMethod]
        public void GivenAToolWithHigherFrameworkItShowsAppropriateErrorMessage()
        {
            // Create a mock tool package with net99.0 framework to simulate a tool requiring a higher .NET version
            var testDir = TestAssetsManager.CreateTestDirectory();
            var fileSystem = new FileSystemWrapper();
            var packageId = new PackageId("test.tool.higher.framework");
            var packageVersion = new NuGetVersion("1.0.0");
            var packageRoot = new DirectoryPath(testDir.Path).WithSubDirectories(".store", packageId.ToString(), packageVersion.ToNormalizedString());

            // Create the package directory structure with net99.0 framework
            var toolsPath = Path.Combine(packageRoot.Value, "tools", "net99.0", "any");
            fileSystem.Directory.CreateDirectory(toolsPath);

            // Create DotnetToolSettings.xml
            var settingsContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<DotNetCliTool Version=""1"">
  <Commands>
    <Command Name=""test-tool"" EntryPoint=""test.dll"" Runner=""dotnet"" />
  </Commands>
</DotNetCliTool>";
            fileSystem.File.WriteAllText(Path.Combine(toolsPath, "DotnetToolSettings.xml"), settingsContent);

            // Create a dummy assembly file
            fileSystem.File.WriteAllText(Path.Combine(toolsPath, "test.dll"), "dummy");

            // Create an empty asset file (simulating NuGet restore with no compatible frameworks)
            var assetFilePath = Path.Combine(packageRoot.Value, "project.assets.json");
            var currentFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
            var assetFileContents = $$"""
                {
                  "version": 3,
                  "targets": {
                    "{{currentFramework}}/{{RuntimeInformation.RuntimeIdentifier}}": {
                      "{{packageId}}/{{packageVersion}}": {
                        "type": "package",
                        "tools": {
                        }
                      }
                    }
                  },
                  "libraries": {},
                  "projectFileDependencyGroups": {}
                }
                """;
            fileSystem.File.WriteAllText(assetFilePath, assetFileContents);

            // Try to create a ToolPackageInstance, which should throw an informative error
            Action action = () =>
            {
                _ = new ToolPackageInstance(
                    packageId,
                    packageVersion,
                    new DirectoryPath(testDir.Path).WithSubDirectories(".store"),
                    packageRoot,
                    fileSystem);
            };

            action.Should().Throw<GracefulException>()
                .WithMessage("*requires a higher version of .NET*")
                .WithMessage("*.NET 99*");
        }
    }
}
