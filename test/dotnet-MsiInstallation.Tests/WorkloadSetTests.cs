// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTests : WorkloadSetTestsBase
    {
        public WorkloadSetTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void DoesNotUseWorkloadSetsByDefault()
        {
            InstallSdk();

            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .PassWithoutWarning();

            var originalRollback = GetRollback();

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .PassWithoutWarning();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(originalRollback.ManifestVersions);

        }

        [Fact]
        public void UpdateWithWorkloadSets()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string _, out WorkloadSet rollbackAfterUpdate);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);

            var newRollback = GetRollback();
            newRollback.ManifestVersions.Should().NotBeEquivalentTo(rollbackAfterUpdate.ManifestVersions);

            //  A second workload update command should not try to install any updates
            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute().Should().PassWithoutWarning()
                .And.HaveStdOutContaining("No workload update found")
                .And.NotHaveStdOutContaining("Installing workload version");

        }

        [Fact]
        public void UpdateInWorkloadSetModeWithNoAvailableWorkloadSet()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate);

            //  Use a nonexistant source because there may be a valid workload set available on NuGet.org
            CreateInstallingCommand("dotnet", "workload", "update", "--source", @"c:\SdkTesting\EmptySource")
                .Execute()
                .Should()
                .PassWithoutWarning();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(rollbackAfterUpdate.ManifestVersions);

            GetWorkloadVersion().Should().Be(updatedWorkloadVersion);
        }

        [Fact]
        public void UpdateToSpecificWorkloadSetVersion()
        {
            UpdateToWorkloadSetVersion(WorkloadSetVersion1);
        }

        [Fact]
        public void UpdateToPreviousBandWorkloadSetVersion()
        {
            UpdateToWorkloadSetVersion(WorkloadSetPreviousBandVersion);
        }

        private void UpdateToWorkloadSetVersion(string versionToInstall)
        {
            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();
            workloadVersionBeforeUpdate.Should().NotBe(versionToInstall);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            CreateInstallingCommand("dotnet", "workload", "update", "--version", versionToInstall)
                .Execute()
                .Should()
                .PassWithoutWarning();

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(versionToInstall);

            //  Installing a workload shouldn't update workload version
            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be(versionToInstall);

            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
        }

        [Fact]
        public void UpdateToUnavailableWorkloadSetVersion()
        {
            string unavailableWorkloadSetVersion = "8.0.300-preview.test.42";

            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            CreateInstallingCommand("dotnet", "workload", "update", "--version", unavailableWorkloadSetVersion)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdErrContaining(unavailableWorkloadSetVersion)
                .And.NotHaveStdOutContaining("Installation rollback failed");

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }


        [Fact]
        public void UpdateWorkloadSetWithoutAvailableManifests()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate);

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            CreateInstallingCommand("dotnet", "workload", "update", "--source", @"c:\SdkTesting\workloadsets")
                .Execute()
                .Should()
                .Fail();

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }

        [Fact]
        public void UpdateToWorkloadSetVersionWithManifestsNotAvailable()
        {
            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            CreateInstallingCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion2, "--source", @"c:\SdkTesting\workloadsets")
                .Execute()
                .Should()
                .Fail();

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }

        [Fact]
        public void UpdateShouldNotPinWorkloadSet()
        {
            InstallSdk();
            UpdateAndSwitchToWorkloadSetMode(out _, out _);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            RemoveWorkloadSetFromLocalSource(WorkloadSetVersion2);

            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion1);

            //  Bring latest workload set version back, so installing workload should update to it
            VM.CreateActionGroup($"Enable {WorkloadSetVersion2}",
                    VM.CreateRunCommand("cmd", "/c", "move", @"c:\SdkTesting\DisabledWorkloadSets\*.nupkg", @"c:\SdkTesting\WorkloadSets"))
                .Execute().Should().PassWithoutWarning();

            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
        }

        [Fact(Skip = "Not Implemented")]
        public void WorkloadSetInstallationRecordIsWrittenCorrectly()
        {
            //  Should the workload set version or the package version be used in the registry?
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void TurnOffWorkloadSetUpdateMode()
        {
            //  If you have a workload set installed and then turn off workload set update mode, what should happen?
            //  - Update should update individual manifests
            //  - Resolver should ignore workload sets that are installed
            throw new NotImplementedException();
        }

        [Fact]
        public void GarbageCollectWorkloadSets()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string _, out WorkloadSet rollbackAfterUpdate);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            //  Update to latest workload set version
            CreateInstallingCommand("dotnet", "workload", "update")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);

            //  Get workload set feature band
            var workloadSetFeatureBand = WorkloadSetVersion.GetFeatureBand(WorkloadSetVersion2);

            string workloadSet2Path = $@"c:\Program Files\dotnet\sdk-manifests\{workloadSetFeatureBand}\workloadsets\{WorkloadSetVersion2}";

            VM.GetRemoteDirectory(workloadSet2Path).Should().Exist();

            //  Downgrade to earlier workload set version
            VM.CreateRunCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion1)
                .Execute().Should().PassWithoutWarning();

            //  Later workload set version should be GC'd
            VM.GetRemoteDirectory(workloadSet2Path).Should().NotExist();

            //  Now, pin older workload set version in global.json
            VM.WriteFile("C:\\SdkTesting\\global.json", @$"{{""sdk"":{{""workloadVersion"":""{WorkloadSetVersion1}""}}}}").Execute().Should().PassWithoutWarning();

            //  Install pinned version
            CreateInstallingCommand("dotnet", "workload", "update")
               .WithWorkingDirectory(SdkTestingDirectory)
               .Execute().Should().PassWithoutWarning();

            //  Update globally installed version to later version
            CreateInstallingCommand("dotnet", "workload", "update")
               .Execute().Should().PassWithoutWarning();

            //  Check workload versions in global context and global.json directory
            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
            GetWorkloadVersion(SdkTestingDirectory).Should().Be(WorkloadSetVersion1);

            //  Workload set 1 should still be installed
            string workloadSet1Path = $@"c:\Program Files\dotnet\sdk-manifests\{workloadSetFeatureBand}\workloadsets\{WorkloadSetVersion1}";
            VM.GetRemoteDirectory(workloadSet1Path).Should().Exist();

            //  Now, remove pinned workload set from global.json
            VM.WriteFile("C:\\SdkTesting\\global.json", "{}").Execute().Should().PassWithoutWarning();

            //  Run workload update to do a GC
            CreateInstallingCommand("dotnet", "workload", "update")
               .Execute().Should().PassWithoutWarning();

            //  Workload set 1 should have been GC'd
            VM.GetRemoteDirectory(workloadSet1Path).Should().NotExist();
        }

        //  Note: this may fail due to https://github.com/dotnet/sdk/issues/43876
        [Fact]
        public void FinalizerUninstallsWorkloadSets()
        {
            UpdateWithWorkloadSets();

            var workloadSetFeatureBand = WorkloadSetVersion.GetFeatureBand(WorkloadSetVersion2);

            string workloadSetPath = $@"c:\Program Files\dotnet\sdk-manifests\{workloadSetFeatureBand}\workloadsets\{WorkloadSetVersion2}";

            VM.GetRemoteDirectory(workloadSetPath).Should().Exist();

            UninstallSdk();

            VM.GetRemoteDirectory(workloadSetPath).Should().NotExist();
        }

        //  Note: this may fail for rtm-branded non-stabilized SDKs: https://github.com/dotnet/sdk/issues/43890
        [Fact]
        public void WorkloadSearchVersion()
        {
            InstallSdk();

            //  Run `dotnet workload search version` without source set up
            var searchVersionResult = VM.CreateRunCommand("dotnet", "workload", "search", "version")
                .WithIsReadOnly(true)
                .Execute(); ;
            searchVersionResult.Should().PassWithoutWarning();

            //  Without source set up, there should be no workload sets found
            searchVersionResult.StdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Should().BeEmpty();
            searchVersionResult.StdErr.Should().Contain($"No workload versions found for SDK feature band {new SdkFeatureBand(SdkInstallerVersion)}");

            //  Add source so workload sets will be found
            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            //  `dotnet workload search version` should return expected versions
            searchVersionResult = VM.CreateRunCommand("dotnet", "workload", "search", "version")
                .WithIsReadOnly(true)
                .Execute();
            searchVersionResult.Should().PassWithoutWarning();
            var actualVersions = searchVersionResult.StdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            actualVersions.Should().Equal(WorkloadSetVersion2, WorkloadSetVersion1);


            //  `dotnet workload search version <VERSION> --format json` should return manifest versions (and eventually perhaps other information) about that workload set
            searchVersionResult = VM.CreateRunCommand("dotnet", "workload", "search", "version", WorkloadSetVersion2, "--format", "json")
                .WithIsReadOnly(true)
                .Execute();

            searchVersionResult.Should().PassWithoutWarning();

            var searchResultJson = JsonNode.Parse(searchVersionResult.StdOut);
            var searchResultWorkloadSet = WorkloadSet.FromDictionaryForJson(JsonSerializer.Deserialize<Dictionary<string, string>>(searchResultJson["manifestVersions"]), new SdkFeatureBand(SdkInstallerVersion));

            //  Update to the workload set version we got the search info from so we can check to see if the manifest versions match what we expect
            CreateInstallingCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion2)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetRollback().ManifestVersions.Should().BeEquivalentTo(searchResultWorkloadSet.ManifestVersions);
        }

    }
}
