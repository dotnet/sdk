// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.MsiInstallerTests.Framework;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests
{
    public class WorkloadTests : VMTestBase
    {
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

        public WorkloadTests(ITestOutputHelper log) : base(log)
        {
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

            InstallWorkload("wasm-tools", skipManifestUpdate: true);
        }

        [Fact]
        public void InstallAndroid()
        {
            InstallSdk();

            ApplyRC1Manifests();

            InstallWorkload("android", skipManifestUpdate: true);
        }

        [Fact]
        public void InstallAndroidAndWasm()
        {
            InstallSdk();

            ApplyRC1Manifests();

            InstallWorkload("android", skipManifestUpdate: true);

            InstallWorkload("wasm-tools", skipManifestUpdate: true);
        }

        [Fact]
        public void SdkInstallation()
        {
            var command = VM.CreateRunCommand("dotnet", "--version");
            command.IsReadOnly = true;

            string originalSdkVersion;
            var versionResult = command.Execute();
            if (versionResult.ExitCode == 0)
            {
                originalSdkVersion = versionResult.StdOut;
            }
            else
            {
                originalSdkVersion = null;
            }

            InstallSdk(deployStage2: false);

            VM.GetRemoteDirectory($@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}")
                .Should()
                .Exist();

            GetInstalledSdkVersion().Should().Be(SdkInstallerVersion);

            UninstallSdk();

            VM.GetRemoteDirectory($@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}")
                .Should()
                .NotExist();

            if (originalSdkVersion != null)
            {
                GetInstalledSdkVersion().Should().Be(originalSdkVersion);
            }
            else
            {
                //  TODO: This doesn't work if we've installed additional runtimes to support the SDK
                VM.GetRemoteDirectory($@"c:\Program Files\dotnet")
                    .Should()
                    .NotExist();
            }
        }


        [Fact]
        public void WorkloadInstallationAndGarbageCollection()
        {
            InstallSdk();

            var originalManifests = GetRollback();

            InstallWorkload("wasm-tools", skipManifestUpdate: true);

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

        //  Fixed by https://github.com/dotnet/installer/pull/18266
        [Fact]
        public void InstallStateShouldBeRemovedOnSdkUninstall()
        {
            InstallSdk();
            InstallWorkload("wasm-tools", skipManifestUpdate: true);
            ApplyRC1Manifests();
            var featureBand = new SdkFeatureBand(SdkInstallerVersion);
            var installStatePath = $@"c:\ProgramData\dotnet\workloads\x64\{featureBand}\InstallState\default.json";
            VM.GetRemoteFile(installStatePath).Should().Exist();
            UninstallSdk();
            VM.GetRemoteFile(installStatePath).Should().NotExist();
        }

        [Fact]
        public void UpdateWithRollback()
        {
            InstallSdk();
            InstallWorkload("wasm-tools", skipManifestUpdate: true);
            ApplyRC1Manifests();

            TestWasmWorkload();

            //  Second time applying same rollback file shouldn't do anything
            ApplyRC1Manifests()
                .Should()
                .NotHaveStdOutContaining("Installing");
        }

        [Fact]
        public void InstallWithRollback()
        {
            InstallSdk();

            VM.WriteFile($@"c:\SdkTesting\rollback-rc1.json", RollbackRC1)
                .Execute().Should().Pass();

            VM.CreateRunCommand("dotnet", "workload", "install", "wasm-tools", "--from-rollback-file", $@"c:\SdkTesting\rollback-rc1.json")
                .Execute().Should().Pass();

            TestWasmWorkload();
        }

        [Fact]
        public void InstallShouldNotUpdatePinnedRollback()
        {
            InstallSdk();
            ApplyRC1Manifests();
            var workloadVersion = GetWorkloadVersion();
            
            InstallWorkload("aspire", skipManifestUpdate: false);

            GetWorkloadVersion().Should().Be(workloadVersion);
        }

        [Fact]
        public void UpdateShouldUndoPinnedRollback()
        {
            InstallSdk();
            ApplyRC1Manifests();
            var workloadVersion = GetWorkloadVersion();

            VM.CreateRunCommand("dotnet", "workload", "update")
                .Execute()
                .Should().Pass();

            GetWorkloadVersion().Should().NotBe(workloadVersion);

        }

        [Fact]
        public void ShouldNotShowRebootMessage()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void ApplyRollbackShouldNotUpdateAdvertisingManifests()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void TestAspire()
        {
            InstallSdk();

            //AddNuGetSource("https://pkgs.dev.azure.com/dnceng/internal/_packaging/8.0.300-rtm.24224.15-shipping/nuget/v3/index.json");
            //AddNuGetSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/darc-pub-dotnet-aspire-d215c528/nuget/v3/index.json");

            //VM.CreateRunCommand("powershell", "-Command", "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }")
            //    .Execute().Should().Pass();

            InstallWorkload("aspire", skipManifestUpdate: true);

            VM.CreateRunCommand("dotnet", "new", "aspire-starter", "-o", "Aspire-StarterApp01")
                .WithWorkingDirectory(@"c:\SdkTesting")
                .Execute()
                .Should()
                .Pass();
        }


        void TestWasmWorkload()
        {
            var snapshot = VM.CreateSnapshot();

            VM.CreateRunCommand("dotnet", "new", "blazorwasm", "-o", "BlazorWasm")
                .WithWorkingDirectory(@"c:\SdkTesting")
                .Execute()
                .Should()
                .Pass();

            var result = VM.CreateRunCommand("dotnet", "publish", "/p:RunAotCompilation=true")
                .WithWorkingDirectory(@"c:\SdkTesting\BlazorWasm")
                .Execute();

            result.Should().Pass();

            //  When publishing a blazorwasm project without the wasm-tools workload installed, the following message is displayed:
            //  Publishing without optimizations. Although it's optional for Blazor, we strongly recommend using `wasm-tools` workload! You can install it by running `dotnet workload install wasm-tools` from the command line.
            //  (Setting RunAotCompilation=true explicitly causes a build failure if the workload isn't installed)
            result.Should().NotHaveStdOutContaining("Publishing without optimizations");

            snapshot.Apply();
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

        string ListWorkloads()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "list", "--machine-readable")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;            
        }
    }
}
