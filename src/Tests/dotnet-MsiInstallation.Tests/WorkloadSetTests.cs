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
    public class WorkloadSetTests : VMTestBase
    {
        public WorkloadSetTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void DoesNotUseWorkloadSetsByDefault()
        {
            InstallSdk();

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var originalRollback = GetRollback();

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(originalRollback.ManifestVersions);

        }

        void UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate)
        {
            var featureBand = new SdkFeatureBand(SdkInstallerVersion).ToStringWithoutPrerelease();
            var originalWorkloadVersion = GetWorkloadVersion();
            originalWorkloadVersion.Should().StartWith($"{featureBand}-manifests.");

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            rollbackAfterUpdate = GetRollback();
            updatedWorkloadVersion = GetWorkloadVersion();
            updatedWorkloadVersion.Should().StartWith($"{featureBand}-manifests.");
            updatedWorkloadVersion.Should().NotBe(originalWorkloadVersion);

            GetUpdateMode().Should().Be("manifests");

            VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode", "workload-set")
                .WithDescription("Switch mode to workload-set")
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(updatedWorkloadVersion);

            GetUpdateMode().Should().Be("workload-set");
        }

        [Fact]
        public void UpdateWithWorkloadSets()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string _, out WorkloadSet rollbackAfterUpdate);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();
            
            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().NotBeEquivalentTo(rollbackAfterUpdate.ManifestVersions);

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24217.2");
        }

        [Fact]
        public void UpdateInWorkloadSetModeWithNoAvailableWorkloadSet()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate);

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(rollbackAfterUpdate.ManifestVersions);

            GetWorkloadVersion().Should().Be(updatedWorkloadVersion);
        }

        [Theory]
        [InlineData("8.0.300-preview.0.24178.1")]
        [InlineData("8.0.204")]
        public void UpdateToSpecificWorkloadSetVersion(string versionToInstall)
        {
            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();
            workloadVersionBeforeUpdate.Should().NotBe(versionToInstall);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", versionToInstall)
                .Execute()
                .Should()
                .Pass();

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(versionToInstall);

            //  Installing a workload shouldn't update workload version
            InstallWorkload("aspire");

            GetWorkloadVersion().Should().Be(versionToInstall);
        }

        [Fact]
        public void UpdateToUnavailableWorkloadSetVersion()
        {
            string unavailableWorkloadSetVersion = "8.0.300-preview.test.42";

            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", unavailableWorkloadSetVersion)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(unavailableWorkloadSetVersion);

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }


        [Fact]
        public void UpdateWorkloadSetWithoutAvailableManifests()
        {
            InstallSdk();

            UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate);

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            VM.CreateRunCommand("dotnet", "workload", "update", "--source", @"c:\SdkTesting\workloadsets")
                .Execute()
                .Should()
                .Fail();


            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }

        [Fact]
        public void UpdateToWorkloadSetVersionWithManifestsNotAvailable()
        {
            InstallSdk();

            var workloadVersionBeforeUpdate = GetWorkloadVersion();

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", @"8.0.300-preview.0.24217.2", "--source", @"c:\SdkTesting\workloadsets")
                .Execute()
                .Should()
                .Fail();

            VM.CreateRunCommand("dotnet", "workload", "search")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(workloadVersionBeforeUpdate);
        }

        string GetUpdateMode()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;
        }
    }
}
