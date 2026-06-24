// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    }
}
