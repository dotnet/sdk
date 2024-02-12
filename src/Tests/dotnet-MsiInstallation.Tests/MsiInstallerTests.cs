// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.DotNet.Cli.Utils;
//using System.Management;
using Microsoft.Management.Infrastructure;
using System.Xml.Linq;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadTests : SdkTest, IDisposable
    {
        //  Remote execution notes:
        //  psexec / uses Admin share (C$)
        //  ddrits / cloudtest
        //  sysinternals: filemon / regmon
        //  May need to run winrm quickconfig to use WMI
        //  How to apply snapshot via C#: https://stackoverflow.com/questions/60173096/hyperv-wmi-apply-snapshot-in-c-sharp
        //  Also see https://stackoverflow.com/questions/1735978/manipulate-hyper-v-from-net
        //  Official documentation?: https://learn.microsoft.com/en-us/windows/win32/hyperv_v2/exporting-virtual-machines
        //  How to rename a snapshot: https://stackoverflow.com/questions/7599217/setting-hyper-v-snapshots-name-programmatically


        //  Reminder: Enable "Remote Service Management" firewall rule so that PSExec will run more quickly.  Make sure network is set to "Private" in Windows settings (or enable the firewall rule for public networks).

        const string SdkInstallerVersion = "8.0.100";
        const string SdkInstallerFileName = $"dotnet-sdk-{SdkInstallerVersion}-win-x64.exe";

        const string RollbackRC1 = """
                {
                  "microsoft.net.sdk.android": "34.0.0-rc.1.432/8.0.100-rc.1",
                  "microsoft.net.sdk.ios": "16.4.8825-net8-rc1/8.0.100-rc.1",
                  "microsoft.net.sdk.maccatalyst": "16.4.8825-net8-rc1/8.0.100-rc.1",
                  "microsoft.net.sdk.macos": "13.3.8825-net8-rc1/8.0.100-rc.1",
                  "microsoft.net.sdk.maui": "8.0.0-rc.1.9171/8.0.100-rc.1",
                  "microsoft.net.sdk.tvos": "16.4.8825-net8-rc1/8.0.100-rc.1",
                  "microsoft.net.workload.mono.toolchain.current": "8.0.0-rc.1.23419.4/8.0.100-rc.1",
                  "microsoft.net.workload.emscripten.current": "8.0.0-rc.1.23415.5/8.0.100-rc.1",
                  "microsoft.net.workload.emscripten.net6": "8.0.0-rc.1.23415.5/8.0.100-rc.1",
                  "microsoft.net.workload.emscripten.net7": "8.0.0-rc.1.23415.5/8.0.100-rc.1",
                  "microsoft.net.workload.mono.toolchain.net6": "8.0.0-rc.1.23419.4/8.0.100-rc.1",
                  "microsoft.net.workload.mono.toolchain.net7": "8.0.0-rc.1.23419.4/8.0.100-rc.1"
                }
                """;

        const string Rollback8_0_101 = """
                {
                  "microsoft.net.sdk.android": "34.0.52/8.0.100",
                  "microsoft.net.sdk.ios": "17.2.8004/8.0.100",
                  "microsoft.net.sdk.maccatalyst": "17.2.8004/8.0.100",
                  "microsoft.net.sdk.macos": "14.2.8004/8.0.100",
                  "microsoft.net.sdk.maui": "8.0.3/8.0.100",
                  "microsoft.net.sdk.tvos": "17.2.8004/8.0.100",
                  "microsoft.net.workload.mono.toolchain.current": "8.0.1/8.0.100",
                  "microsoft.net.workload.emscripten.current": "8.0.1/8.0.100",
                  "microsoft.net.workload.emscripten.net6": "8.0.1/8.0.100",
                  "microsoft.net.workload.emscripten.net7": "8.0.1/8.0.100",
                  "microsoft.net.workload.mono.toolchain.net6": "8.0.1/8.0.100",
                  "microsoft.net.workload.mono.toolchain.net7": "8.0.1/8.0.100",
                  "microsoft.net.sdk.aspire": "8.0.0-preview.2.23619.3/8.0.100"
                }
                """;

        //VMControl VM { get; }
        VirtualMachine VM { get; }

        public WorkloadTests(ITestOutputHelper log) : base(log)
        {
            VM = new VirtualMachine(Log);
        }

        public void Dispose()
        {
            VM.Dispose();
        }

        [Fact]
        public void InstallSdk()
        {
            VM.CreateRunCommand("setx", "DOTNET_NOLOGO", "true")
                .WithDescription("Disable .NET SDK first run message")
                .Execute()
                .Should()
                .Pass();

            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet")
                .WithDescription($"Install SDK {SdkInstallerVersion}")
                .Execute()
                .Should()
                .Pass();

            DeployStage2Sdk();
        }

        void UninstallSdk()
        {
            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall")
                .WithDescription($"Uninstall SDK {SdkInstallerVersion}")
                .Execute()
                .Should()
                .Pass();
        }

        private CommandResult ApplyManifests(string manifestContents, string rollbackID)
        {
            CommandResult result = VM.CreateActionGroup("Rollback to " + rollbackID + " manifests",
                    VM.WriteFile($@"c:\SdkTesting\rollback-{rollbackID}.json", manifestContents),
                    VM.CreateRunCommand("dotnet", "workload", "update", "--from-rollback-file", $@"c:\SdkTesting\rollback-{rollbackID}.json", "--skip-sign-check"))
                .Execute();
            result.Should().Pass();
            return result;
        }

        CommandResult ApplyRC1Manifests()
        {
            return ApplyManifests(RollbackRC1, "rc1");
        }

        CommandResult Apply8_0_101Manifests()
        {
            return ApplyManifests(Rollback8_0_101, "8.0.101");
        }

        [Fact]
        public void InstallWasm()
        {
            InstallSdk();

            ApplyRC1Manifests();

            InstallWorkload("wasm-tools");
        }

        [Fact]
        public void InstallAndroid()
        {
            InstallSdk();

            ApplyRC1Manifests();

            InstallWorkload("android");
        }

        [Fact]
        public void InstallAndroidAndWasm()
        {
            InstallSdk();

            ApplyRC1Manifests();

            InstallWorkload("android");

            InstallWorkload("wasm-tools");
        }

        [Fact]
        public async Task UseWMI()
        {
            var snapshots = VM.VMControl.GetSnapshots();

            foreach (var snapshot in snapshots)
            {
                Log.WriteLine(snapshot.id + ": " + snapshot.name);

                //await vm.RenameSnapshot(snapshot.id, snapshot.name + " - renamed");
            }

            //await VM.CreateSnapshotAsync("New test snapshot");

            //await VM.ApplySnapshotAsync("9BA74A78-D221-436E-9875-0A3BF86CEF4A");

            await Task.Yield();
        }

        [Fact]
        public void SdkInstallation()
        {
            GetInstalledSdkVersion().Should().Be("7.0.401");

            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet")
                .WithDescription($"Install SDK {SdkInstallerVersion}")
                .Execute()
                .Should()
                .Pass();

            VM.GetRemoteDirectory($@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}")
                .Should()
                .Exist();

            GetInstalledSdkVersion().Should().Be(SdkInstallerVersion);

            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall")
                .Execute()
                .Should()
                .Pass();

            VM.GetRemoteDirectory($@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}")
                .Should()
                .NotExist();

            GetInstalledSdkVersion().Should().Be("7.0.401");
        }


        [Fact]
        public void WorkloadInstallationAndGarbageCollection()
        {
            InstallSdk();

            var originalManifests = GetRollback();

            InstallWorkload("wasm-tools");

            ListWorkloads().Should().Contain("wasm-tools");

            CheckForDuplicateManifests();

            ApplyRC1Manifests();

            Apply8_0_101Manifests();

            HashSet<(string id, string version, string featureBand)> expectedManifests = new();
            foreach (var kvp in originalManifests.ManifestVersions.Concat(WorkloadSet.FromJson(Rollback8_0_101, new SdkFeatureBand(SdkInstallerVersion)).ManifestVersions))
            {
                expectedManifests.Add((kvp.Key.ToString(), kvp.Value.Version.ToString(), kvp.Value.FeatureBand.ToString()));
            }

            var unexpectedManifests = GetInstalledManifestVersions()
                .SelectMany(kvp => kvp.Value.Select(v => (id: kvp.Key, version: v.version, featureBand: v.sdkFeatureBand)))
                .Except(expectedManifests);

            if (unexpectedManifests.Any())
            {
                Assert.Fail($"Unexpected manifests installed:\r\n{string.Join(Environment.NewLine, unexpectedManifests.Select(m => $"{m.id} {m.version}/{m.featureBand}"))}");
            }
        }

        [Fact]
        public void InstallStateShouldBeRemovedOnSdkUninstall()
        {
            InstallSdk();
            InstallWorkload("wasm-tools");
            ApplyRC1Manifests();
            var featureBand = new SdkFeatureBand(SdkInstallerVersion);
            var installStatePath = $@"c:\ProgramData\dotnet\workloads\{featureBand}\InstallState\default.json";
            VM.GetRemoteFile(installStatePath).Should().Exist();
            UninstallSdk();
            VM.GetRemoteFile(installStatePath).Should().NotExist();
        }

        [Fact]
        public void RepeatedUpdateToSameRollbackFile()
        {
            InstallSdk();
            InstallWorkload("wasm-tools");
            ApplyRC1Manifests();
            ApplyRC1Manifests()
                .Should()
                .NotHaveStdOutContaining("Installing");
        }

        [Fact]
        public void ShouldNotShowRebootMessage()
        {
            throw new NotImplementedException();
        }

        string GetInstalledSdkVersion()
        {
            var command = VM.CreateRunCommand("dotnet", "--version");
            command.IsReadOnly = true;
            var result = command.Execute();
            result.Should().Pass();
            return result.StdOut;
        }

        void CheckForDuplicateManifests()
        {
            var installedManifestVersions = GetInstalledManifestVersions();

            foreach (var (manifestId, installedVersions) in installedManifestVersions)
            {
                if (installedVersions.Count > 1)
                {
                    Assert.Fail($"Found multiple manifest versions for {manifestId}: {string.Join(", ", installedVersions)}");
                }
            }
        }

        Dictionary<string, List<(string version, string sdkFeatureBand)>> GetInstalledManifestVersions()
        {
            Dictionary<string, List<(string version, string sdkFeatureBand)>> installedManifestVersions = new();

            var manifestsRoot = VM.GetRemoteDirectory($@"c:\Program Files\dotnet\sdk-manifests");
            
            foreach (var manifestFeatureBandPath in manifestsRoot.Directories)
            {
                var manifestFeatureBand = Path.GetFileName(manifestFeatureBandPath);
                if (manifestFeatureBand.Equals("7.0.100"))
                {
                    //  Skip manifests from SDK pre-installed with VS on dev image
                    continue;
                }

                foreach (var manifestIdPath in VM.GetRemoteDirectory(manifestFeatureBandPath).Directories)
                {
                    var manifestId = Path.GetFileName(manifestIdPath);
                    VM.GetRemoteFile(Path.Combine(manifestIdPath, "WorkloadManifest.json"))
                        .Should().NotExist("Not expecting non side-by-side workload manifests");

                    foreach (var manifestVersionPath in VM.GetRemoteDirectory(manifestIdPath).Directories)
                    {
                        VM.GetRemoteFile(Path.Combine(manifestVersionPath, "WorkloadManifest.json"))
                            .Should().Exist("Workload manifest should exist");

                        var manifestVersion = Path.GetFileName(manifestVersionPath);
                        if (!installedManifestVersions.TryGetValue(manifestId, out var installedVersions))
                        {
                            installedVersions = new();
                            installedManifestVersions[manifestId] = installedVersions;
                        }
                        installedVersions.Add((manifestVersion, manifestFeatureBand));
                    }
                }
            }

            return installedManifestVersions;
        }

        void DeployStage2Sdk()
        {
            Log.WriteLine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest);

            var installedSdkFolder = $@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}";

            var vmVersionFilePath = Path.Combine(installedSdkFolder, ".version");

            var existingVersionFileContents = VM.GetRemoteFile(vmVersionFilePath).ReadAllText().Split(Environment.NewLine);
            var newVersionFileContents = File.ReadAllLines(Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, ".version"));
            newVersionFileContents[1] = existingVersionFileContents[1];

            //  TODO: It would be nice if the description included the date/time of the SDK build, to distinguish different snapshots
            VM.CreateActionGroup("Deploy Stage 2 SDK",
                    VM.CopyFolder(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, installedSdkFolder),
                    VM.WriteFile(vmVersionFilePath, string.Join(Environment.NewLine, newVersionFileContents)))
                .Execute()
                .Should()
                .Pass();
        }

        CommandResult InstallWorkload(string workloadName)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "install", workloadName, "--skip-manifest-update")
                    .WithDescription($"Install {workloadName} workload")
                    .Execute();

            result.Should().Pass();

            return result;
        }

        string ListWorkloads()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "list", "--machine-readable")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;            
        }

        WorkloadSet GetRollback()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "update", "--print-rollback")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return ParseRollbackOutput(result.StdOut);
        }

        WorkloadSet ParseRollbackOutput(string output)
        {
            var filteredOutput = string.Join(Environment.NewLine,
                output.Split(Environment.NewLine)
                .Except(["==workloadRollbackDefinitionJsonOutputStart==", "==workloadRollbackDefinitionJsonOutputEnd=="]));

            return WorkloadSet.FromJson(filteredOutput, defaultFeatureBand: new SdkFeatureBand(SdkInstallerVersion));
        }
    }
}
