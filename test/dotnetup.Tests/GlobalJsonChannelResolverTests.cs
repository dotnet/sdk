// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class GlobalJsonChannelResolverTests
{
    [TestMethod]
    [DataRow("10.0.100", null, "10.0.1xx")]
    [DataRow("10.0.105", null, "10.0.1xx")]
    [DataRow("10.0.199", null, "10.0.1xx")]
    [DataRow("10.0.200", null, "10.0.2xx")]
    [DataRow("9.0.304", null, "9.0.3xx")]
    [DataRow("10.0.0", null, "10.0.0xx")]
    [DataRow("10.0.99", null, "10.0.0xx")]
    [DataRow("10.0.100", "latestPatch", "10.0.1xx")]
    public void DeriveChannel_FeatureBandBoundaries(string version, string? rollForward, string expectedChannel)
    {
        var result = GlobalJsonChannelResolver.DeriveChannel(version, rollForward);
        result.Should().Be(expectedChannel);
    }

    [TestMethod]
    [DataRow("10.0.100", "disable", "10.0.100")]
    [DataRow("10.0.100", "patch", "10.0.100")]
    [DataRow("10.0.100", "feature", "10.0.100")]
    [DataRow("10.0.100", "minor", "10.0.100")]
    [DataRow("10.0.100", "major", "10.0.100")]
    public void DeriveChannel_PinToExactVersion(string version, string rollForward, string expectedChannel)
    {
        var result = GlobalJsonChannelResolver.DeriveChannel(version, rollForward);
        result.Should().Be(expectedChannel);
    }

    [TestMethod]
    [DataRow("10.0.100", "latestFeature", "10.0")]
    [DataRow("9.0.304", "latestFeature", "9.0")]
    public void DeriveChannel_LatestFeature_MajorMinor(string version, string rollForward, string expectedChannel)
    {
        var result = GlobalJsonChannelResolver.DeriveChannel(version, rollForward);
        result.Should().Be(expectedChannel);
    }

    [TestMethod]
    [DataRow("10.0.100", "latestMinor", "10")]
    [DataRow("9.0.304", "latestMinor", "9")]
    public void DeriveChannel_LatestMinor_MajorOnly(string version, string rollForward, string expectedChannel)
    {
        var result = GlobalJsonChannelResolver.DeriveChannel(version, rollForward);
        result.Should().Be(expectedChannel);
    }

    [TestMethod]
    [DataRow("10.0.100", "latestMajor", "latest")]
    [DataRow("9.0.304", "latestMajor", "latest")]
    public void DeriveChannel_LatestMajor_Latest(string version, string rollForward, string expectedChannel)
    {
        var result = GlobalJsonChannelResolver.DeriveChannel(version, rollForward);
        result.Should().Be(expectedChannel);
    }

    [TestMethod]
    public void DeriveChannel_InvalidVersion_ReturnsNull()
    {
        var result = GlobalJsonChannelResolver.DeriveChannel("not-a-version", null);
        result.Should().BeNull();
    }
}
