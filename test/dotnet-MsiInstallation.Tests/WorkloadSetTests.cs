// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTests : VMTestBase
    {
        readonly string SdkTestingDirectory = @"C:\SdkTesting";


        Lazy<Dictionary<string, string>> _testWorkloadSetVersions;
        string WorkloadSetVersion1 => _testWorkloadSetVersions.Value["version1"];
        string WorkloadSetVersion2 => _testWorkloadSetVersions.Value["version2"];
        string WorkloadSetPreviousBandVersion => _testWorkloadSetVersions.Value.GetValueOrDefault("previousbandversion", "8.0.204");

        public WorkloadSetTests(ITestOutputHelper log) : base(log)
        {
            _testWorkloadSetVersions = new Lazy<Dictionary<string, string>>(() =>
            {
                string remoteFilePath = @"c:\SdkTesting\workloadsets\testworkloadsetversions.json";
                var versionsFile = VM.GetRemoteFile(remoteFilePath);
                if (!versionsFile.Exists)
                {
                    throw new FileNotFoundException($"Could not find file {remoteFilePath} on VM");
                }

                return JsonSerializer.Deserialize<Dictionary<string, string>>(versionsFile.ReadAllText());
            });
        }

        [Fact]
        public void DoesNotUseWorkloadSetsByDefault()
        {
            InstallSdk();

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute()
                .Should()
                .PassWithoutWarning();

            var originalRollback = GetRollback();

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute()
                .Should()
                .PassWithoutWarning();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(originalRollback.ManifestVersions);

        }

        void UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate)
        {
            var featureBand = new SdkFeatureBand(SdkInstallerVersion).ToStringWithoutPrerelease();
            var originalWorkloadVersion = GetWorkloadVersion();
            originalWorkloadVersion.Should().StartWith($"{featureBand}-manifests.");

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute()
                .Should()
                .PassWithoutWarning();

            rollbackAfterUpdate = GetRollback();
            updatedWorkloadVersion = GetWorkloadVersion();
            updatedWorkloadVersion.Should().StartWith($"{featureBand}-manifests.");
            updatedWorkloadVersion.Should().NotBe(originalWorkloadVersion);

            GetUpdateMode().Should().Be("manifests");

            VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode", "workload-set")
                .WithDescription("Switch mode to workload-set")
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetWorkloadVersion().Should().Be(updatedWorkloadVersion);

            var expectedMessage = "Workloads are configured to install and update using workload versions, but none were found. Run \"dotnet workload restore\" to install a workload version.";

            GetDotnetInfo().Should().Contain(expectedMessage)
                .And.NotContain("(not installed)");
            GetDotnetWorkloadInfo().Should().Contain(expectedMessage)
                .And.NotContain("(not installed)");

            GetUpdateMode().Should().Be("workload-set");
        }

        [Fact]
        public void UpdateWithWorkloadSets()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string _, out WorkloadSet rollbackAfterUpdate);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);

            var newRollback = GetRollback();
            newRollback.ManifestVersions.Should().NotBeEquivalentTo(rollbackAfterUpdate.ManifestVersions);
        }

        [Fact]
        public void UpdateInWorkloadSetModeWithNoAvailableWorkloadSet()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate);

            //  Use a nonexistant source because there may be a valid workload set available on NuGet.org
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews", "--source", @"c:\SdkTesting\EmptySource")
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

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", versionToInstall, "--include-previews")
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

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
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

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", unavailableWorkloadSetVersion, "--include-previews")
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

            VM.CreateRunCommand("dotnet", "workload", "update", "--source", @"c:\SdkTesting\workloadsets", "--include-previews")
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

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion2, "--source", @"c:\SdkTesting\workloadsets", "--include-previews")
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

        void SetupWorkloadSetInGlobalJson(out WorkloadSet originalRollback)
        {
            InstallSdk();

            var versionToUpdateTo = WorkloadSetVersion2;

            string originalVersion = GetWorkloadVersion();

            originalRollback = GetRollback(SdkTestingDirectory);

            VM.WriteFile("C:\\SdkTesting\\global.json", @$"{{""sdk"":{{""workloadVersion"":""{versionToUpdateTo}""}}}}").Execute().Should().PassWithoutWarning();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be(versionToUpdateTo + " (not installed)");
            GetDotnetInfo(SdkTestingDirectory).Should().Contain("Workload version:  " + versionToUpdateTo + " (not installed)")
                .And.Contain($@"Workload version {versionToUpdateTo}, which was specified in C:\SdkTesting\global.json, was not found");
            GetDotnetWorkloadInfo(SdkTestingDirectory).Should().Contain("Workload version: " + versionToUpdateTo + " (not installed)")
                .And.Contain($@"Workload version {versionToUpdateTo}, which was specified in C:\SdkTesting\global.json, was not found");

            // The version should have changed but not yet the manifests. Since we expect both, getting the rollback should fail.
            var result = VM.CreateRunCommand("dotnet", "workload", "update", "--print-rollback")
               .WithWorkingDirectory(SdkTestingDirectory)
               .WithIsReadOnly(true)
               .Execute();

            result.Should().Fail();
            result.StdErr.Should().Contain("FileNotFoundException");
            result.StdErr.Should().Contain(versionToUpdateTo);

            AddNuGetSource(@"C:\SdkTesting\workloadsets", SdkTestingDirectory);
        }

        [Fact]
        public void RestoreWorkloadSetViaGlobalJson()
        {
            InstallSdk();

            var testProjectFolder = Path.Combine(SdkTestingDirectory, "ConsoleApp");
            VM.CreateRunCommand("dotnet", "new", "console", "-o", "ConsoleApp")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().PassWithoutWarning();

            SetupWorkloadSetInGlobalJson(out var originalRollback);

            VM.CreateRunCommand("dotnet", "workload", "restore")
                .WithWorkingDirectory(testProjectFolder)
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be(WorkloadSetVersion2);

            GetRollback(SdkTestingDirectory).Should().NotBe(originalRollback);
        }

        [Theory]
        [InlineData("update")]
        [InlineData("install")]
        public void UseGlobalJsonToSpecifyWorkloadSet(string command)
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            string[] args = command.Equals("install") ? ["dotnet", "workload", "install", "aspire"] : ["dotnet", "workload", command];
            VM.CreateRunCommand(args).WithWorkingDirectory(SdkTestingDirectory).Execute().Should().PassWithoutWarning();
            GetRollback(SdkTestingDirectory).Should().NotBe(originalRollback);
        }

        [Fact]
        public void DotnetInfoWithGlobalJson()
        {
            InstallSdk();

            //  Install a workload before setting up global.json.  Commands like "dotnet workload --info" were previously crashing if global.json specified a workload set that wasn't installed
            InstallWorkload("aspire", skipManifestUpdate: true);

            SetupWorkloadSetInGlobalJson(out _);
        }

        [Fact]
        public void InstallWithGlobalJsonAndSkipManifestUpdate()
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire", "--skip-manifest-update")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().Fail()
                .And.HaveStdErrContaining("--skip-manifest-update")
                .And.HaveStdErrContaining(Path.Combine(SdkTestingDirectory, "global.json"));
        }

        [Fact]
        public void InstallWithVersionAndSkipManifestUpdate()
        {
            InstallSdk();

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire", "--skip-manifest-update", "--version", WorkloadSetVersion1)
                .Execute().Should().Fail()
                .And.HaveStdErrContaining("--skip-manifest-update")
                .And.HaveStdErrContaining("--sdk-version");
        }

        [Fact]
        public void InstallWithVersionWhenPinned()
        {
            InstallSdk();

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            string originalVersion = GetWorkloadVersion();
            originalVersion.Should().NotBe(WorkloadSetVersion1);

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion1, "--include-previews")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion1);

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire", "--version", WorkloadSetVersion2)
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
        }

        [Fact]
        public void InstallWithGlobalJsonWhenPinned()
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            //AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            string originalVersion = GetWorkloadVersion();
            originalVersion.Should().NotBe(WorkloadSetVersion1);

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion1, "--include-previews")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion1);

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be(WorkloadSetVersion2);

            GetRollback(SdkTestingDirectory).Should().NotBe(originalRollback);

        }

        [Fact]
        public void UpdateShouldNotPinWorkloadSet()
        {
            InstallSdk();
            UpdateAndSwitchToWorkloadSetMode(out _, out _);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            var packageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(WorkloadSetVersion2, out var sdkFeatureBand);

            //  Rename latest workload set so it won't be installed
            VM.CreateActionGroup($"Disable {WorkloadSetVersion2}",
                    VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.{packageVersion}.nupkg", $"Microsoft.NET.Workloads.{sdkFeatureBand}.{packageVersion}.bak"),
                    VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.*.{packageVersion}.nupkg", $"Microsoft.NET.Workloads.{sdkFeatureBand}.*.{packageVersion}.bak"))
                .Execute().Should().PassWithoutWarning();

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion1);

            //  Bring latest workload set version back, so installing workload should update to it
            VM.CreateActionGroup($"Enable {WorkloadSetVersion2}",
                    VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.{packageVersion}.bak", $"Microsoft.NET.Workloads.{sdkFeatureBand}.{packageVersion}.nupkg"),
                    VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.*.{packageVersion}.bak", $"Microsoft.NET.Workloads.{sdkFeatureBand}.*.{packageVersion}.nupkg"))
                .Execute().Should().PassWithoutWarning();

            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
        }

        [Fact]
        public void WorkloadSetInstallationRecordIsWrittenCorrectly()
        {
            //  Should the workload set version or the package version be used in the registry?
            throw new NotImplementedException();
        }

        [Fact]
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
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
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
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
               .WithWorkingDirectory(SdkTestingDirectory)
               .Execute().Should().PassWithoutWarning();

            //  Update globally installed version to later version
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
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
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
               .Execute().Should().PassWithoutWarning();

            //  Workload set 1 should have been GC'd
            VM.GetRemoteDirectory(workloadSet1Path).Should().NotExist();
        }

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
            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews", "--version", WorkloadSetVersion2)
                .Execute()
                .Should()
                .PassWithoutWarning();

            GetRollback().ManifestVersions.Should().BeEquivalentTo(searchResultWorkloadSet.ManifestVersions);
        }

        string GetWorkloadVersion(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--version")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        string GetDotnetInfo(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "--info")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        string GetDotnetWorkloadInfo(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--info")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        string GetUpdateMode()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        void AddNuGetSource(string source, string directory = null)
        {
            VM.CreateRunCommand("dotnet", "nuget", "add", "source", source)
                .WithWorkingDirectory(directory)
                .WithDescription($"Add {source} to NuGet.config")
                .Execute()
                .Should()
                .PassWithoutWarning();
        }
    }
}
