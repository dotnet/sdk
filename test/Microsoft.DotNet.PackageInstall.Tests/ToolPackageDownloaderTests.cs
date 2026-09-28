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
            string dotnetRootUnderTest = TestContext.Current.ToolsetUnderTest.DotNetRoot;
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
                .NotContain(e => Path.GetFileName(e) != ToolPackageStoreAndQuery.StagingDirectory);

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
                    runtimeJsonPathForTests: TestContext.GetRuntimeGraphFilePath(),
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
                var testRuntimeJsonPath = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "RuntimeIdentifierGraph.json");
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

        private readonly TestToolBuilder ToolBuilder;

        public ToolPackageDownloaderTests(ITestOutputHelper log, TestToolBuilder toolBuilder) : base(log)
        {
            ToolBuilder = toolBuilder;
        }
    }
}
