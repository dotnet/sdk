// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                    verifySignatures: false,
                    cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GivenAllButNoPackageVersionItReturnLatestStableVersion(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.GetNuGetVersion(
                new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                cancellationToken: TestContext.CancellationToken).version;

            package.OriginalVersion.Should().Be(TestPackageVersion);
        }

        [TestMethod]
        [DataRow(false, "1.0.0-rc*", TestPackageVersion)]
        [DataRow(true, "1.0.0-rc*", TestPackageVersion)]
        [DataRow(false, "1.*", TestPackageVersion)]
        [DataRow(true, "1.*", TestPackageVersion)]
        [DataRow(false, TestPackageVersion, TestPackageVersion)]
        [DataRow(true, TestPackageVersion, TestPackageVersion)]
        public void GivenASpecificVersionGetCorrectVersion(bool testMockBehaviorIsInSync, string requestedVersion, string expectedVersion)
        {

            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.GetNuGetVersion(new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config"),
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(requestedVersion),
                cancellationToken: TestContext.CancellationToken).version;

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                new PackageLocation(additionalFeeds: new[] { relativePath }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);

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
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);

                    first.Should().NotThrow();

                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true,
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);

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
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);

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
        public async Task GivenConcurrentLocalInstallWhenFirstTransactionRollsBackSecondInstallRemains()
        {
            var (_, _, downloader, _, _, fileSystem, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);
            var mockDownloader = (ToolPackageDownloaderMock2)downloader;
            var firstInstallCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowRollback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int downloadCount = 0;
            mockDownloader.BeforeDownloadCallback = _ => Interlocked.Increment(ref downloadCount);
            CancellationToken cancellationToken = TestContext.CancellationToken;

            Task firstInstall = Task.Run(async () =>
            {
                using var transaction = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero,
                    TransactionScopeAsyncFlowOption.Enabled);

                await InstallLocalPackageAsync(downloader, TestPackageId, cancellationToken);
                firstInstallCompleted.SetResult();
                await allowRollback.Task.WaitAsync(cancellationToken);
            }, cancellationToken);

            await firstInstallCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            Task<IToolPackage> secondInstall = Task.Run(
                () => InstallLocalPackageAsync(downloader, TestPackageId, cancellationToken),
                cancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                secondInstall.IsCompleted.Should().BeFalse();
            }
            finally
            {
                allowRollback.SetResult();
            }

            await firstInstall;
            IToolPackage package = await secondInstall;

            downloadCount.Should().Be(2);
            fileSystem.Directory.Exists(package.PackageDirectory.Value).Should().BeTrue();
        }

        [TestMethod]
        public async Task GivenLocalPackageReadWhenInstallRollsBackReadWaitsAndReturnsNoPackage()
        {
            var (_, _, downloader, _, _, _, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);
            var firstInstallCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowRollback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken cancellationToken = TestContext.CancellationToken;

            Task firstInstall = Task.Run(async () =>
            {
                using var transaction = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero,
                    TransactionScopeAsyncFlowOption.Enabled);

                await InstallLocalPackageAsync(downloader, TestPackageId, cancellationToken);
                firstInstallCompleted.SetResult();
                await allowRollback.Task.WaitAsync(cancellationToken);
            }, cancellationToken);

            await firstInstallCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            Task<IToolPackage> readPackage = downloader.TryGetDownloadedToolAsync(
                TestPackageId,
                NuGetVersion.Parse(TestPackageVersion),
                _testTargetframework,
                TestVerbosity,
                TestContext.CancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                readPackage.IsCompleted.Should().BeFalse();
            }
            finally
            {
                allowRollback.SetResult();
            }

            await firstInstall;
            (await readPackage).Should().BeNull();
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
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);


                    downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        verifySignatures: false,
                        cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            Action secondCall = () => downloader.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                verifySignatures: false,
                cancellationToken: TestContext.CancellationToken);

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
                    isGlobalTool: true,
                    cancellationToken: TestContext.CancellationToken);

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
                .NotContain(e => Path.GetFileName(e) != ToolPackageStoreAndQuery.StagingDirectory
                    && Path.GetFileName(e) != ToolPackageStoreAndQuery.LockDirectory);

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
                string identiifer = null,
                TimeSpan? lockInitialWaitTimeout = null,
                TimeSpan? lockWaitTimeout = null)
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
                    fileSystem,
                    lockInitialWaitTimeout,
                    lockWaitTimeout);

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

        public static string FormatNuGetConfig(string localFeedPath)
        {
            string localFeed = string.IsNullOrEmpty(localFeedPath)
                ? string.Empty
                : $"<add key=\"Test Source\" value=\"{localFeedPath}\" />";

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
{localFeed}
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GivenConcurrentInstallationsTheyDoNotConflict(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: false);

            // Run multiple installations concurrently using Task.Run
            // This tests that the mutex prevents file system conflicts during package download/extraction
            var tasks = new List<Task<IToolPackage>>();
            CancellationToken cancellationToken = TestContext.CancellationToken;
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    return downloader.InstallPackage(
                        new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        verbosity: TestVerbosity,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        isGlobalTool: true,
                        verifySignatures: false,
                        cancellationToken: cancellationToken);
                }));
            }

            string expectedConflict = string.Format(
                CliStrings.ToolPackageConflictPackageId,
                TestPackageId,
                TestPackageVersion);
            IToolPackage[] results = await Task.WhenAll(tasks.Select(async task =>
            {
                try
                {
                    return await task;
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
        public async Task GivenDifferentLocalToolsTheyUseSeparateAssetDirectories()
        {
            var (_, _, downloader, _, _, _, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);
            var mockDownloader = (ToolPackageDownloaderMock2)downloader;

            await Task.WhenAll(
                InstallLocalPackageAsync(downloader, new PackageId("test.tool.a"), TestContext.CancellationToken),
                InstallLocalPackageAsync(downloader, new PackageId("test.tool.b"), TestContext.CancellationToken));

            mockDownloader.AssetFilePaths.Should().HaveCount(2);
            mockDownloader.AssetFilePaths
                .Select(Path.GetDirectoryName)
                .Should().OnlyHaveUniqueItems();
        }

        [TestMethod]
        public async Task GivenDifferentLocalToolsWithTheSameRidPackageTheySerializeRidPackageDownload()
        {
            var (_, _, downloader, _, _, _, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);
            var mockDownloader = (ToolPackageDownloaderMock2)downloader;
            PackageId ridPackageId = new("test.tool.shared.rid");
            mockDownloader.RidSpecificPackages = new Dictionary<string, PackageIdentity>
            {
                [RuntimeInformation.RuntimeIdentifier] = new PackageIdentity(
                    ridPackageId.ToString(),
                    version: null)
            };

            using CountdownEvent parentDownloadsReady = new(2);
            using ManualResetEventSlim firstRidDownloadStarted = new();
            using ManualResetEventSlim secondRidDownloadStarted = new();
            using ManualResetEventSlim releaseRidDownload = new();
            int ridDownloadCount = 0;
            CancellationToken cancellationToken = TestContext.CancellationToken;
            mockDownloader.BeforeDownloadCallback = packageId =>
            {
                if (packageId.ToString() != ridPackageId.ToString())
                {
                    parentDownloadsReady.Signal();
                    parentDownloadsReady.Wait(TimeSpan.FromSeconds(10), cancellationToken).Should().BeTrue();
                    return;
                }

                if (Interlocked.Increment(ref ridDownloadCount) == 1)
                {
                    firstRidDownloadStarted.Set();
                    releaseRidDownload.Wait(TimeSpan.FromSeconds(10), cancellationToken).Should().BeTrue();
                }
                else
                {
                    secondRidDownloadStarted.Set();
                }
            };

            Task<IToolPackage> firstInstall = Task.Run(
                () => InstallLocalPackageAsync(downloader, new PackageId("test.tool.a"), cancellationToken),
                cancellationToken);
            Task<IToolPackage> secondInstall = Task.Run(
                () => InstallLocalPackageAsync(downloader, new PackageId("test.tool.b"), cancellationToken),
                cancellationToken);

            try
            {
                firstRidDownloadStarted.Wait(TimeSpan.FromSeconds(10), cancellationToken).Should().BeTrue();
                secondRidDownloadStarted.Wait(TimeSpan.FromMilliseconds(250), cancellationToken).Should().BeFalse();
            }
            finally
            {
                releaseRidDownload.Set();
            }

            await Task.WhenAll(firstInstall, secondInstall);
            ridDownloadCount.Should().Be(1);
        }

        [TestMethod]
        public void GivenAContendedFileLockTimesOut()
        {
            var (store, _, downloader, _, _, _, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false,
                lockInitialWaitTimeout: TimeSpan.Zero,
                lockWaitTimeout: TimeSpan.FromMilliseconds(50));
            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(
                store.Root,
                TestPackageId,
                NuGetVersion.Parse(TestPackageVersion));

            WithHeldFileLock(lockFilePath, () =>
            {
                Action install = () => InstallGlobalPackage(downloader, CancellationToken.None);

                install.Should().Throw<ToolPackageException>()
                    .WithMessage(string.Format(
                        CliStrings.ToolInstallationTimeout,
                        TestPackageId,
                        TestPackageVersion));
            });
        }

        [TestMethod]
        public void GivenAContendedFileLockCancellationStopsWaiting()
        {
            var (store, _, downloader, _, _, _, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false,
                lockInitialWaitTimeout: TimeSpan.Zero);
            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(
                store.Root,
                TestPackageId,
                NuGetVersion.Parse(TestPackageVersion));

            WithHeldFileLock(lockFilePath, () =>
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                Action install = () => InstallGlobalPackage(downloader, cancellationTokenSource.Token);

                install.Should().Throw<OperationCanceledException>();
            });
        }

        [TestMethod]
        public void GivenAStaleFileLockInstallationContinues()
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, _) = Setup(
                useMock: true,
                includeLocalFeedInNugetConfig: false);
            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(
                store.Root,
                TestPackageId,
                NuGetVersion.Parse(TestPackageVersion));

            Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
            File.WriteAllText(lockFilePath, "stale");

            IToolPackage package = InstallGlobalPackage(downloader, CancellationToken.None);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);
            File.Exists(lockFilePath).Should().BeTrue();
            uninstaller.Uninstall(package.PackageDirectory);
        }

        [TestMethod]
        public async Task StoreLockIsHeldUntilAmbientTransactionCompletes()
        {
            DirectoryPath root = new(TestAssetsManager.CreateTestDirectory().Path);
            var firstLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowRollback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationToken cancellationToken = TestContext.CancellationToken;
            Task firstLock = Task.Run(async () =>
            {
                using var transaction = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero,
                    TransactionScopeAsyncFlowOption.Enabled);

                await ToolPackageDownloaderBase.ExecuteWithToolInstallStoreLockAsync(
                    root,
                    TestPackageId,
                    TestPackageVersion,
                    cancellationToken,
                    () =>
                    {
                        firstLockAcquired.SetResult();
                        return Task.FromResult(true);
                    });

                await allowRollback.Task.WaitAsync(cancellationToken);
            }, cancellationToken);

            await firstLockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            Task<bool> secondLock = Task.Run(
                () => ToolPackageDownloaderBase.ExecuteWithToolInstallStoreLockAsync(
                    root,
                    TestPackageId,
                    TestPackageVersion,
                    cancellationToken,
                    () =>
                    {
                        secondLockAcquired.SetResult();
                        return Task.FromResult(true);
                    }),
                cancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                secondLockAcquired.Task.IsCompleted.Should().BeFalse();
            }
            finally
            {
                allowRollback.SetResult();
            }

            await firstLock;
            (await secondLock).Should().BeTrue();
            secondLockAcquired.Task.IsCompletedSuccessfully.Should().BeTrue();
        }

        [TestMethod]
        public void ToolInstallLockFilePathIsBoundedAndScopedToInstallIdentity()
        {
            DirectoryPath root = new(TestAssetsManager.CreateTestDirectory().Path);
            DirectoryPath otherRoot = new(Path.Combine(root.Value, "other"));
            var version = NuGetVersion.Parse(TestPackageVersion);

            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(root, TestPackageId, version);

            Path.GetFileName(lockFilePath).Should().HaveLength(64 + ".lock".Length);
            Path.GetDirectoryName(lockFilePath).Should().Be(
                Path.Combine(Path.GetFullPath(root.Value), ToolPackageStoreAndQuery.LockDirectory));
            ToolPackageDownloaderBase.GetToolInstallLockFilePath(
                    new DirectoryPath(root.Value + Path.DirectorySeparatorChar),
                    new PackageId(TestPackageId.ToString().ToUpperInvariant()),
                    NuGetVersion.Parse(TestPackageVersion.ToUpperInvariant()))
                .Should().Be(lockFilePath);
            ToolPackageDownloaderBase.GetToolInstallLockFilePath(otherRoot, TestPackageId, version)
                .Should().NotBe(lockFilePath);
            ToolPackageDownloaderBase.GetToolInstallLockFilePath(root, TestPackageId, NuGetVersion.Parse("1.0.5"))
                .Should().NotBe(lockFilePath);
        }

        [TestMethod]
        public void ToolInstallLockRetriesOnlyContentionErrors()
        {
            int contentionError = OperatingSystem.IsWindows()
                ? unchecked((int)0x80070020)
                : OperatingSystem.IsMacOS() ? 35 : 11;

            ToolPackageDownloaderBase
                .IsToolInstallLockContention(new IOException("contended", contentionError))
                .Should().BeTrue();
            ToolPackageDownloaderBase
                .IsToolInstallLockContention(new IOException("failed", unchecked((int)0x80070005)))
                .Should().BeFalse();
        }

        private Task<IToolPackage> InstallLocalPackageAsync(
            IToolPackageDownloader downloader,
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return downloader.InstallPackageAsync(
                new PackageLocation(additionalFeeds: [GetTestLocalFeedPath()]),
                packageId,
                TestVerbosity,
                VersionRange.Parse(TestPackageVersion),
                _testTargetframework,
                verifySignatures: false,
                cancellationToken: cancellationToken);
        }

        private IToolPackage InstallGlobalPackage(
            IToolPackageDownloader downloader,
            CancellationToken cancellationToken)
        {
            return downloader.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { GetTestLocalFeedPath() }),
                packageId: TestPackageId,
                verbosity: TestVerbosity,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                isGlobalTool: true,
                verifySignatures: false,
                cancellationToken: cancellationToken);
        }

        private static void WithHeldFileLock(string lockFilePath, Action action)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
            using var lockFile = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            action();
        }
    }
}
