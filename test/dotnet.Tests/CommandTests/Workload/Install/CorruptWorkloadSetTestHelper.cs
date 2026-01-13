// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using ManifestReaderTests;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
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
        /// </summary>
        public static (string dotnetRoot, string userProfileDir, MockPackWorkloadInstaller mockInstaller, IWorkloadResolver workloadResolver)
            SetupCorruptWorkloadSet(
                TestAssetsManager testAssetsManager,
                string manifestPath,
                bool userLocal,
                out string sdkFeatureVersion)
        {
            var testDirectory = testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            sdkFeatureVersion = "6.0.100";
            var workloadSetVersion = "6.0.100";

            // Create workload set contents
            var workloadSetContents = new Dictionary<string, string>
            {
                [workloadSetVersion] = """
{
  "xamarin-android-build": "8.4.7/6.0.100",
  "xamarin-ios-sdk": "10.0.1/6.0.100"
}
"""
            };

            // Set up mock installer with workload set support
            var mockInstaller = new MockPackWorkloadInstaller(
                dotnetDir: dotnetRoot,
                installedWorkloads: new List<WorkloadId> { new WorkloadId("xamarin-android") },
                workloadSetContents: workloadSetContents);

            // Create the manifest provider and resolver
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { manifestPath }), dotnetRoot, userLocal, userProfileDir);
            mockInstaller.WorkloadResolver = workloadResolver;

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
                WorkloadVersion = workloadSetVersion
            };
            File.WriteAllText(installStatePath, installState.ToString());

            // Create mock manifest directories but delete the manifest files to simulate ruined install
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

            return (dotnetRoot, userProfileDir, mockInstaller, workloadResolver);
        }
    }
}
