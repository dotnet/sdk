// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.MsiInstallerTests.Framework;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTests : VMTestBase
    {
        public WorkloadSetTests(ITestOutputHelper log) : base(log)
        {
        }

        //  dotnet nuget add source c:\SdkTesting\WorkloadSets
        //  dotnet workload update --mode workloadset

        //  Show workload mode in dotnet workload --info


        //  dotnet workload update-mode set workload-set

        //  dotnet workload config --update-mode workload-set

        //  dotnet workload config update-mode
        //  dotnet workload config update-mode workload-set
        //  dotnet workload config update-mode manifests

        //  dotnet workload config update-band [default|release|preview|daily]

        //  dotnet config workload.update-mode workload-set

        //  dotnet setconfig --workload-update-mode workload-set

        [Fact]
        public void DoesNotUseWorkloadSetsByDefault()
        {
            InstallSdk();

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var originalRollback = GetRollback();

            VM.CreateRunCommand("dotnet", "nuget", "add", "source", @"c:\SdkTesting\WorkloadSets")
                .WithDescription("Add WorkloadSets to NuGet.config")
                .Execute()
                .Should()
                .Pass();

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().BeEquivalentTo(originalRollback.ManifestVersions);

        }

        [Fact]
        public void UpdateWithWorkloadSets()
        {
            InstallSdk();

            var originalWorkloadVersion = GetWorkloadVersion();
            originalWorkloadVersion.Should().StartWith("8.0.200-manifests.");

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var originalRollback = GetRollback();
            var updatedWorkloadVersion = GetWorkloadVersion();
            updatedWorkloadVersion.Should().StartWith("8.0.200-manifests.");
            updatedWorkloadVersion.Should().NotBe(originalWorkloadVersion);

            VM.CreateRunCommand("dotnet", "nuget", "add", "source", @"c:\SdkTesting\WorkloadSets")
                .WithDescription("Add WorkloadSets to NuGet.config")
                .Execute()
                .Should()
                .Pass();

            GetUpdateMode().Should().Be("manifests");

            VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode", "workload-set")
                .WithDescription("Switch mode to workload-set")
                .Execute()
                .Should()
                .Pass();

            GetWorkloadVersion().Should().Be(updatedWorkloadVersion);

            GetUpdateMode().Should().Be("workload-set");

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should()
                .Pass();

            var newRollback = GetRollback();

            newRollback.ManifestVersions.Should().NotBeEquivalentTo(originalRollback.ManifestVersions);

            GetWorkloadVersion().Should().Be("8.0.201");

        }

        string GetWorkloadVersion()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--version")
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
    }
}
