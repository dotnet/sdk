// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
[SupportedOSPlatform("windows")]
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
    public async Task ExtractManifestAsyncVerifiesTheResolvedMsiPathBeforeAdminInstall()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string? expectedMsiPath = null;
        var downloader = new Mock<INuGetPackageDownloader>();
        downloader.Setup(d => d.ExtractPackageAsync(It.IsAny<string>(), It.IsAny<DirectoryPath>()))
            .Returns((string _, DirectoryPath targetFolder) =>
            {
                string dataPath = Path.Combine(targetFolder.Value, "data");
                Directory.CreateDirectory(dataPath);
                expectedMsiPath = Path.Combine(dataPath, "payload.msi");
                File.WriteAllText(Path.Combine(dataPath, "msi.json"), """{"Payload":"payload.msi"}""");
                File.WriteAllText(expectedMsiPath, string.Empty);
                return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
            });

        string? verifiedMsiPath = null;
        var expectedException = new VerificationCallbackInvokedException();
        var installer = new WindowsMsiManifestInstaller(
            downloader.Object,
            verifyPackageSignature: msiPath =>
            {
                verifiedMsiPath = msiPath;
                throw expectedException;
            });

        var exception = await Assert.ThrowsExactlyAsync<VerificationCallbackInvokedException>(
            () => installer.ExtractManifestAsync("manifest.nupkg", Path.Combine(temporaryDirectory.DirectoryPath, "target")));

        Assert.AreSame(expectedException, exception);
        Assert.AreEqual(expectedMsiPath, verifiedMsiPath);
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

    private sealed class VerificationCallbackInvokedException : Exception
    {
    }
}
