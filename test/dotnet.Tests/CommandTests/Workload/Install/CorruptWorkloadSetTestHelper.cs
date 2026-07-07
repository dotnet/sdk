// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ManifestReaderTests;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    /// <summary>
    /// Test helper for setting up corrupt workload set scenarios.
    /// </summary>
    internal static class CorruptWorkloadSetTestHelper
    {
        /// <summary>
        /// Sets up a corrupt workload set scenario where manifests are missing but workload set is configured.
        /// This simulates package managers deleting manifests during SDK updates.
        /// Returns a real SdkDirectoryWorkloadManifestProvider so the corruption repairer can be attached.
        /// </summary>
        public static (string dotnetRoot, string userProfileDir, MockPackWorkloadInstaller mockInstaller, IWorkloadResolver workloadResolver, SdkDirectoryWorkloadManifestProvider manifestProvider)
            SetupCorruptWorkloadSet(
                TestAssetsManager testAssetsManager,
                bool userLocal,
                out string sdkFeatureVersion)
        {
            var testDirectory = testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            sdkFeatureVersion = "6.0.100";
            var workloadSetVersion = "6.0.100";

            // Create workload set contents JSON for the current (corrupt) version
            var workloadSetJson = """
{
  "xamarin-android-build": "8.4.7/6.0.100",
  "xamarin-ios-sdk": "10.0.1/6.0.100"
}
""";

            // Create workload set contents for the updated version
            var workloadSetJsonUpdated = """
{
  "xamarin-android-build": "8.4.8/6.0.100",
  "xamarin-ios-sdk": "10.0.2/6.0.100"
}
""";

            // Create workload set contents for the mock installer
            var workloadSetContents = new Dictionary<string, string>
            {
                [workloadSetVersion] = workloadSetJson,
                ["6.0.101"] = workloadSetJsonUpdated
            };

            // Set up mock installer with workload set support
            // Note: Don't pre-populate installedWorkloads - the test focuses on manifest repair, not workload installation
            var mockInstaller = new MockPackWorkloadInstaller(
                dotnetDir: dotnetRoot,
                installedWorkloads: new List<WorkloadId>(),
                workloadSetContents: workloadSetContents);

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            // Create install state with workload set version
            var installStateDir = Path.Combine(installRoot, "metadata", "workloads", RuntimeInformation.ProcessArchitecture.ToString(), sdkFeatureVersion, "InstallState");
            Directory.CreateDirectory(installStateDir);
            var installStatePath = Path.Combine(installStateDir, "default.json");
            var installState = new InstallStateContents
            {
                UseWorkloadSets = true,
                WorkloadVersion = workloadSetVersion,
                Manifests = new Dictionary<string, string>
                {
                    ["xamarin-android-build"] = "8.4.7",
                    ["xamarin-ios-sdk"] = "10.0.1"
                }
            };
            File.WriteAllText(installStatePath, installState.ToString());

            // Create workload set folder so the real provider can find it
            var workloadSetsRoot = Path.Combine(dotnetRoot, "sdk-manifests", sdkFeatureVersion, "workloadsets", workloadSetVersion);
            Directory.CreateDirectory(workloadSetsRoot);
            File.WriteAllText(Path.Combine(workloadSetsRoot, "workloadset.workloadset.json"), workloadSetJson);

            // Create mock manifest directories but WITHOUT manifest files to simulate ruined install
            var manifestRoot = Path.Combine(dotnetRoot, "sdk-manifests", sdkFeatureVersion);
            var androidManifestDir = Path.Combine(manifestRoot, "xamarin-android-build", "8.4.7");
            var iosManifestDir = Path.Combine(manifestRoot, "xamarin-ios-sdk", "10.0.1");
            Directory.CreateDirectory(androidManifestDir);
            Directory.CreateDirectory(iosManifestDir);

            // Verify manifests don't exist (simulating the ruined install)
            if (File.Exists(Path.Combine(androidManifestDir, "WorkloadManifest.json")) ||
                File.Exists(Path.Combine(iosManifestDir, "WorkloadManifest.json")))
            {
                throw new InvalidOperationException("Test setup failed: manifest files should not exist");
            }

            // Create a real SdkDirectoryWorkloadManifestProvider
            var manifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetRoot, sdkFeatureVersion, userProfileDir, globalJsonPath: null);
            var workloadResolver = WorkloadResolver.Create(manifestProvider, dotnetRoot, sdkFeatureVersion, userProfileDir);
            mockInstaller.WorkloadResolver = workloadResolver;

            return (dotnetRoot, userProfileDir, mockInstaller, workloadResolver, manifestProvider);
        }
    }
}
