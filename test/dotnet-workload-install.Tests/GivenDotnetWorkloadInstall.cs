// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.CompilerServices;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class GivenDotnetWorkloadInstall : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadInstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
        }

        // These two tests hit an IOException when run in helix on non-windows
        [WindowsOnlyFact]
        public void GivenWorkloadInstallItErrorsOnFakeWorkloadName()
        {
            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "install", "fake", "--skip-manifest-update")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(string.Format(Workloads.Workload.Install.LocalizableStrings.WorkloadNotRecognized, "fake"));
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/26624")]
        public void ItErrorUsingSkipManifestAndRollback()
        {
            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "install", "wasm-tools", "--skip-manifest-update", "--from-rollback-file", "foo.txt")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(string.Format(Workloads.Workload.Install.LocalizableStrings.CannotCombineSkipManifestAndRollback, "skip-manifest-update", "from-rollback-file"));
        }


        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanInstallPacks(bool userLocal, string sdkVersion)
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "--skip-manifest-update" });
            (_, var installManager, var installer, _, _, _) = GetTestInstallers(parseResult, userLocal, sdkVersion, installedFeatureBand: sdkVersion);

            installManager.InstallWorkloads(mockWorkloadIds, true);

            installer.GarbageCollectionCalled.Should().BeTrue();
            installer.CachePath.Should().BeNull();
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEquivalentTo(mockWorkloadIds);
            installer.InstalledPacks.Count.Should().Be(8);
            installer.InstalledPacks.Where(pack => pack.Id.ToString().Contains("Android")).Count().Should().Be(8);
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanRollBackPackInstallation(bool userLocal, string sdkVersion)
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android"), new WorkloadId("xamarin-android-build") };
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "xamarin-android-build", "--skip-manifest-update" });
            (_, var installManager, var installer, var workloadResolver, _, _) = GetTestInstallers(parseResult, userLocal, sdkVersion, failingWorkload: "xamarin-android-build", installedFeatureBand: sdkVersion);

            var exceptionThrown = Assert.Throws<Exception>(() => installManager.InstallWorkloads(mockWorkloadIds, true));
            exceptionThrown.Message.Should().Be("Failing workload: xamarin-android-build");
            var expectedPacks = mockWorkloadIds
                .SelectMany(workloadId => workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);
            installer.RolledBackPacks.Should().BeEquivalentTo(expectedPacks);
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEmpty();
        }

        [Fact]
        public void GivenWorkloadInstallOnFailingRollbackItDisplaysTopLevelError()
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android"), new WorkloadId("xamarin-android-build") };
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller(dotnetRoot, failingWorkload: "xamarin-android-build", failingRollback: true);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "xamarin-android-build", "--skip-manifest-update" });
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver);

            var installManager = new WorkloadInstallCommand(parseResult, reporter: _reporter, workloadResolverFactory, workloadInstaller: installer);

            var exceptionThrown = Assert.Throws<Exception>(() => installManager.InstallWorkloads(mockWorkloadIds, true));
            exceptionThrown.Message.Should().Be("Failing workload: xamarin-android-build");
            string.Join(" ", _reporter.Lines).Should().Contain("Rollback failure");
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanUpdateAdvertisingManifests(bool userLocal, string sdkVersion)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android" });
            (_, var installManager, var installer, _, var manifestUpdater, _) = GetTestInstallers(parseResult, userLocal, sdkVersion, installedFeatureBand: sdkVersion);

            installManager.InstallWorkloads(new List<WorkloadId>(), false); // Don't actually do any installs, just update manifests

            installer.InstalledManifests.Should().BeEmpty(); // Didn't try to alter any installed manifests
            manifestUpdater.CalculateManifestUpdatesCallCount.Should().Be(1);
            manifestUpdater.UpdateAdvertisingManifestsCallCount.Should().Be(1);
        }

        [Fact]
        public void GivenWorkloadInstallItWarnsOnGarbageCollectionFailure()
        {
            _reporter.Clear();
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android"), new WorkloadId("xamarin-android-build") };
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller(dotnetRoot, failingGarbageCollection: true);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "xamarin-android-build", "--skip-manifest-update" });
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver);
            var installManager = new WorkloadInstallCommand(parseResult, reporter: _reporter, workloadResolverFactory, workloadInstaller: installer);

            installManager.InstallWorkloads(mockWorkloadIds, true);
            string.Join(" ", _reporter.Lines).Should().Contain("Failing garbage collection");
        }

        [Fact]
        public void GivenInfoOptionWorkloadBaseCommandAcceptsThatOption()
        {
            var command = new DotnetCommand(Log);
            var commandResult = command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "--info");

            commandResult.Should().Pass();
        }

        [Fact]
        public void GivenNoWorkloadsInstalledInfoOptionRemarksOnThat()
        {
            // We can't easily mock the end to end process of installing a workload and testing --info on it so we are adding that to the manual testing document.
            // However, we can test a setup where no workloads are installed and --info is provided. 

            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android" });

            WorkloadInfoHelper workloadInfoHelper = new WorkloadInfoHelper(isInteractive: false, workloadResolver: workloadResolver);
            WorkloadCommandParser.ShowWorkloadsInfo(parseResult, workloadInfoHelper: workloadInfoHelper, reporter: _reporter);
            _reporter.Lines.Should().Contain("There are no installed workloads to display.");
        }

        [Fact]
        public void GivenBadOptionWorkloadBaseInformsRequiredCommandWasNotProvided()
        {
            _reporter.Clear();
            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "--infoz")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("Required command was not provided.");
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanUpdateInstalledManifests(bool userLocal, string sdkVersion)
        {
            var parseResult =
                Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android" });
            var featureBand = new SdkFeatureBand(sdkVersion);
            var manifestsToUpdate =
                new ManifestUpdateWithWorkloads[]
                    {
                        new(new ManifestVersionUpdate(new ManifestId("mock-manifest"), new ManifestVersion("1.0.0"), featureBand.ToString(), new ManifestVersion("2.0.0"), featureBand.ToString()), null),
                    };
            (_, var installManager, var installer, _, _, _) =
                GetTestInstallers(parseResult, userLocal, sdkVersion, manifestUpdates: manifestsToUpdate, installedFeatureBand: sdkVersion);

            installManager.InstallWorkloads(new List<WorkloadId>(), false); // Don't actually do any installs, just update manifests

            installer.InstalledManifests[0].manifestUpdate.ManifestId.Should().Be(manifestsToUpdate[0].ManifestUpdate.ManifestId);
            installer.InstalledManifests[0].manifestUpdate.NewVersion.Should().Be(manifestsToUpdate[0].ManifestUpdate.NewVersion);
            installer.InstalledManifests[0].manifestUpdate.NewFeatureBand.Should().Be(new SdkFeatureBand(sdkVersion).ToString());
            installer.InstalledManifests[0].offlineCache.Should().Be(null);
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallFromCacheItInstallsCachedManifest(bool userLocal, string sdkVersion)
        {
            var featureBand = new SdkFeatureBand(sdkVersion);
            var manifestsToUpdate =
                new ManifestUpdateWithWorkloads[]
                    {
                        new(new ManifestVersionUpdate(new ManifestId("mock-manifest"), new ManifestVersion("1.0.0"), featureBand.ToString(), new ManifestVersion("2.0.0"), featureBand.ToString()), null)
                    };
            var cachePath = Path.Combine(_testAssetsManager.CreateTestDirectory(identifier: AppendForUserLocal("mockCache_", userLocal) + sdkVersion).Path,
                "mockCachePath");
            var parseResult = Parser.Instance.Parse(new string[]
            {
                "dotnet", "workload", "install", "xamarin-android", "--from-cache", cachePath
            });
            (_, var installManager, var installer, _, _, _) = GetTestInstallers(parseResult, userLocal, sdkVersion,
                tempDirManifestPath: _manifestPath, manifestUpdates: manifestsToUpdate, installedFeatureBand: sdkVersion);

            installManager.Execute();

            installer.InstalledManifests[0].manifestUpdate.ManifestId.Should().Be(manifestsToUpdate[0].ManifestUpdate.ManifestId);
            installer.InstalledManifests[0].manifestUpdate.NewVersion.Should().Be(manifestsToUpdate[0].ManifestUpdate.NewVersion);
            installer.InstalledManifests[0].manifestUpdate.NewFeatureBand.Should().Be(new SdkFeatureBand(sdkVersion).ToString());
            installer.InstalledManifests[0].offlineCache.Should().Be(new DirectoryPath(cachePath));
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanDownloadToOfflineCache(bool userLocal, string sdkVersion)
        {
            var cachePath = Path.Combine(_testAssetsManager.CreateTestDirectory(identifier: AppendForUserLocal("mockCache_", userLocal) + sdkVersion).Path, "mockCachePath");
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "--download-to-cache", cachePath });
            (_, var installManager, _, _, var manifestUpdater, var packageDownloader) = GetTestInstallers(parseResult, userLocal, sdkVersion, tempDirManifestPath: _manifestPath, installedFeatureBand: sdkVersion);

            installManager.Execute();

            // Manifest packages should have been 'downloaded' and used for pack resolution
            manifestUpdater.GetManifestPackageDownloadsCallCount.Should().Be(1);
            // 8 android pack packages, plus 1 manifest
            packageDownloader.DownloadCallParams.Count.Should().Be(9);
            foreach (var downloadParams in packageDownloader.DownloadCallParams)
            {
                downloadParams.downloadFolder.Value.Value.Should().Be(cachePath);
            }
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItCanInstallFromOfflineCache(bool userLocal, string sdkVersion)
        {
            var mockWorkloadIds = new WorkloadId[] { new WorkloadId("xamarin-android") };
            var cachePath = "mockCachePath";
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "--from-cache", cachePath });
            (_, var installManager, var installer, _, _, var nugetDownloader) = GetTestInstallers(parseResult, userLocal, sdkVersion, installedFeatureBand: sdkVersion);

            installManager.Execute();

            installer.GarbageCollectionCalled.Should().BeTrue();
            installer.CachePath.Should().Contain(cachePath);
            installer.InstallationRecordRepository.WorkloadInstallRecord.Should().BeEquivalentTo(mockWorkloadIds);
            installer.InstalledPacks.Count.Should().Be(8);
            installer.InstalledPacks.Where(pack => pack.Id.ToString().Contains("Android")).Count().Should().Be(8);
            nugetDownloader.DownloadCallParams.Count().Should().Be(0);
        }

        [Theory]
        [InlineData(true, "6.0.100")]
        [InlineData(true, "6.0.101")]
        [InlineData(true, "6.0.102-preview1")]
        [InlineData(false, "6.0.100")]
        public void GivenWorkloadInstallItPrintsDownloadUrls(bool userLocal, string sdkVersion)
        {
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "xamarin-android", "--print-download-link-only" });
            (_, var installManager, _, _, _, _) = GetTestInstallers(parseResult, userLocal, sdkVersion, tempDirManifestPath: _manifestPath, installedFeatureBand: sdkVersion);

            installManager.Execute();

            string.Join(" ", _reporter.Lines).Should().Contain("http://mock-url/xamarin.android.sdk.8.4.7.nupkg");
            string.Join(" ", _reporter.Lines).Should().Contain("http://mock-url/mock-manifest-package.1.0.5.nupkg");
        }

        [Fact]
        public void GivenWorkloadInstallItErrorsOnUnsupportedPlatform()
        {
            var mockWorkloadId = "unsupported";
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "UnsupportedPlatform.json");
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var installer = new MockPackWorkloadInstaller(dotnetRoot);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", mockWorkloadId });
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver, userProfileDir: testDirectory);

            var command = new WorkloadInstallCommand(parseResult, reporter: _reporter, workloadResolverFactory, workloadInstaller: installer,
                nugetPackageDownloader: nugetDownloader, workloadManifestUpdater: manifestUpdater);

            var exceptionThrown = Assert.Throws<GracefulException>(() => command.Execute());
            exceptionThrown.Message.Should().Be(String.Format(Workloads.Workload.Install.LocalizableStrings.WorkloadInstallationFailed, String.Format(Workloads.Workload.Install.LocalizableStrings.WorkloadNotSupportedOnPlatform, mockWorkloadId)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadInstallItDoesNotRemoveOldInstallsOnRollback(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var tmpDir = Path.Combine(testDirectory, "tmp");
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new FailingNuGetPackageDownloader(tmpDir);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var sdkFeatureVersion = "6.0.100";
            var existingWorkload = "mock-1";
            var installingWorkload = "mock-2";
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, sdkFeatureVersion, workloadResolver, userProfileDir);


            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            // Successfully install a workload
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", existingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            installCommand.Execute();

            // Install a workload with a mocked nuget failure
            installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolverFactory, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            var exceptionThrown = Assert.Throws<GracefulException>(() => installCommand.Execute());
            exceptionThrown.Message.Should().Contain("Test Failure");

            // Existing installation is still present
            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            var installRecordPath = Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads");
            Directory.GetFiles(installRecordPath).Count().Should().Be(1);
            File.Exists(Path.Combine(installRecordPath, existingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(3);
        }

        [Fact]
        public void GivenWorkloadInstallItTreatsPreviewsAsSeparateFeatureBands()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var tmpDir = Path.Combine(testDirectory, "tmp");
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
            
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var prev7SdkFeatureVersion = "6.0.100-preview.7.21379.14";
            var prev7FormattedFeatureVersion = "6.0.100-preview.7";
            var rc1SdkFeatureVersion = "6.0.100-rc.1.21463.6";
            var rc1FormattedFeatureVersion = "6.0.100-rc.1";

            static void CreateFile(string path)
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                using var _ = File.Create(path);
            }

            //  Create fake SDK directories (so garbage collector will see them as installed versions)
            CreateFile(Path.Combine(dotnetRoot, "sdk", prev7SdkFeatureVersion, "dotnet.dll"));
            CreateFile(Path.Combine(dotnetRoot, "sdk", rc1SdkFeatureVersion, "dotnet.dll"));

            var existingWorkload = "mock-1";
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot, userProfileDir: userProfileDir);
            var prev7workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, prev7SdkFeatureVersion, workloadResolver, userProfileDir);
            var rc1WorkloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, rc1SdkFeatureVersion, workloadResolver, userProfileDir);

            // Install a workload for preview 7
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", existingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, prev7workloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            installCommand.Execute();

            // Install workload for RC1
            installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, rc1WorkloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            installCommand.Execute();

            // Existing installation is present
            var prev7InstallRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", prev7FormattedFeatureVersion, "InstalledWorkloads");
            Directory.GetFiles(prev7InstallRecordPath).Count().Should().Be(1);
            File.Exists(Path.Combine(prev7InstallRecordPath, existingWorkload))
                .Should().BeTrue();

            var rc1InstallRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", rc1FormattedFeatureVersion, "InstalledWorkloads");
            Directory.GetFiles(rc1InstallRecordPath).Count().Should().Be(1);
            File.Exists(Path.Combine(rc1InstallRecordPath, existingWorkload))
                .Should().BeTrue();

            // Assert that packs have been installed
            var packRecordDirs = Directory.GetDirectories(Path.Combine(dotnetRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
            var installPacks = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs"));
            installPacks.Count().Should().Be(2);

            // Assert feature band records are correct
            var featureBandRecords = Directory.GetFiles(Directory.GetDirectories(packRecordDirs[0])[0]);
            featureBandRecords.Count().Should().Be(2);
            featureBandRecords.Select(recordPath => Path.GetFileName(recordPath))
                .Should().BeEquivalentTo(new string[] { prev7FormattedFeatureVersion, rc1FormattedFeatureVersion });
        }

        private (string, WorkloadInstallCommand, MockPackWorkloadInstaller, IWorkloadResolver, MockWorkloadManifestUpdater, MockNuGetPackageDownloader) GetTestInstallers(
                ParseResult parseResult,
                bool userLocal,
                string sdkVersion,
                [CallerMemberName] string testName = "",
                string failingWorkload = null,
                IEnumerable<ManifestUpdateWithWorkloads> manifestUpdates = null,
                string tempDirManifestPath = null,
                string installedFeatureBand = null)
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory(testName: testName, identifier: (userLocal ? "userlocal" : "default") + sdkVersion).Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var installer = new MockPackWorkloadInstaller(dotnetRoot, failingWorkload)
            {
                WorkloadResolver = workloadResolver
            };

            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater(manifestUpdates);
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkVersion);
            }
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, sdkVersion, workloadResolver, userProfileDir);

            var installManager = new WorkloadInstallCommand(
                parseResult,
                reporter: _reporter,
                workloadResolverFactory: workloadResolverFactory,
                workloadInstaller: installer,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater);

            return (testDirectory, installManager, installer, workloadResolver, manifestUpdater, nugetDownloader);
        }

        [Fact]
        public void GivenWorkloadInstallItErrorsOnInvalidWorkloadRollbackFile()
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var tmpDir = Path.Combine(testDirectory, "tmp");
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot);
            var sdkFeatureVersion = "6.0.100";
            var workload = "mock-1";
            var mockRollbackFileContent = @"{""fake.manifest.name"":""1.0.0""}";
            var rollbackFilePath = Path.Combine(testDirectory, "rollback.json");
            File.WriteAllText(rollbackFilePath, mockRollbackFileContent);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, sdkFeatureVersion, workloadResolver, userProfileDir);

            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", workload, "--from-rollback-file", rollbackFilePath });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                tempDirPath: testDirectory);
            
            Assert.Throws<GracefulException>(() => installCommand.Execute());
            string.Join(" ", _reporter.Lines).Should().Contain("Invalid rollback definition");
        }

        [Fact]
        public void GivenWorkloadInstallItWarnsWhenTheWorkloadIsAlreadyInstalled()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var tmpDir = Path.Combine(testDirectory, "tmp");
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot, false, userProfileDir);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var sdkFeatureVersion = "6.0.100";
            var workloadId = "mock-1";
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, sdkFeatureVersion, workloadResolver, userProfileDir);

            // Successfully install a workload
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", workloadId });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            installCommand.Execute()
                .Should().Be(0);
            _reporter.Clear();

            // Install again, this time it should tell you that you already have the workload installed
            installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", workloadId, "mock-2" });
            installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolverFactory, nugetPackageDownloader: new MockNuGetPackageDownloader(tmpDir),
                workloadManifestUpdater: manifestUpdater, tempDirPath: testDirectory);
            installCommand.Execute()
                .Should().Be(0);

            // Install command warns
            string.Join(" ", _reporter.Lines).Should().Contain(string.Format(Workloads.Workload.Install.LocalizableStrings.WorkloadAlreadyInstalled, workloadId));

            // Both workloads are installed
            var installRecordPath = Path.Combine(dotnetRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads");
            Directory.GetFiles(installRecordPath).Count().Should().Be(2);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/25175")]
        public void HideManifestUpdateCheckWhenVerbosityIsQuiet()
        {
            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "install", "--verbosity:quiet", "wasm-tools")
                .Should()
                .NotHaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.CheckForUpdatedWorkloadManifests)
                .And
                .NotHaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.AdManifestUpdated);
        }


        [Theory(Skip = "https://github.com/dotnet/sdk/issues/25175")]
        [InlineData("--verbosity:minimal")]
        [InlineData("--verbosity:normal")]
        public void HideManifestUpdatesWhenVerbosityIsMinimalOrNormal(string verbosityFlag)
        {
            var command = new DotnetCommand(Log);
            command
                .WithEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", string.Empty)
                .WithEnvironmentVariable("PATH", "fake")
                .Execute("workload", "install", verbosityFlag, "wasm-tools")
                .Should()
                .HaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.CheckForUpdatedWorkloadManifests)
                .And
                .NotHaveStdOutContaining(Workloads.Workload.Install.LocalizableStrings.AdManifestUpdated);
        }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/25175")]
        [InlineData("--verbosity:detailed")]
        [InlineData("--verbosity:diagnostic")]
        public void ShowManifestUpdatesWhenVerbosityIsDetailedOrDiagnostic(string verbosityFlag)
        {
            string sdkFeatureBand = "6.0.300";

            var parseResult =
               Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", verbosityFlag, "xamarin-android" });
            var manifestsToUpdate =
                new ManifestUpdateWithWorkloads[]
                    {
                        new(new ManifestVersionUpdate(new ManifestId("mock-manifest"), new ManifestVersion("1.0.0"), sdkFeatureBand, new ManifestVersion("2.0.0"), sdkFeatureBand), null),
                    };
            (_, var installManager, _, _, _, _) =
                GetTestInstallers(parseResult, true, sdkFeatureBand, manifestUpdates: manifestsToUpdate);

            installManager.InstallWorkloads(new List<WorkloadId>(), false); // Don't actually do any installs, just update manifests

            string.Join(" ", _reporter.Lines).Should().Contain(Workloads.Workload.Install.LocalizableStrings.CheckForUpdatedWorkloadManifests);
            string.Join(" ", _reporter.Lines).Should().Contain(string.Format(Workloads.Workload.Install.LocalizableStrings.CheckForUpdatedWorkloadManifests, "mock-manifest"));
        }

        private string AppendForUserLocal(string identifier, bool userLocal)
        {
            if (!userLocal)
            {
                return identifier;
            }

            return $"{identifier}_userlocal";
        }
    }
}
