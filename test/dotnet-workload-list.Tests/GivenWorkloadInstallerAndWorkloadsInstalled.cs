// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.CompilerServices;
using ManifestReaderTests;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Update.Tests
{
    public class GivenInstalledWorkloadAndManifestUpdater : SdkTest
    {
        private const string CurrentSdkVersion = "6.0.101";
        private const string InstallingWorkload = "xamarin-android";
        private const string UpdateAvailableVersion = "7.0.100";
        private const string XamarinAndroidDescription = "xamarin-android description";
        private readonly BufferedReporter _reporter = new();
        private WorkloadListCommand _workloadListCommand;
        private string _testDirectory;

        private List<(TestManifestUpdate update, WorkloadCollection workloads)> _mockManifestUpdates;

        private MockNuGetPackageDownloader _nugetDownloader;
        private string _dotnetRoot;

        public GivenInstalledWorkloadAndManifestUpdater(ITestOutputHelper log) : base(log)
        {
        }

        private IEnumerable<ManifestUpdateWithWorkloads> GetManifestUpdatesForMock()
        {
            return _mockManifestUpdates.Select(u => new ManifestUpdateWithWorkloads(u.update.ToManifestVersionUpdate(), u.workloads));
        }

        private void Setup([CallerMemberName] string identifier = "")
        {
            _testDirectory = _testAssetsManager.CreateTestDirectory(identifier: identifier).Path;
            _dotnetRoot = Path.Combine(_testDirectory, "dotnet");
            _nugetDownloader = new(_dotnetRoot);
            var currentSdkFeatureBand = new SdkFeatureBand(CurrentSdkVersion);

            _mockManifestUpdates = new()
            {
                new(
                    new TestManifestUpdate(
                        new ManifestId("manifest1"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion(UpdateAvailableVersion),
                        currentSdkFeatureBand.ToString()),
                    new WorkloadCollection
                    {
                        [new WorkloadId(InstallingWorkload)] = new(
                            new WorkloadId(InstallingWorkload), false, XamarinAndroidDescription,
                            WorkloadDefinitionKind.Dev, null, null, null),
                        [new WorkloadId("other")] = new(
                            new WorkloadId("other"), false, "other description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    }),
                new(
                    new TestManifestUpdate(
                        new ManifestId("manifest-other"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion("7.0.101"),
                        currentSdkFeatureBand.ToString()),
                    new WorkloadCollection
                    {
                        [new WorkloadId("other-manifest-workload")] = new(
                            new WorkloadId("other-manifest-workload"), false,
                            "other-manifest-workload description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    }),
                new(
                    new TestManifestUpdate(
                        new ManifestId("manifest-older-version"),
                        new ManifestVersion(CurrentSdkVersion),
                        currentSdkFeatureBand.ToString(),
                        new ManifestVersion("6.0.100"),
                        currentSdkFeatureBand.ToString()),
                    new WorkloadCollection
                    {
                        [new WorkloadId("other-manifest-workload")] = new(
                            new WorkloadId("other-manifest-workload"), false,
                            "other-manifest-workload description",
                            WorkloadDefinitionKind.Dev, null, null, null)
                    })
            };

            ParseResult listParseResult = Parser.Instance.Parse(new[]
            {
                "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "7.0.100"
            });


            var manifestProvider = new MockManifestProvider(_mockManifestUpdates.Select(u =>
            {
                string manifestFile = Path.Combine(_testDirectory, u.update.ManifestId.ToString() + ".json");
                File.WriteAllText(manifestFile, GivenWorkloadManifestUpdater.GetManifestContent(u.update.ExistingVersion));
                return (u.update.ManifestId.ToString(), manifestFile, u.update.ExistingVersion.ToString(), u.update.ExistingFeatureBand.ToString());
            }).ToArray());
            var workloadResolver = WorkloadResolver.CreateForTests(manifestProvider, _dotnetRoot);

            _workloadListCommand = new WorkloadListCommand(
                listParseResult,
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(GetManifestUpdatesForMock()),
                userProfileDir: _testDirectory,
                currentSdkVersion: CurrentSdkVersion,
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository(),
                workloadResolver: workloadResolver);
        }

        [Fact]
        public void ItShouldGetAvailableUpdate()
        {
            Setup();
            WorkloadListCommand.UpdateAvailableEntry[] result =
                _workloadListCommand.GetUpdateAvailable(new List<WorkloadId> { new("xamarin-android") }).ToArray();

            result.Should().NotBeEmpty();
            result[0].WorkloadId.Should().Be(InstallingWorkload, "Only should installed workload");
            result[0].ExistingManifestVersion.Should().Be(CurrentSdkVersion);
            result[0].AvailableUpdateManifestVersion.Should().Be(UpdateAvailableVersion);
            result[0].Description.Should().Be(XamarinAndroidDescription);
        }

        [Fact]
        public void ItShouldGetListOfWorkloadWithCurrentSdkVersionBand()
        {
            Setup();
            _workloadListCommand.Execute();
            _reporter.Lines.Should().Contain(c => c.Contains("\"installed\":[\"xamarin-android\"]"));
        }

        [Fact]
        public void GivenLowerTargetVersionItShouldThrow()
        {
            _workloadListCommand = new WorkloadListCommand(
                Parser.Instance.Parse(new[]
                {
                    "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "5.0.306"
                }),
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(null),
                userProfileDir: _testDirectory,
                currentSdkVersion: CurrentSdkVersion,
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());

            Action a = () => _workloadListCommand.Execute();
            a.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GivenSameLowerTargetVersionBandItShouldNotThrow()
        {
            _workloadListCommand = new WorkloadListCommand(
                Parser.Instance.Parse(new[]
                {
                    "dotnet", "workload", "list", "--machine-readable", InstallingWorkloadCommandParser.VersionOption.Name, "6.0.100"
                }),
                _reporter,
                nugetPackageDownloader: _nugetDownloader,
                workloadManifestUpdater: new MockWorkloadManifestUpdater(null),
                userProfileDir: _testDirectory,
                currentSdkVersion: "6.0.101",
                dotnetDir: _dotnetRoot,
                workloadRecordRepo: new MockMatchingFeatureBandInstallationRecordRepository());

            Action a = () => _workloadListCommand.Execute();
            a.Should().NotThrow();
        }

        internal class MockMatchingFeatureBandInstallationRecordRepository : IWorkloadInstallationRecordRepository
        {
            public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) =>
                throw new NotImplementedException();

            public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand) =>
                throw new NotImplementedException();

            public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
            {
                SdkFeatureBand featureBand = new(new ReleaseVersion(6, 0, 100));
                if (sdkFeatureBand.Equals(featureBand))
                {
                    return new[] { new WorkloadId("xamarin-android") };
                }

                throw new Exception($"Should not pass other feature band {sdkFeatureBand}");
            }

            public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() =>
                throw new NotImplementedException();
        }
    }
}
