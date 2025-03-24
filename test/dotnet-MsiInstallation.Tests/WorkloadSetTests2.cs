// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTests2 : WorkloadSetTestsBase
    {
        public WorkloadSetTests2(ITestOutputHelper log) : base(log)
        {
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

            CreateInstallingCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion1)
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

            CreateInstallingCommand("dotnet", "workload", "update", "--version", WorkloadSetVersion1)
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(WorkloadSetVersion1);

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be(WorkloadSetVersion2);

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UpdateDoesNotTryToInstallOlderWorkloadSet(bool usePreview)
        {
            if (NeedsIncludePreviews && usePreview)
            {
                //  This version of the test can't run when all of the test workload sets are previews
                return;
            }

            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string _, out WorkloadSet rollbackAfterUpdate);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            if (usePreview)
            {
                RemoveWorkloadSetFromLocalSource(WorkloadSetVersion2);
            }

            VM.CreateRunCommand("dotnet", "workload", "update", "--include-previews")
                .Execute().Should().PassWithoutWarning();

            GetWorkloadVersion().Should().Be(usePreview ? WorkloadSetVersionPreview : WorkloadSetVersion2);

            if (!usePreview)
            {
                RemoveWorkloadSetFromLocalSource(WorkloadSetVersion2);
            }

            InstallWorkload("aspire", skipManifestUpdate: false)
                .Should().NotHaveStdOutContaining("Installing workload version")
                .And.NotHaveStdOutContaining("microsoft.net.workloads.");

            if (usePreview)
            {
                GetWorkloadVersion().Should().Be(WorkloadSetVersionPreview);
            }
            else
            {
                GetWorkloadVersion().Should().Be(WorkloadSetVersion2);
            }
        }
    }
}
