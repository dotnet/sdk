// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class GivenAWindowsMsiManifestInstaller : SdkTest
{
    [TestMethod]
    public void GetManifestPackageIdReturnsTheArchitectureQualifiedWorkloadSetPackageId()
    {
        var installer = new WindowsMsiManifestInstaller(new MockNuGetPackageDownloader());
        var featureBand = new SdkFeatureBand("6.0.100");

        var packageId = installer.GetManifestPackageId(
            new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId),
            featureBand);

        packageId.ToString().Should().Be(
            $"{WorkloadManifestUpdater.WorkloadSetManifestId}.{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture}"
                .ToLowerInvariant());
    }

    [TestMethod]
    public void GetManifestPackageIdReturnsTheArchitectureQualifiedManifestPackageId()
    {
        var installer = new WindowsMsiManifestInstaller(new MockNuGetPackageDownloader());
        var featureBand = new SdkFeatureBand("6.0.300");
        var manifestId = new ManifestId("test.manifest");

        var packageId = installer.GetManifestPackageId(manifestId, featureBand);

        packageId.ToString().Should().Be(
            $"{manifestId}.Manifest-{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture}".ToLowerInvariant());
    }

    [TestMethod]
    public async Task ExtractManifestVerifiesMsiBeforeAdminInstall()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var downloader = new MsiPackageDownloader();
        string? verifiedMsiPath = null;
        var installer = new WindowsMsiManifestInstaller(
            downloader,
            verifyPackageSignature: msiPath =>
            {
                verifiedMsiPath = msiPath;
                throw new InvalidOperationException("Package signature verification failed.");
            });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => installer.ExtractManifestAsync("fake.nupkg", Path.Combine(temporaryDirectory.DirectoryPath, "manifest")));

        exception.Message.Should().Be("Package signature verification failed.");
        verifiedMsiPath.Should().Be(downloader.MsiPath);
    }

    // MSIs built with WiX v3 collapse the Program Files directory into the administrative install target.
    [TestMethod]
    public void FindExtractedManifestFolderLocatesTheManifestInTheWiXV3AdminInstallLayout()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var testDirectory = temporaryDirectory.DirectoryPath;
        var expected = Path.Combine(testDirectory, "dotnet", "sdk-manifests", "6.0.100", "test.manifest");
        Directory.CreateDirectory(expected);

        WindowsMsiManifestInstaller.FindExtractedManifestFolder(testDirectory).Should().Be(expected);
    }

    // MSIs built with WiX v4 and newer emit a named directory for Program Files in the administrative image.
    [TestMethod]
    public void FindExtractedManifestFolderLocatesTheManifestInTheWiXV4AdminInstallLayout()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var testDirectory = temporaryDirectory.DirectoryPath;
        var expected = Path.Combine(testDirectory, "PFiles64", "dotnet", "sdk-manifests", "6.0.100", "workloadsets");
        Directory.CreateDirectory(expected);

        WindowsMsiManifestInstaller.FindExtractedManifestFolder(testDirectory).Should().Be(expected);
    }

    [TestMethod]
    public void FindExtractedManifestFolderReturnsNullWhenThereIsNoManifest()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var testDirectory = temporaryDirectory.DirectoryPath;
        Directory.CreateDirectory(Path.Combine(testDirectory, "PFiles64", "dotnet"));

        WindowsMsiManifestInstaller.FindExtractedManifestFolder(testDirectory).Should().BeNull();
        WindowsMsiManifestInstaller.FindExtractedManifestFolder(Path.Combine(testDirectory, "does-not-exist")).Should().BeNull();
    }

    [TestMethod]
    public void FindExtractedManifestFolderDoesNotSearchBeyondTheKnownLayouts()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var testDirectory = temporaryDirectory.DirectoryPath;
        Directory.CreateDirectory(Path.Combine(testDirectory, "unexpected", "PFiles64", "dotnet", "sdk-manifests", "6.0.100", "test.manifest"));

        WindowsMsiManifestInstaller.FindExtractedManifestFolder(testDirectory).Should().BeNull();
    }

#nullable disable
    private sealed class MsiPackageDownloader : INuGetPackageDownloader
    {
        public string MsiPath { get; private set; } = string.Empty;

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            string dataPath = Path.Combine(targetFolder.Value, "data");
            Directory.CreateDirectory(dataPath);
            MsiPath = Path.Combine(dataPath, "test.msi");
            File.WriteAllText(MsiPath, string.Empty);
            File.WriteAllText(Path.Combine(dataPath, "msi.json"), """{"Payload":"test.msi"}""");
            return Task.FromResult(Enumerable.Empty<string>());
        }

        public Task<string> DownloadPackageAsync(
            PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping packageSourceMapping = null) => throw new NotImplementedException();

        public Task<string> GetPackageUrl(
            PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false) => throw new NotImplementedException();

        public Task<NuGetVersion> GetLatestPackageVersion(
            PackageId packageId,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false) => throw new NotImplementedException();

        public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(
            PackageId packageId,
            int numberOfResults,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false) => throw new NotImplementedException();

        public Task<NuGetVersion> GetBestPackageVersionAsync(
            PackageId packageId,
            VersionRange versionRange,
            PackageSourceLocation packageSourceLocation = null) => throw new NotImplementedException();

        public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(
            PackageId packageId,
            VersionRange versionRange,
            PackageSourceLocation packageSourceLocation = null) => throw new NotImplementedException();
    }
#nullable restore
}
