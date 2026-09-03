// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.NET.TestFramework;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace ManifestReaderTests
{

    [TestClass]

    public class SdkFeatureBandTests : SdkTest
    {

        [TestMethod]
        [DataRow("6.0.100", "6.0.100")]
        [DataRow("10.0.512", "10.0.500")]
        [DataRow("7.0.100-preview.1.12345", "7.0.100-preview.1")]
        [DataRow("7.0.100-dev", "7.0.100")]
        [DataRow("7.0.100-ci", "7.0.100")]
        [DataRow("6.0.100-rc.2.21505.57", "6.0.100-rc.2")]
        [DataRow("7.0.100-alpha.1.21558.2", "7.0.100-alpha.1")]
        public void ItParsesVersionsCorrectly(string version, string expectedParsedVersion)
        {
            var parsedVersion = new SdkFeatureBand(version).ToString();
            parsedVersion.Should().Be(expectedParsedVersion);
        }

        [TestMethod]
        [DataRow("6.0.100", "6.0.100")]
        [DataRow("10.0.512", "10.0.500")]
        [DataRow("7.0.105-preview.1.12345", "7.0.100")]
        [DataRow("7.0.100-dev", "7.0.100")]
        [DataRow("7.0.100-ci", "7.0.100")]
        [DataRow("6.0.100-rc.2.21505.57", "6.0.100")]
        [DataRow("7.0.400-alpha.1.21558.2", "7.0.400")]
        public void ItDiscardsPreleaseLabelsCorrectly(string version, string expectedParsedVersion)
        {
            var parsedVersion = new SdkFeatureBand(version).ToStringWithoutPrerelease();
            parsedVersion.Should().Be(expectedParsedVersion);
        }

        [TestMethod]
        //  Wrong (package version) format: detected=true, suggested corrected workload set version
        [DataRow("10.105.0",                     true,  "10.0.105")]
        [DataRow("11.100.0-preview.5.26309.3",   true,  "11.0.100-preview.5.26309.3")]
        [DataRow("8.200.0",                      true,  "8.0.200")]
        [DataRow("8.201.0",                      true,  "8.0.201")]
        [DataRow("8.203.1",                      true,  "8.0.203.1")]
        [DataRow("9.100.0-preview.2.3.4.5.6.7.8", true, "9.0.100-preview.2.3.4.5.6.7.8")]
        [DataRow("8.201.1-preview",              true,  "8.0.201.1-preview")]
        [DataRow("8.201.1-preview.2",            true,  "8.0.201.1-preview.2")]
        //  Correct (workload set) format: detected=false
        [DataRow("10.0.105",                     false, null)]
        [DataRow("11.0.100-preview.5.26309.3",   false, null)]
        [DataRow("8.0.200",                      false, null)]
        [DataRow("8.0.203.1",                    false, null)]
        [DataRow("9.0.100-preview.2",            false, null)]
        //  Not a version: detected=false
        [DataRow("not-a-version",                false, null)]
        [DataRow("",                             false, null)]
        public void ItDetectsPackageVersionFormat(string version, bool expectedIsDetected, string? expectedSuggestion)
        {
            bool isDetected = WorkloadSetVersion.IsWorkloadSetVersionInPackageVersionFormat(version, out var suggestedVersion);

            isDetected.Should().Be(expectedIsDetected);
            suggestedVersion.Should().Be(expectedSuggestion);
        }
    }
}
