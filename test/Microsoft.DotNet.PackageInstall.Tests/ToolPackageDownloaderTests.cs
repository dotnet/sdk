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
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
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

    [Collection(nameof(TestToolBuilderCollection))]
    public class ToolPackageDownloaderTests : SdkTest, IClassFixture<DotnetEnvironmentTestFixture>
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionItReturnLatestStableVersion(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, downloader, uninstaller, reporter, fileSystem, testDir) = Setup(
                useMock: testMockBehaviorIsInSync,
                includeLocalFeedInNugetConfig: true);

            var package = downloader.GetNuGetVersion(
                new PackageLocation(nugetConfig: testDir.WithFile("NuGet.config")),
                packageId: TestPackageId,
                verbosity: TestVerbosity).version;

            package.OriginalVersion.Should().Be(TestPackageVersion);
        }

        [Theory]
        [InlineData(false, "1.0.0-rc*", TestPackageVersion)]
        [InlineData(true, "1.0.0-rc*", TestPackageVersion)]
        [InlineData(false, "1.*", TestPackageVersion)]
        [InlineData(true, "1.*", TestPackageVersion)]
        [InlineData(false, TestPackageVersion, TestPackageVersion)]
        [InlineData(true, TestPackageVersion, TestPackageVersion)]
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
                versionRange: VersionRange.Parse(requestedVersion)).version;

            package.OriginalVersion.Should().Be(expectedVersion);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailureAfterInstallingLocalToolThePublishedPackageIsNotRolledBack(bool testMockBehaviorIsInSync)
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
                .BeTrue();

            fileSystem.Directory.Delete(localToolVersionDir, true);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [Fact]
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
                    CancellationToken.None))
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

        [Fact]
        public async Task GivenPackageExtractionInProgressThePackageIsNotReportedAsInstalled()
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory();
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            using var downloader = new TestableToolPackageDownloader(
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
                CancellationToken.None);

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

        [Fact]
        public async Task GivenPackageHashPublicationInProgressThePackageIsNotReportedAsInstalled()
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory();
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            using var downloader = new TestableToolPackageDownloader(
                store,
                Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json"),
                root.Path,
                pauseHashPublication: true);
            var packageVersion = NuGetVersion.Parse(TestPackageVersion);
            var packageSourceLocation = new PackageSourceLocation(
                additionalSourceFeeds: [source],
                basePath: root.Path);

            Task<NuGetVersion> download = Task.Run(
                () => downloader.DownloadAndExtract(
                    TestPackageId,
                    downloader.CreatePackageDownloader(),
                    packagesRoot.Value,
                    packageVersion,
                    packageSourceLocation),
                CancellationToken.None);

            try
            {
                Task firstCompleted = await Task.WhenAny(downloader.HashMoveStarted, download);
                firstCompleted.Should().Be(
                    downloader.HashMoveStarted,
                    "the package download should reach completion-marker publication before completing");

                string hashPath = new VersionFolderPathResolver(packagesRoot.Value)
                    .GetHashPath(TestPackageId.ToString(), packageVersion);
                File.Exists(hashPath).Should().BeFalse(
                    "the final completion-marker path must not exist until the complete marker is atomically moved");
                downloader.IsInstalled(TestPackageId, packageVersion, packagesRoot.Value).Should().BeFalse();
            }
            finally
            {
                downloader.ContinueHashMove();
            }

            (await download).Should().Be(packageVersion);
            downloader.IsInstalled(TestPackageId, packageVersion, packagesRoot.Value).Should().BeTrue();
            string publishedHashPath = new VersionFolderPathResolver(packagesRoot.Value)
                .GetHashPath(TestPackageId.ToString(), packageVersion);
            var pathResolver = new VersionFolderPathResolver(packagesRoot.Value);
            string nupkgPath = pathResolver.GetPackageFilePath(TestPackageId.ToString(), packageVersion);
            using FileStream packageStream = File.OpenRead(nupkgPath);
            string expectedPackageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(packageStream));
            File.ReadAllText(publishedHashPath).Should().Be(expectedPackageHash);

            packageStream.Position = 0;
            using PackageArchiveReader reader = new(packageStream);
            string expectedContentHash = reader.GetContentHash(CancellationToken.None, () => expectedPackageHash);
            string metadataPath = pathResolver.GetNupkgMetadataPath(TestPackageId.ToString(), packageVersion);
            NupkgMetadataFileFormat.Read(metadataPath).ContentHash.Should().Be(expectedContentHash);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GivenMetadataPublicationFailsItPreservesThePackageForAnotherInstall(bool metadataPublished)
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory(identifier: metadataPublished.ToString());
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            string runtimeJsonPath = Path.Combine(
                SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
                "RuntimeIdentifierGraph.json");
            var packageVersion = NuGetVersion.Parse(TestPackageVersion);
            var packageSourceLocation = new PackageSourceLocation(additionalSourceFeeds: [source], basePath: root.Path);
            var pathResolver = new VersionFolderPathResolver(packagesRoot.Value);
            string hashPath = pathResolver.GetHashPath(TestPackageId.ToString(), packageVersion);
            string metadataPath = pathResolver.GetNupkgMetadataPath(TestPackageId.ToString(), packageVersion);
            var metadataPublicationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var continuePublication = new ManualResetEventSlim(initialState: false);
            Action failPublication = () =>
            {
                metadataPublicationReached.SetResult();
                continuePublication.Wait();
                throw new IOException("Simulated metadata publication failure.");
            };
            using var downloader = new TestableToolPackageDownloader(
                new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools"))),
                runtimeJsonPath,
                root.Path,
                beforeMetadataPublication: metadataPublished ? null : failPublication,
                afterMetadataPublication: metadataPublished ? failPublication : null);
            using var reusingDownloader = new TestableToolPackageDownloader(
                new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "reusing-tools"))),
                runtimeJsonPath,
                root.Path);

            Task<NuGetVersion> download = Task.Run(() =>
            {
                using FileStream installLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                    packagesRoot, TestPackageId, packageVersion);
                return downloader.DownloadAndExtract(
                    TestPackageId,
                    downloader.CreatePackageDownloader(),
                    packagesRoot.Value,
                    packageVersion,
                    packageSourceLocation);
            });

            Task<IToolPackage> reuse;
            string publishedHash;
            try
            {
                Task firstCompleted = await Task.WhenAny(metadataPublicationReached.Task, download)
                    .WaitAsync(TimeSpan.FromSeconds(30));
                firstCompleted.Should().Be(metadataPublicationReached.Task);
                publishedHash = File.ReadAllText(hashPath);
                File.Exists(metadataPath).Should().Be(metadataPublished);
                reuse = Task.Run(() => reusingDownloader.InstallFromCache(
                    TestPackageId,
                    packageVersion,
                    packagesRoot,
                    packageSourceLocation,
                    _testTargetframework));
                if (metadataPublished)
                {
                    IToolPackage acceptedPackage = await reuse.WaitAsync(TimeSpan.FromSeconds(30));
                    File.Exists(acceptedPackage.Command.Executable.Value).Should().BeTrue();
                }
                else
                {
                    (await Task.WhenAny(reuse, Task.Delay(TimeSpan.FromMilliseconds(250))))
                        .Should().NotBe(reuse, "NuGet must wait for the writer before it can repair the missing metadata");
                }
            }
            finally
            {
                continuePublication.Set();
            }

            Func<Task> awaitDownload = async () => await download;
            await awaitDownload.Should().ThrowAsync<IOException>()
                .WithMessage("Simulated metadata publication failure.");
            IToolPackage reusedPackage = await reuse.WaitAsync(TimeSpan.FromSeconds(30));
            File.Exists(reusedPackage.Command.Executable.Value).Should().BeTrue(
                "metadata publication failure must not delete a package accepted by another install");
            File.ReadAllText(hashPath).Should().Be(publishedHash);
            File.Exists(metadataPath).Should().BeTrue(
                "NuGet must preserve published metadata or repair it after the writer releases its lock");
            var packageDownloader = new StaticPackagePathNuGetPackageDownloader("must-not-download");
            reusingDownloader.DownloadAndExtract(
                TestPackageId,
                packageDownloader,
                packagesRoot.Value,
                packageVersion,
                packageSourceLocation).Should().Be(packageVersion);
            packageDownloader.ExtractCalled.Should().BeFalse();
            File.ReadAllText(hashPath).Should().Be(publishedHash);
            File.Exists(reusedPackage.Command.Executable.Value).Should().BeTrue();
            using FileStream packageStream = File.OpenRead(
                pathResolver.GetPackageFilePath(TestPackageId.ToString(), packageVersion));
            using PackageArchiveReader reader = new(packageStream);
            NupkgMetadataFileFormat.Read(metadataPath).ContentHash.Should().Be(
                reader.GetContentHash(CancellationToken.None, () => publishedHash));
        }

        [Fact]
        public async Task GivenNuGetsPackageLockToolInstallationWaitsForTheCanonicalWriter()
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory();
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            using var downloader = new TestableToolPackageDownloader(
                store,
                Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json"),
                root.Path);
            using var blockingDownloader = new BlockingNuGetPackageDownloader(downloader.CreatePackageDownloader());
            var packageVersion = NuGetVersion.Parse(TestPackageVersion);
            var packageSourceLocation = new PackageSourceLocation(
                additionalSourceFeeds: [source],
                basePath: root.Path);
            var pathResolver = new VersionFolderPathResolver(packagesRoot.Value);
            string nupkgPath = pathResolver.GetPackageFilePath(TestPackageId.ToString(), packageVersion);
            var nugetLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseNuGetLock = new ManualResetEventSlim(initialState: false);

            Task canonicalWriter = Task.Run(
                () => ConcurrencyUtilities.ExecuteWithFileLocked(
                    nupkgPath,
                    () =>
                    {
                        nugetLockAcquired.SetResult();
                        releaseNuGetLock.Wait(CancellationToken.None);
                    }),
                CancellationToken.None);
            await nugetLockAcquired.Task;

            Task<NuGetVersion> download = Task.Run(
                () => downloader.DownloadAndExtract(
                    TestPackageId,
                    blockingDownloader,
                    packagesRoot.Value,
                    packageVersion,
                    packageSourceLocation),
                CancellationToken.None);

            Task firstCompleted = await Task.WhenAny(
                blockingDownloader.ExtractionStarted,
                Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None));
            releaseNuGetLock.Set();
            await canonicalWriter;
            blockingDownloader.ContinueExtraction();

            (await download).Should().Be(packageVersion);
            firstCompleted.Should().NotBe(
                blockingDownloader.ExtractionStarted,
                "tool publication must participate in NuGet's target-nupkg lock");
            File.Exists(pathResolver.GetNupkgMetadataPath(TestPackageId.ToString(), packageVersion)).Should().BeTrue(
                "NuGet's canonical completion marker prevents a later writer from clearing the published package");
        }

        [Fact]
        public void GivenADownloadedPackageWithAnotherIdentityItIsRejectedBeforeExtraction()
        {
            var root = _testAssetsManager.CreateTestDirectory();
            var packagesRoot = new DirectoryPath(Path.Combine(root.Path, "packages"));
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "tools")));
            using var downloader = new TestableToolPackageDownloader(
                store,
                Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json"),
                root.Path);
            var packageVersion = NuGetVersion.Parse(TestPackageVersion);
            var pathResolver = new VersionFolderPathResolver(packagesRoot.Value);
            string nupkgPath = pathResolver.GetPackageFilePath(TestPackageId.ToString(), packageVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath)!);
            var actualIdentity = new PackageIdentity("unexpected.tool.package", NuGetVersion.Parse("9.9.9"));
            var packageBuilder = new PackageBuilder
            {
                Id = actualIdentity.Id,
                Version = actualIdentity.Version,
                Description = "Identity mismatch test package"
            };
            packageBuilder.Authors.Add("dotnet/sdk");
            string contentPath = Path.Combine(root.Path, "content.txt");
            File.WriteAllText(contentPath, "test");
            packageBuilder.Files.Add(new PhysicalPackageFile
            {
                SourcePath = contentPath,
                TargetPath = "content/content.txt"
            });
            using (FileStream packageStream = File.Create(nupkgPath))
            {
                packageBuilder.Save(packageStream);
            }

            var packageDownloader = new StaticPackagePathNuGetPackageDownloader(nupkgPath);
            Action download = () => downloader.DownloadAndExtract(
                TestPackageId,
                packageDownloader,
                packagesRoot.Value,
                packageVersion,
                new PackageSourceLocation(basePath: root.Path));

            download.Should().Throw<PackagingException>()
                .WithMessage($"*'{actualIdentity}'*'{new PackageIdentity(TestPackageId.ToString(), packageVersion)}'*");
            packageDownloader.ExtractCalled.Should().BeFalse(
                "a package must be validated before any files are extracted");
            Directory.Exists(pathResolver.GetInstallPath(TestPackageId.ToString(), packageVersion)).Should().BeFalse(
                "the mismatched package must be removed from the requested lock destination");
            Directory.Exists(pathResolver.GetInstallPath(actualIdentity.Id, actualIdentity.Version)).Should().BeFalse(
                "the archive identity must never select a destination outside the requested lock");
        }

        [Fact]
        public void GivenACompleteCachedLocalToolInstallationDoesNotCreateALockFile()
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory();
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

        [Fact]
        public async Task GivenOneLocalInstallFailsAfterPublicationAnotherInstallCanKeepUsingThePackage()
        {
            string source = GetTestLocalFeedPath();
            var root = _testAssetsManager.CreateTestDirectory();
            string packagesRoot = Path.Combine(root.Path, "packages");
            var nugetConfigPath = new FilePath(Path.Combine(root.Path, "NuGet.config"));
            var fileSystem = new FileSystemWrapper();
            fileSystem.File.WriteAllText(nugetConfigPath.Value, FormatNuGetConfig(source, packagesRoot));
            string runtimeJsonPath = Path.Combine(
                SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest,
                "RuntimeIdentifierGraph.json");
            var packageLocation = new PackageLocation(nugetConfig: nugetConfigPath);
            var packageVersion = VersionRange.Parse(TestPackageVersion);
            using var failingDownloader = new FailingAfterPublicationToolPackageDownloader(
                new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "failing-tools"))),
                runtimeJsonPath,
                root.Path);
            var reusingDownloader = new ToolPackageDownloader(
                new ToolPackageStoreAndQuery(new DirectoryPath(Path.Combine(root.Path, "reusing-tools"))),
                runtimeJsonPath,
                root.Path);

            Task<IToolPackage> failingInstall = Task.Run(
                () => failingDownloader.InstallPackage(
                    packageLocation,
                    TestPackageId,
                    TestVerbosity,
                    packageVersion,
                    _testTargetframework,
                    isGlobalTool: false,
                    verifySignatures: false),
                CancellationToken.None);

            Task firstCompleted = await Task.WhenAny(failingDownloader.AssetCreationStarted, failingInstall);
            firstCompleted.Should().Be(
                failingDownloader.AssetCreationStarted,
                "the first install should publish the package before its simulated validation failure");

            IToolPackage reusedPackage;
            try
            {
                reusedPackage = reusingDownloader.InstallPackage(
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
                failingDownloader.ContinueAssetCreation();
            }

            Func<Task> awaitFailingInstall = async () => await failingInstall;
            await awaitFailingInstall.Should().ThrowAsync<ToolPackageException>()
                .WithMessage(FailingAfterPublicationToolPackageDownloader.FailureMessage);
            File.Exists(reusedPackage.Command.Executable.Value).Should().BeTrue(
                "a later failure must not remove a shared package that another install already reused");
        }

        [Fact]
        public void GivenAStaleLockFileInstallationCanAcquireTheLock()
        {
            DirectoryPath root = new(_testAssetsManager.CreateTestDirectory().Path);
            NuGetVersion version = NuGetVersion.Parse(TestPackageVersion);
            string lockFilePath = ToolPackageDownloaderBase.GetToolInstallLockFilePath(root, TestPackageId, version);
            Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
            File.WriteAllText(lockFilePath, "stale");

            using FileStream installLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                root,
                TestPackageId,
                version,
                initialWaitTimeout: TimeSpan.Zero,
                waitTimeout: TimeSpan.FromSeconds(1),
                cancellationToken: CancellationToken.None);

            installLock.Should().NotBeNull();
        }

        [Fact]
        public void GivenAContendedLockInstallationTimesOut()
        {
            DirectoryPath root = new(_testAssetsManager.CreateTestDirectory().Path);
            NuGetVersion version = NuGetVersion.Parse(TestPackageVersion);
            using FileStream heldLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                root,
                TestPackageId,
                version,
                cancellationToken: CancellationToken.None);
            var reporter = new BufferedReporter();
            Action acquire = () =>
            {
                using FileStream _ = ToolPackageDownloaderBase.AcquireToolInstallLock(
                    root,
                    TestPackageId,
                    version,
                    initialWaitTimeout: TimeSpan.Zero,
                    waitTimeout: TimeSpan.FromMilliseconds(50),
                    errorReporter: reporter,
                    cancellationToken: CancellationToken.None);
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

        [Fact]
        public void GivenAContendedLockInstallationCanBeCanceled()
        {
            DirectoryPath root = new(_testAssetsManager.CreateTestDirectory().Path);
            NuGetVersion version = NuGetVersion.Parse(TestPackageVersion);
            using FileStream heldLock = ToolPackageDownloaderBase.AcquireToolInstallLock(
                root,
                TestPackageId,
                version,
                cancellationToken: CancellationToken.None);
            using var cancellationTokenSource = new CancellationTokenSource();
            var reporter = new CancelingReporter(cancellationTokenSource);
            Action acquire = () =>
            {
                using FileStream _ = ToolPackageDownloaderBase.AcquireToolInstallLock(
                    root,
                    TestPackageId,
                    version,
                    initialWaitTimeout: TimeSpan.Zero,
                    errorReporter: reporter,
                    cancellationToken: cancellationTokenSource.Token);
            };

            acquire.Should().Throw<OperationCanceledException>();
            reporter.Lines.Should().ContainSingle();
        }

        [Fact]
        public void GivenARootWithNonAsciiCharacterInstallSucceeds()
        {
            var surrogate = char.ConvertFromUtf32(int.Parse("2A601", NumberStyles.HexNumber));
            string nonAscii = "ab Ṱ̺̺̕o 田中さん åä," + surrogate;

            var root = _testAssetsManager.CreateTestDirectory(testName: nonAscii, identifier: "root");
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


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
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

        [UnixOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
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
            var root = new DirectoryPath(_testAssetsManager.CreateTestDirectory(callingMethod, identifier: useMock.ToString() + identiifer).Path);
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

        private readonly TestToolBuilder ToolBuilder;

        private sealed class TestableToolPackageDownloader : ToolPackageDownloader, IDisposable
        {
            private readonly bool _pauseHashPublication;
            private readonly Action _beforeMetadataPublication;
            private readonly Action _afterMetadataPublication;
            private readonly TaskCompletionSource _hashMoveStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ManualResetEventSlim _continueHashMove = new(initialState: false);

            public TestableToolPackageDownloader(
                IToolPackageStore store,
                string runtimeJsonPathForTests,
                string currentWorkingDirectory,
                bool pauseHashPublication = false,
                Action beforeMetadataPublication = null,
                Action afterMetadataPublication = null)
                : base(store, runtimeJsonPathForTests, currentWorkingDirectory)
            {
                _pauseHashPublication = pauseHashPublication;
                _beforeMetadataPublication = beforeMetadataPublication;
                _afterMetadataPublication = afterMetadataPublication;
            }

            public Task HashMoveStarted => _hashMoveStarted.Task;

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

            public IToolPackage InstallFromCache(
                PackageId packageId,
                NuGetVersion packageVersion,
                DirectoryPath packagesRoot,
                PackageSourceLocation packageSourceLocation,
                string targetFramework)
            {
                DownloadTool(
                    packagesRoot,
                    packageId,
                    packageVersion,
                    CreatePackageDownloader(),
                    packageSourceLocation,
                    givenSpecificVersion: true,
                    assetFileDirectory: _localToolAssetDir,
                    acquireInstallLock: true,
                    targetFramework,
                    TestVerbosity);
                return new ToolPackageInstance(
                    packageId,
                    packageVersion,
                    packagesRoot,
                    _localToolAssetDir,
                    _fileSystem);
            }

            public void ContinueHashMove() => _continueHashMove.Set();

            protected override void MovePackageHash(string temporaryHashPath, string hashPath)
            {
                if (_pauseHashPublication)
                {
                    _hashMoveStarted.TrySetResult();
                    _continueHashMove.Wait();
                }

                base.MovePackageHash(temporaryHashPath, hashPath);
            }

            protected override void MovePackageMetadata(string temporaryMetadataPath, string metadataPath)
            {
                _beforeMetadataPublication?.Invoke();
                base.MovePackageMetadata(temporaryMetadataPath, metadataPath);
                _afterMetadataPublication?.Invoke();
            }

            public void Dispose() => _continueHashMove.Dispose();
        }

        private sealed class CancelingReporter(CancellationTokenSource cancellationTokenSource) : IReporter
        {
            public List<string> Lines { get; } = [];

            public void WriteLine(string message)
            {
                Lines.Add(message);
                cancellationTokenSource.Cancel();
            }

            public void WriteLine() => WriteLine(string.Empty);

            public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

            public void Write(string message) => WriteLine(message);
        }

        private sealed class FailingAfterPublicationToolPackageDownloader : ToolPackageDownloader, IDisposable
        {
            public const string FailureMessage = "Simulated failure after package publication.";

            private readonly TaskCompletionSource _assetCreationStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ManualResetEventSlim _continueAssetCreation = new(initialState: false);

            public FailingAfterPublicationToolPackageDownloader(
                IToolPackageStore store,
                string runtimeJsonPathForTests,
                string currentWorkingDirectory)
                : base(store, runtimeJsonPathForTests, currentWorkingDirectory)
            {
            }

            public Task AssetCreationStarted => _assetCreationStarted.Task;

            public void ContinueAssetCreation() => _continueAssetCreation.Set();

            protected override void CreateAssetFile(
                PackageId packageId,
                NuGetVersion version,
                DirectoryPath packagesRootPath,
                string assetFilePath,
                string runtimeJsonGraph,
                VerbosityOptions verbosity,
                string targetFramework = null)
            {
                _assetCreationStarted.TrySetResult();
                _continueAssetCreation.Wait();
                throw new ToolPackageException(FailureMessage);
            }

            public void Dispose() => _continueAssetCreation.Dispose();
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
                PackageSourceLocation packageSourceLocation = null) =>
                _inner.GetBestPackageVersionAndSourceAsync(
                    packageId,
                    versionRange,
                    packageSourceLocation);

            public void Dispose() => _continueExtraction.Dispose();
        }

        private sealed class StaticPackagePathNuGetPackageDownloader(string packagePath) : INuGetPackageDownloader
        {
            public bool ExtractCalled { get; private set; }

            public Task<string> DownloadPackageAsync(
                PackageId packageId,
                NuGetVersion packageVersion = null,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false,
                bool? includeUnlisted = null,
                DirectoryPath? downloadFolder = null,
                PackageSourceMapping packageSourceMapping = null) =>
                Task.FromResult(packagePath);

            public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
            {
                ExtractCalled = true;
                throw new InvalidOperationException("Extraction should not run for a mismatched package identity.");
            }

            public Task<string> GetPackageUrl(
                PackageId packageId,
                NuGetVersion packageVersion = null,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                throw new NotSupportedException();

            public Task<NuGetVersion> GetLatestPackageVersion(
                PackageId packageId,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                throw new NotSupportedException();

            public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(
                PackageId packageId,
                int numberOfResults,
                PackageSourceLocation packageSourceLocation = null,
                bool includePreview = false) =>
                throw new NotSupportedException();

            public Task<NuGetVersion> GetBestPackageVersionAsync(
                PackageId packageId,
                VersionRange versionRange,
                PackageSourceLocation packageSourceLocation = null) =>
                throw new NotSupportedException();

            public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(
                PackageId packageId,
                VersionRange versionRange,
                PackageSourceLocation packageSourceLocation = null) =>
                throw new NotSupportedException();
        }

        public ToolPackageDownloaderTests(ITestOutputHelper log, TestToolBuilder toolBuilder) : base(log)
        {
            ToolBuilder = toolBuilder;
        }
    }
}
