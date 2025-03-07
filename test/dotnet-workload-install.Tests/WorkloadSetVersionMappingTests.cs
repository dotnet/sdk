// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;
using Microsoft.TemplateEngine.Edge.Constraints;
using Microsoft.DotNet.Workloads.Workload;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class WorkloadSetVersionMappingTests : SdkTest
    {

        public WorkloadSetVersionMappingTests(ITestOutputHelper log) : base(log)
        {
        }

        public static IEnumerable<object[]> WorkloadVersionsData
        {
            get
            {
                return
                [
                    //  Workload set version, feature band, package version
                    ["8.0.200", "8.0.200", "8.200.0"],
                    ["8.0.201", "8.0.200", "8.201.0"],
                    ["8.0.203.1", "8.0.200", "8.203.1"],
                    ["9.0.100-preview.2.3.4.5.6.7.8", "9.0.100-preview.2", "9.100.0-preview.2.3.4.5.6.7.8"],
                    ["8.0.201.1-preview", "8.0.200", "8.201.1-preview"],
                    ["8.0.201.1-preview.2", "8.0.200", "8.201.1-preview.2"],

                    //  Versions with "rtm" in them are special-cased to not use a preview feature band
                    ["9.0.100-rtm.23015", "9.0.100", "9.100.0-rtm.23015"],
                    ["9.0.100-testingrtms.23015", "9.0.100", "9.100.0-testingrtms.23015"],

                    //  This apparently works accidentally, since "servicing" contains "ci", which is what the SdkFeatureBand constructor check to see if it should ignore the prerelease specifier
                    ["8.0.201-servicing.23015", "8.0.200", "8.201.0-servicing.23015"],
                    ["9.0.100-preview-servicing.1.23015", "9.0.100", "9.100.0-preview-servicing.1.23015"],

                ];
            }
        }

        [Theory]
        [MemberData(nameof(WorkloadVersionsData))]
        public void TestWorkloadSetVersionParsing(string workloadSetVersion, string expectedFeatureBand, string expectedPackageVersion)
        {
            string packageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(workloadSetVersion, out SdkFeatureBand featureBand);

            packageVersion.Should().Be(expectedPackageVersion);
            featureBand.Should().Be(new SdkFeatureBand(expectedFeatureBand));
        }

        [Theory]
        [MemberData(nameof(WorkloadVersionsData))]
        public void TestWorkloadSetPackageVersionParsing(string expectedWorkloadSetVersion, string packageFeatureBand, string packageVersion)
        {
            string workloadSetVersion = WorkloadSetVersion.FromWorkloadSetPackageVersion(new SdkFeatureBand(packageFeatureBand), packageVersion);

            workloadSetVersion.Should().Be(expectedWorkloadSetVersion);
        }
    }
}
