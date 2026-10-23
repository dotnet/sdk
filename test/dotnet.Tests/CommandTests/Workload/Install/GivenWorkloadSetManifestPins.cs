// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Update;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
public class GivenWorkloadSetManifestPins : SdkTest
{
    private const string SdkVersion = "8.0.300";
    private const string WorkloadSetVersion = "8.0.302";
    private const string WorkloadSetJson = """
        {
          "sample": "2.0.0/8.0.200"
        }
        """;

    [TestMethod]
    [CombinatorialData]
    public void ExplicitWorkloadSetSupersedesOnlyPinsInTheSet(
        // Exercise install when true, or update when false; both explicitly select the target set.
        bool install,
        // Reselect the already-selected target set when true; otherwise start in loose-manifest mode with no selected set.
        bool sameWorkloadSet,
        // Add an older pin for a manifest in the target set, which explicit selection must remove.
        bool pinSetManifest,
        // Add an older pin for a manifest outside the target set, which explicit selection must preserve.
        bool pinExternalManifest)
    {
        var (dotnetRoot, userProfileDir, installer, resolver, updater, statePath) =
            SetUp(sameWorkloadSet, pinSetManifest, pinExternalManifest,
                identifier: $"{install}-{sameWorkloadSet}-{pinSetManifest}-{pinExternalManifest}");

        Execute(install, dotnetRoot, userProfileDir, installer, resolver, updater).Should().Be(0);

        var state = InstallStateContents.FromPath(statePath);
        state.UseWorkloadSets.Should().BeTrue();
        state.WorkloadVersion.Should().Be(WorkloadSetVersion);
        state.Manifests?.ContainsKey("SAMPLE").Should().NotBe(true);
        if (pinExternalManifest)
        {
            state.Manifests.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["external"] = "1.0.0/8.0.100"
            });
        }
        else
        {
            state.Manifests.Should().BeNull();
        }

        resolver.GetInstalledManifests().Single(m => m.Id == "sample").Version.Should().Be("2.0.0");
        resolver.GetInstalledManifests().Single(m => m.Id == "external").Version
            .Should().Be(pinExternalManifest ? "1.0.0" : "2.0.0");
        installer.InstalledPacks.Single().Version.Should().Be("2.0.0",
            because: "pack selection must use the set after the overlapping pin is removed");
    }

    [TestMethod]
    [CombinatorialData]
    public void FailedExplicitWorkloadSetRestoresManifestPins(bool install, bool sameWorkloadSet, bool pinExternalManifest)
    {
        var (dotnetRoot, userProfileDir, installer, resolver, updater, statePath) =
            SetUp(sameWorkloadSet, pinSetManifest: true, pinExternalManifest, failPackInstallation: true,
                identifier: $"{install}-{sameWorkloadSet}-{pinExternalManifest}");
        var originalState = File.ReadAllText(statePath);

        var exception = Assert.ThrowsExactly<GracefulException>(() =>
            Execute(install, dotnetRoot, userProfileDir, installer, resolver, updater));

        exception.Message.Should().Contain("Failing pack: sample.pack");
        File.ReadAllText(statePath).Should().Be(originalState);
        resolver.GetInstalledManifests().Single(m => m.Id == "sample").Version.Should().Be("1.0.0");
        resolver.GetInstalledManifests().Single(m => m.Id == "external").Version
            .Should().Be(pinExternalManifest ? "1.0.0" : "2.0.0");
        installer.RolledBackPacks.Single().Version.Should().Be("2.0.0");
    }

    [TestMethod]
    public void InstallWithoutExplicitWorkloadSetPreservesManifestPins()
    {
        var (dotnetRoot, userProfileDir, installer, resolver, updater, statePath) =
            SetUp(sameWorkloadSet: true, pinSetManifest: true, pinExternalManifest: true);
        var originalState = File.ReadAllText(statePath);

        Execute(true, dotnetRoot, userProfileDir, installer, resolver, updater, explicitVersion: false)
            .Should().Be(0);

        File.ReadAllText(statePath).Should().Be(originalState);
        installer.InstallWorkloadSetCalled.Should().BeFalse();
        installer.InstalledPacks.Single().Version.Should().Be("1.0.0");
    }

    private int Execute(
        bool install,
        string dotnetRoot,
        string userProfileDir,
        MockPackWorkloadInstaller installer,
        IWorkloadResolver resolver,
        MockWorkloadManifestUpdater updater,
        bool explicitVersion = true)
    {
        var factory = new MockWorkloadResolverFactory(dotnetRoot, SdkVersion, resolver, userProfileDir);
        string[] arguments = install ? ["dotnet", "workload", "install", "sample-workload"] : ["dotnet", "workload", "update"];
        if (explicitVersion)
        {
            arguments = [.. arguments, "--version", WorkloadSetVersion];
        }

        var parseResult = Parser.Parse(arguments);
        var reporter = new BufferedReporter();
        var downloader = new MockNuGetPackageDownloader(dotnetRoot);
        return install
            ? new WorkloadInstallCommand(parseResult, reporter, factory, installer, downloader, updater).Execute()
            : new WorkloadUpdateCommand(parseResult, reporter, factory, installer, downloader, updater).Execute();
    }

    private (string DotnetRoot, string UserProfileDir, MockPackWorkloadInstaller Installer,
        IWorkloadResolver Resolver, MockWorkloadManifestUpdater Updater, string StatePath) SetUp(
        bool sameWorkloadSet,
        bool pinSetManifest,
        bool pinExternalManifest,
        bool failPackInstallation = false,
        string? identifier = null,
        [CallerMemberName] string testName = "")
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory(testName: testName, identifier: identifier).Path;
        var dotnetRoot = Path.Combine(testDirectory, "dotnet");
        var userProfileDir = Path.Combine(testDirectory, "user-profile");
        var featureBand = new SdkFeatureBand(SdkVersion);
        var statePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(featureBand, dotnetRoot), "default.json");
        var state = new InstallStateContents
        {
            UseWorkloadSets = sameWorkloadSet,
            WorkloadVersion = sameWorkloadSet ? WorkloadSetVersion : null,
            Manifests = pinSetManifest || pinExternalManifest ? new Dictionary<string, string>() : null
        };
        if (pinSetManifest)
        {
            state.Manifests!["SAMPLE"] = "1.0.0/8.0.100";
        }
        if (pinExternalManifest)
        {
            state.Manifests!["external"] = "1.0.0/8.0.100";
        }
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, state.ToString());

        var workloadSetDirectory = Path.Combine(dotnetRoot, "sdk-manifests", SdkVersion, "workloadsets", WorkloadSetVersion);
        Directory.CreateDirectory(workloadSetDirectory);
        File.WriteAllText(Path.Combine(workloadSetDirectory, "test.workloadset.json"), WorkloadSetJson);
        WriteManifest(dotnetRoot, "8.0.100", "sample", "1.0.0");
        WriteManifest(dotnetRoot, "8.0.200", "sample", "2.0.0");
        WriteManifest(dotnetRoot, SdkVersion, "sample", "3.0.0");
        WriteManifest(dotnetRoot, "8.0.100", "external", "1.0.0");
        WriteManifest(dotnetRoot, SdkVersion, "external", "2.0.0");

        var provider = new SdkDirectoryWorkloadManifestProvider(dotnetRoot, SdkVersion, userProfileDir, globalJsonPath: null);
        var resolver = WorkloadResolver.Create(provider, dotnetRoot, SdkVersion, userProfileDir);
        var installer = new MockPackWorkloadInstaller(dotnetRoot,
            failingPack: failPackInstallation ? "sample.pack" : null,
            installedWorkloads: [new WorkloadId("sample-workload")],
            workloadSetContents: new Dictionary<string, string> { [WorkloadSetVersion] = WorkloadSetJson })
        {
            WorkloadResolver = resolver
        };
        var updater = new MockWorkloadManifestUpdater(
            manifestUpdates: [new ManifestUpdateWithWorkloads(
                new ManifestVersionUpdate(new ManifestId("sample"), new ManifestVersion("2.0.0"), "8.0.200"),
                new Dictionary<WorkloadId, WorkloadDefinition>())],
            fromWorkloadSet: true,
            workloadSetVersion: WorkloadSetVersion);

        return (dotnetRoot, userProfileDir, installer, resolver, updater, statePath);
    }

    private static void WriteManifest(string dotnetRoot, string featureBand, string id, string version)
    {
        var directory = Path.Combine(dotnetRoot, "sdk-manifests", featureBand, id, version);
        Directory.CreateDirectory(directory);
        string workloads = id == "sample" ? """
            "workloads": {
              "sample-workload": { "packs": ["sample.pack"] }
            },
            "packs": {
              "sample.pack": { "kind": "framework", "version": "VERSION" }
            }
            """.Replace("VERSION", version) : """
            "workloads": {},
            "packs": {}
            """;
        File.WriteAllText(Path.Combine(directory, "WorkloadManifest.json"), $$"""
            {
              "version": "{{version}}",
              {{workloads}}
            }
            """);
    }
}
