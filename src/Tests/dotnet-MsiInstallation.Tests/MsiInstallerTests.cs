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


        //  Reminder: Enable "Remote Service Management" firewall rule so that PSExec will run more quickly

        const string TargetMachineName = "dsp-vm";
        const string PsExecPath = @"C:\Users\Daniel\Downloads\PSTools\PsExec.exe";
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
            //VM = new VMControl(Log);
            VM = new VirtualMachine(Log);
        }

        public void Dispose()
        {
            VM.Dispose();
        }

        [Fact]
        public void InstallSdk()
        {
            VM.RunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet")
                .Should()
                .Pass();
        }

        [Fact]
        public void InstallWasm()
        {
            InstallSdk();

            VM.WriteFile($@"C:\SdkTesting\rollback-rc1.json", RollbackRC1);
            VM.RunCommand("dotnet", "workload", "update", "--from-rollback-file", @"c:\SdkTesting\rollback-rc1.json", "--skip-sign-check");

            VM.RunCommand("dotnet", "workload", "install", "wasm-tools", "--skip-manifest-update")
                .Should()
                .Pass();
        }

        [Fact]
        public void InstallAndroid()
        {
            InstallSdk();

            VM.WriteFile($@"C:\SdkTesting\rollback-rc1.json", RollbackRC1);
            VM.RunCommand("dotnet", "workload", "update", "--from-rollback-file", @"c:\SdkTesting\rollback-rc1.json", "--skip-sign-check");

            VM.RunCommand("dotnet", "workload", "install", "Android", "--skip-manifest-update")
                .Should()
                .Pass();
        }

        [Fact]
        public void InstallAndroidAndWasm()
        {
            InstallSdk();

            VM.WriteFile($@"C:\SdkTesting\rollback-rc1.json", RollbackRC1);
            VM.RunCommand("dotnet", "workload", "update", "--from-rollback-file", @"c:\SdkTesting\rollback-rc1.json", "--skip-sign-check");

            VM.RunCommand("dotnet", "workload", "install", "Android", "--skip-manifest-update")
                .Should()
                .Pass();

            VM.RunCommand("dotnet", "workload", "install", "wasm-tools", "--skip-manifest-update")
                .Should()
                .Pass();
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

            await VM.VMControl.RenameSnapshotAsync("D258F2C9-F4BE-47F7-8D9C-DF5D955B84BC", "Can I rename this?");

            //await VM.CreateSnapshotAsync("New test snapshot");

            //await VM.ApplySnapshotAsync("9BA74A78-D221-436E-9875-0A3BF86CEF4A");

            await Task.Yield();
        }

        //[Fact(Skip = "testing")]
        //public void UninstallSdk()
        //{
        //    RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall");
        //}

        //[Fact(Skip = "testing")]
        //public void SdkInstallation()
        //{
        //    RunRemoteCommand("dotnet", "--version")
        //        .Should()
        //        .HaveStdOut("7.0.401");

        //    RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet");

        //    new DirectoryInfo($@"\\{TargetMachineName}\c$\Program Files\dotnet\sdk\{SdkInstallerVersion}")
        //        .Should()
        //        .Exist();

        //    RunRemoteCommand("dotnet", "--version")
        //        .Should()
        //        .HaveStdOut(SdkInstallerVersion);

        //    RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall");

        //    new DirectoryInfo($@"\\{TargetMachineName}\c$\Program Files\dotnet\sdk\{SdkInstallerVersion}")
        //        .Should()
        //        .NotExist();

        //    RunRemoteCommand("dotnet", "--version")
        //        .Should()
        //        .HaveStdOut("7.0.401");
        //}

        //[Fact]
        //public void SdkInstallation2()
        //{
        //    //VM.RunCommand("dotnet", "--version")
        //    //    .Should()
        //    //    .HaveStdOut("7.0.401");

        //    //VM.RunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet");

        //    //new DirectoryInfo($@"\\{TargetMachineName}\c$\Program Files\dotnet\sdk\{SdkInstallerVersion}")
        //    //    .Should()
        //    //    .Exist();

        //    //RunRemoteCommand("dotnet", "--version")
        //    //    .Should()
        //    //    .HaveStdOut(SdkInstallerVersion);

        //    //RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall");

        //    //new DirectoryInfo($@"\\{TargetMachineName}\c$\Program Files\dotnet\sdk\{SdkInstallerVersion}")
        //    //    .Should()
        //    //    .NotExist();

        //    //RunRemoteCommand("dotnet", "--version")
        //    //    .Should()
        //    //    .HaveStdOut("7.0.401");

        //}


        //[Fact(Skip = "testing")]
        //public void WorkloadInstallation()
        //{
        //    //CleanupInstallState();

        //    //RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet");

        //    //DeployStage2Sdk();

        //    var rollbackResult = RunRemoteCommand("dotnet", "workload", "update", "--print-rollback");
        //    rollbackResult.Should().Pass();
        //    var originalManifests = ParseRollbackOutput(rollbackResult.StdOut);
           

        //    RunRemoteCommand("dotnet", "workload", "install", "wasm-tools", "--skip-manifest-update");

        //    RunRemoteCommand("dotnet", "workload", "list", "--machine-readable")
        //        .Should()
        //        .HaveStdOutContaining("wasm-tools");

        //    CheckForDuplicateManifests();

        //    File.WriteAllText($@"\\{TargetMachineName}\c$\SdkTesting\rollback-rc1.json", RollbackRC1);

        //    RunRemoteCommand("dotnet", "workload", "update", "--from-rollback-file", @"c:\SdkTesting\rollback-rc1.json", "--skip-sign-check");

        //    File.WriteAllText($@"\\{TargetMachineName}\c$\SdkTesting\rollback-8.0.101.json", Rollback8_0_101);

        //    RunRemoteCommand("dotnet", "workload", "update", "--from-rollback-file", @"c:\SdkTesting\rollback-8.0.101.json", "--skip-sign-check");

        //    HashSet<(string id, string version, string featureBand)> expectedManifests = new();
        //    foreach (var kvp in originalManifests.ManifestVersions.Concat(WorkloadSet.FromJson(Rollback8_0_101, new SdkFeatureBand(SdkInstallerVersion)).ManifestVersions))
        //    {
        //        expectedManifests.Add((kvp.Key.ToString(), kvp.Value.Version.ToString(), kvp.Value.FeatureBand.ToString()));
        //    }

        //    var unexpectedManifests = GetInstalledManifestVersions()
        //        .SelectMany(kvp => kvp.Value.Select(v => (id: kvp.Key, version: v.version, featureBand: v.sdkFeatureBand)))
        //        .Except(expectedManifests);

        //    if (unexpectedManifests.Any())
        //    {
        //        Assert.Fail($"Unexpected manifests installed: {string.Join(", ", unexpectedManifests)}");
        //    }

        //    //CheckForDuplicateManifests();

        //    //RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall");
        //}

        //[Fact(Skip = "testing")]
        //public void InstallStateShouldBeRemovedOnSdkUninstall()
        //{
        //    //  This is currently broken, needs to be added to the finalizer
        //    throw new NotImplementedException();
        //}

        //[Fact(Skip = "testing")]
        //public void RepeatedUpdateToSameRollbackFile()
        //{
        //    //  Should not install or uninstall anything
        //}

        void CleanupInstallState()
        {
            var featureBand = new SdkFeatureBand(SdkInstallerVersion);
            string installStatePath = $@"\\{TargetMachineName}\c$\ProgramData\dotnet\workloads\{featureBand}\InstallState\default.json";
            if (File.Exists(installStatePath))
            {
                File.Delete(installStatePath);
            }
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
                //installedVersions.Count.Should().Be(1, $"Only one version of manifest {manifestId} should be installed");
            }
        }

        Dictionary<string, List<(string version, string sdkFeatureBand)>> GetInstalledManifestVersions()
        {
            Dictionary<string, List<(string version, string sdkFeatureBand)>> installedManifestVersions = new();

            foreach (var manifestFeatureBandPath in Directory.GetDirectories($@"\\{TargetMachineName}\c$\Program Files\dotnet\sdk-manifests"))
            {
                var manifestFeatureBand = Path.GetFileName(manifestFeatureBandPath);
                if (manifestFeatureBand.Equals("7.0.100"))
                {
                    //  Skip manifests from SDK pre-installed with VS on dev image
                    continue;
                }

                foreach (var manifestIdPath in Directory.GetDirectories(manifestFeatureBandPath))
                {
                    var manifestId = Path.GetFileName(manifestIdPath);
                    new FileInfo(Path.Combine(manifestIdPath, "WorkloadManifest.json"))
                        .Should().NotExist("Not expecting non side-by-side workload manifests");

                    foreach (var manifestVersionPath in Directory.GetDirectories(manifestIdPath))
                    {
                        new FileInfo(Path.Combine(manifestVersionPath, "WorkloadManifest.json"))
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

        [Fact]
        void TempTest()
        {
            //CleanupInstallState();
            RunRemoteCommand("dotnet", "--version")
                .Should()
                .HaveStdOut("7.0.401");

            //RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet");
            //DeployStage2Sdk();
            //CleanupInstallState();
            //RunRemoteCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall");
        }

        void DeployStage2Sdk()
        {
            Log.WriteLine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest);

            
            var existingSdkFolder = $@"\\dsp-vm\c$\Program Files\dotnet\sdk\{SdkInstallerVersion}";
            //var targetSdkFolder = $@"\\dsp-vm\c$\Program Files\dotnet\sdk\{TestContext.Current.ToolsetUnderTest.SdkVersion}";
            var targetSdkFolder = existingSdkFolder;
            var backupSdkFolder = $@"\\dsp-vm\c$\SdkTesting\backup\{SdkInstallerVersion}";

            var existingVersionFileContents = File.ReadAllLines(Path.Combine(existingSdkFolder, ".version"));

            if (!Directory.Exists(backupSdkFolder))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupSdkFolder));
                Directory.Move(existingSdkFolder, backupSdkFolder);
            }
            else if (Directory.Exists(targetSdkFolder))
            {
                Directory.Delete(targetSdkFolder, true);
            }

            CopyDirectory(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, targetSdkFolder);

            var newVersionFileContents = File.ReadAllLines(Path.Combine(targetSdkFolder, ".version"));
            //  Change feature band for deployed SDK to match MSI installation version
            newVersionFileContents[1] = existingVersionFileContents[1];
            File.WriteAllLines(Path.Combine(targetSdkFolder, ".version"), newVersionFileContents);
        }

        WorkloadSet ParseRollbackOutput(string output)
        {
            var filteredOutput = string.Join(Environment.NewLine,
                output.Split(Environment.NewLine)
                .Except(["==workloadRollbackDefinitionJsonOutputStart==", "==workloadRollbackDefinitionJsonOutputEnd=="]));

            return WorkloadSet.FromJson(filteredOutput, defaultFeatureBand: new SdkFeatureBand(SdkInstallerVersion));
        }


        private static void CopyDirectory(string sourcePath, string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                CopyDirectory(dir, Path.Combine(destPath, Path.GetFileName(dir)));
            }

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                new FileInfo(file).CopyTo(Path.Combine(destPath, Path.GetFileName(file)), true);
            }
        }


        CommandResult RunRemoteCommand(params string[] args)
        {
            var result = new RemoteCommand(Log, args).Execute();

            result.Should().Pass();

            return result;
        }

        

        class RemoteCommand : TestCommand
        {


            public RemoteCommand(ITestOutputHelper log, params string[] args)
                : base(log)
            {
                Arguments.Add("-nobanner");
                Arguments.Add($@"\\{TargetMachineName}");
                Arguments.AddRange(args);
            }

            protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
            {
                var sdkCommandSpec = new SdkCommandSpec()
                {
                    FileName = PsExecPath,
                    Arguments = args.ToList(),
                    WorkingDirectory = WorkingDirectory,
                };
                return sdkCommandSpec;
            }
        }
    }
}
