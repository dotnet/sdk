// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadSetTestsBase : VMTestBase
    {
        protected readonly string SdkTestingDirectory = @"C:\SdkTesting";


        protected Lazy<Dictionary<string, string>> _testWorkloadSetVersions;
        protected string WorkloadSetVersion1 => _testWorkloadSetVersions.Value["version1"];
        protected string WorkloadSetVersionPreview => _testWorkloadSetVersions.Value["versionpreview"];
        protected string WorkloadSetVersion2 => _testWorkloadSetVersions.Value["version2"];
        protected string WorkloadSetPreviousBandVersion => _testWorkloadSetVersions.Value.GetValueOrDefault("previousbandversion", "8.0.204");

        protected override bool NeedsIncludePreviews => bool.Parse(_testWorkloadSetVersions.Value.GetValueOrDefault("needsIncludePreviews", "false"));
        public WorkloadSetTestsBase(ITestOutputHelper log) : base(log)
        {
            _testWorkloadSetVersions = new Lazy<Dictionary<string, string>>(() =>
            {
                string remoteFilePath = @"c:\SdkTesting\workloadsets\testworkloadsetversions.json";
                var versionsFile = VM.GetRemoteFile(remoteFilePath, mustExist: true);
                if (!versionsFile.Exists)
                {
                    throw new FileNotFoundException($"Could not find file {remoteFilePath} on VM");
                }

                return JsonSerializer.Deserialize<Dictionary<string, string>>(versionsFile.ReadAllText());
            });
        }

        protected void UpdateAndSwitchToWorkloadSetMode(out string updatedWorkloadVersion, out WorkloadSet rollbackAfterUpdate)
        {
            var featureBand = new SdkFeatureBand(SdkInstallerVersion).ToStringWithoutPrerelease();
            var originalWorkloadVersion = GetWorkloadVersion();
            originalWorkloadVersion.Should().StartWith($"{featureBand}-manifests.");

            CreateInstallingCommand("dotnet", "workload", "update")
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

        internal string GetWorkloadVersion(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--version")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        internal string GetDotnetInfo(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "--info")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        internal string GetDotnetWorkloadInfo(string workingDirectory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--info")
                .WithWorkingDirectory(workingDirectory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        internal string GetUpdateMode()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "config", "--update-mode")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().PassWithoutWarning();

            return result.StdOut;
        }

        internal void AddNuGetSource(string source, string directory = null)
        {
            VM.CreateRunCommand("dotnet", "nuget", "add", "source", source)
                .WithWorkingDirectory(directory)
                .WithDescription($"Add {source} to NuGet.config")
                .Execute()
                .Should()
                .PassWithoutWarning();
        }

        //  Creates a command and possibly adds "--include-previews" to the argument list
        internal VMRunAction CreateInstallingCommand(params string[] args)
        {
            if (NeedsIncludePreviews)
            {
                args = [.. args, "--include-previews"];
            }
            return VM.CreateRunCommand(args);
        }

        //  Moves workload set packages for a given version from C:\SdkTesting\WorkloadSets to C:\SdkTesting\DisabledWorkloadSets
        protected void RemoveWorkloadSetFromLocalSource(string workloadSetVersion)
        {
            var packageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(workloadSetVersion, out var sdkFeatureBand);

            VM.CreateActionGroup($"Disable {workloadSetVersion}",
                VM.CreateRunCommand("cmd", "/c", "mkdir", @"c:\SdkTesting\DisabledWorkloadSets"),
                VM.CreateRunCommand("cmd", "/c", "move", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.{packageVersion}.nupkg", @"c:\SdkTesting\DisabledWorkloadSets"),
                VM.CreateRunCommand("cmd", "/c", "move", @$"c:\SdkTesting\WorkloadSets\Microsoft.NET.Workloads.{sdkFeatureBand}.*.{packageVersion}.nupkg", @"c:\SdkTesting\DisabledWorkloadSets"))
            .Execute().Should().PassWithoutWarning();
        }
    }
}
