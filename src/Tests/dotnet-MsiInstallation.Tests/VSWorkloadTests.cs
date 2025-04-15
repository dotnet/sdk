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
    public class VSWorkloadTests : VMTestBase
    {
        public VSWorkloadTests(ITestOutputHelper log) : base(log)
        {
            VM.SetCurrentState("Install VS 17.10 Preview 6");
            DeployStage2Sdk();
        }

        [Fact]
        public void WorkloadListShowsVSInstalledWorkloads()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "list")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            result.Should().HaveStdOutContaining("aspire");
        }

        [Fact]
        public void UpdatesAreAdvertisedForVSInstalledWorkloads()
        {
            AddNuGetSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json");

            VM.CreateRunCommand("dotnet", "new", "classlib", "-o", "LibraryTest")
                .WithWorkingDirectory(@"C:\SdkTesting")
                .Execute()
                .Should()
                .Pass();

            //  build (or any restoring) command should check for and notify of updates
            VM.CreateRunCommand("dotnet", "build")
                .WithWorkingDirectory(@"C:\SdkTesting\LibraryTest")
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Workload updates are available");

            //  Workload list should list the specific workloads that have updates
            VM.CreateRunCommand("dotnet", "workload", "list")
                .WithIsReadOnly(true)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Updates are available for the following workload(s): aspire");
        }
    }
}
