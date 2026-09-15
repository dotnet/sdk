// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
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
}
