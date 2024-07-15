﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTests : VMTestBase
    {
        readonly string SdkTestingDirectory = @"C:\SdkTesting";

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
                .Execute().Should().Pass();
            
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
            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be(versionToInstall);

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24217.2");
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
                .And.HaveStdErrContaining(unavailableWorkloadSetVersion)
                .And.NotHaveStdOutContaining("Installation rollback failed");

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
                .Pass()
                .And.HaveStdOutContaining("No workload update found");

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

        void SetupWorkloadSetInGlobalJson(out WorkloadSet originalRollback)
        {
            InstallSdk();

            var versionToUpdateTo = "8.0.300-preview.0.24217.2";

            string originalVersion = GetWorkloadVersion();

            originalRollback = GetRollback(SdkTestingDirectory);

            VM.WriteFile("C:\\SdkTesting\\global.json", @$"{{""sdk"":{{""workloadVersion"":""{versionToUpdateTo}""}}}}").Execute().Should().Pass();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be(versionToUpdateTo);

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
        public void UpdateWorkloadSetViaGlobalJson()
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            VM.CreateRunCommand("dotnet", "workload", "update").WithWorkingDirectory(SdkTestingDirectory).Execute().Should().Pass();
            GetRollback(SdkTestingDirectory).Should().NotBe(originalRollback);
        }

        [Fact]
        public void InstallWorkloadSetViaGlobalJson()
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().Pass();

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

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire", "--skip-manifest-update", "--version", "8.0.300-preview.0.24178.1")
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
            originalVersion.Should().NotBe("8.0.300-preview.0.24178.1");

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", "8.0.300-preview.0.24178.1")
                .Execute().Should().Pass();

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24178.1");

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire", "--version", "8.0.300-preview.0.24217.2")
                .Execute().Should().Pass();

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24217.2");
        }

        [Fact]
        public void InstallWithGlobalJsonWhenPinned()
        {
            SetupWorkloadSetInGlobalJson(out var originalRollback);

            //AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            string originalVersion = GetWorkloadVersion();
            originalVersion.Should().NotBe("8.0.300-preview.0.24178.1");

            VM.CreateRunCommand("dotnet", "workload", "update", "--version", "8.0.300-preview.0.24178.1")
                .Execute().Should().Pass();

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24178.1");

            VM.CreateRunCommand("dotnet", "workload", "install", "aspire")
                .WithWorkingDirectory(SdkTestingDirectory)
                .Execute().Should().Pass();

            GetWorkloadVersion(SdkTestingDirectory).Should().Be("8.0.300-preview.0.24217.2");

            GetRollback(SdkTestingDirectory).Should().NotBe(originalRollback);

        }

        [Fact]
        public void UpdateShouldNotPinWorkloadSet()
        {
            InstallSdk();
            UpdateAndSwitchToWorkloadSetMode(out _, out _);

            AddNuGetSource(@"c:\SdkTesting\WorkloadSets");

            //  Rename latest workload set so it won't be installed
            VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.8.0.300-preview.*.24217.2.nupkg", $"Microsoft.NET.Workloads.8.0.300-preview.*.24217.2.bak")
                .Execute().Should().Pass();

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute().Should().Pass();

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24178.1");

            //  Bring latest workload set version back, so installing workload should update to it
            VM.CreateRunCommand("cmd", "/c", "ren", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.8.0.300-preview.*.24217.2.bak", $"Microsoft.NET.Workloads.8.0.300-preview.*.24217.2.nupkg")
                .Execute().Should().Pass();

            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be("8.0.300-preview.0.24217.2");
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

        string GetWorkloadVersion(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--version")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;
        }
        string GetUpdateMode()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;
        }

        void AddNuGetSource(string source, string directory = null)
        {
            VM.CreateRunCommand("dotnet", "nuget", "add", "source", source)
                .WithWorkingDirectory(directory)
                .WithDescription($"Add {source} to NuGet.config")
                .Execute()
                .Should()
                .Pass();
        }
    }
}
