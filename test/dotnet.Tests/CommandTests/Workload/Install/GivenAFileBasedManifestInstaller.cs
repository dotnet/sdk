// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    /// <summary>
    ///  Focused tests for <see cref="FileBasedManifestInstaller"/>, the narrow
    ///  <see cref="IWorkloadManifestInstaller"/> extracted out of <see cref="FileBasedInstaller"/> so it
    ///  can be shared by the full file-based installer and by lightweight consumers (e.g. the background
    ///  advertising-manifest updater) without pulling in the rest of the installer.
    /// </summary>
    [TestClass]
    public class GivenAFileBasedManifestInstaller : SdkTest
    {
        [TestMethod]
        public void GetManifestPackageIdReturnsTheWorkloadSetPackageIdForTheWorkloadSetManifestId()
        {
            var installer = new FileBasedManifestInstaller(new MockNuGetPackageDownloader(), new DirectoryPath(TestAssetsManager.CreateTestDirectory().Path));
            var featureBand = new SdkFeatureBand("6.0.100");

            var packageId = installer.GetManifestPackageId(new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId), featureBand);

            packageId.ToString().Should().Be($"{WorkloadManifestUpdater.WorkloadSetManifestId}.{featureBand}".ToLowerInvariant());
        }

        [TestMethod]
        public void GetManifestPackageIdReturnsTheManifestPackageIdForOtherManifestIds()
        {
            var installer = new FileBasedManifestInstaller(new MockNuGetPackageDownloader(), new DirectoryPath(TestAssetsManager.CreateTestDirectory().Path));
            var featureBand = new SdkFeatureBand("6.0.100");
            var manifestId = new ManifestId("test.manifest");

            var packageId = installer.GetManifestPackageId(manifestId, featureBand);

            packageId.ToString().Should().Be($"{manifestId}.Manifest-{featureBand}".ToLowerInvariant());
        }

        [TestMethod]
        public void GetManifestPackageIdMatchesTheFileBasedInstallerItWasExtractedFrom()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var manifestInstaller = new FileBasedManifestInstaller(new MockNuGetPackageDownloader(), new DirectoryPath(testDirectory));
            var fullInstaller = new FileBasedInstaller(
                new BufferedReporter(),
                new SdkFeatureBand("6.0.100"),
                workloadResolver: null,
                userProfileDir: testDirectory,
                nugetPackageDownloader: new MockNuGetPackageDownloader(),
                dotnetDir: dotnetRoot);

            foreach (var (manifestId, featureBand) in new[]
            {
                (new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId), new SdkFeatureBand("6.0.100")),
                (new ManifestId("test.manifest"), new SdkFeatureBand("6.0.300")),
            })
            {
                fullInstaller.GetManifestPackageId(manifestId, featureBand)
                    .Should().Be(manifestInstaller.GetManifestPackageId(manifestId, featureBand));
            }
        }

        [TestMethod]
        public async Task ExtractManifestAsyncMovesTheExtractedDataDirectoryToTheTargetPath()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
            var nugetDownloader = new MockNuGetPackageDownloader(testDirectory, manifestDownload: true);
            var installer = new FileBasedManifestInstaller(nugetDownloader, new DirectoryPath(testDirectory));
            var targetPath = Path.Combine(testDirectory, "target-manifest");

            await installer.ExtractManifestAsync(Path.Combine(testDirectory, "fake.nupkg"), targetPath);

            File.Exists(Path.Combine(targetPath, "WorkloadManifest.json")).Should().BeTrue();
        }
    }
}
