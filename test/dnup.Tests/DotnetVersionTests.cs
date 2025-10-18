// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

public class DotnetVersionTests
{
    [Theory]
    [InlineData("7.0.201", "7")]
    [InlineData("7.0.2xx", "7")]
    [InlineData("7.1.300", "7")]
    [InlineData("10.0.102", "10")]
    [InlineData("7", "7")]
    [InlineData("7.0", "7")]
    public void GetMajor(string version, string expected) =>
        new DotnetVersion(version).Major.ToString().Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", "0")]
    [InlineData("7.1.300", "1")]
    [InlineData("10.0.102", "0")]
    [InlineData("7", "0")]
    [InlineData("7.0", "0")]
    public void GetMinor(string version, string expected) =>
        new DotnetVersion(version).Minor.ToString().Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", "7.0")]
    [InlineData("7.0.2xx", "7.0")]
    [InlineData("7.1.300", "7.1")]
    [InlineData("10.0.102", "10.0")]
    [InlineData("7", "7.0")]
    [InlineData("7.0", "7.0")]
    public void GetMajorMinor(string version, string expected) =>
        new DotnetVersion(version).MajorMinor.Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", "2")]
    [InlineData("7.0.2xx", "2")]
    [InlineData("7.1.300", "3")]
    [InlineData("10.0.102", "1")]
    [InlineData("7.0.221", "2")]
    [InlineData("7.0.7", null)]
    [InlineData("8.0", null)]
    public void GetFeatureBand(string version, string? expected) =>
        DotnetVersion.FromSdk(version).GetFeatureBand().Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", "01")]
    [InlineData("7.1.300", "00")]
    [InlineData("10.0.102", "02")]
    [InlineData("7.0.221", "21")]
    [InlineData("8.0.400-preview.0.24324.5", "00")]
    [InlineData("7.0.7", null)]
    [InlineData("8.0", null)]
    public void GetFeatureBandPatch(string version, string? expected) =>
        DotnetVersion.FromSdk(version).GetFeatureBandPatch().Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", "201")]
    [InlineData("7.1.300", "300")]
    [InlineData("10.0.102", "102")]
    [InlineData("7.0.221", "221")]
    [InlineData("7.0.7", null)]
    [InlineData("8.0", null)]
    public void GetCompleteBandAndPatch(string version, string? expected) =>
        DotnetVersion.FromSdk(version).GetCompleteBandAndPatch().Should().Be(expected);

    [Theory]
    [InlineData("7.0", null)]
    [InlineData("8.0.10", "10")]
    [InlineData("8.0.9-rc.2.24502.A", "9")]
    public void GetRuntimePatch(string version, string? expected)
    {
        var v = DotnetVersion.FromRuntime(version);
        var patch = v.Patch == 0 ? null : v.Patch.ToString();
        patch.Should().Be(expected);
    }

    [Theory]
    [InlineData("8.0.400-preview.0.24324.5", true)]
    [InlineData("9.0.0-rc.2", true)]
    [InlineData("9.0.0-rc.2.24473.5", true)]
    [InlineData("8.0.0-preview.7", true)]
    [InlineData("10.0.0-alpha.2.24522.8", true)]
    [InlineData("7.0.2xx", false)]
    [InlineData("7.0", false)]
    [InlineData("7.1.10", false)]
    [InlineData("7.0.201", false)]
    [InlineData("10.0.100-rc.2.25420.109", true)]
    public void IsPreview(string version, bool expected) =>
        new DotnetVersion(version).IsPreview.Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", false)]
    [InlineData("7.0.2xx", true)]
    [InlineData("10.0.102", false)]
    public void IsNonSpecificFeatureBand(string version, bool expected) =>
        new DotnetVersion(version).IsNonSpecificFeatureBand.Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", true)]
    [InlineData("7.1.300", true)]
    [InlineData("10.0.102", true)]
    [InlineData("7", false)]
    [InlineData("7.0.2xx", false)]
    [InlineData("7.0", false)]
    public void IsFullySpecified(string version, bool expected) =>
        new DotnetVersion(version).IsFullySpecified.Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", false)]
    [InlineData("7.1.300", false)]
    [InlineData("10.0.102", false)]
    [InlineData("7", true)]
    [InlineData("7.0.2xx", false)]
    [InlineData("7.0", true)]
    public void IsNonSpecificMajorMinor(string version, bool expected) =>
        new DotnetVersion(version).IsNonSpecificMajorMinor.Should().Be(expected);

    [Theory]
    [InlineData("7.0.201", true)]
    [InlineData("7.1.300", true)]
    [InlineData("10.0.102", true)]
    [InlineData("7.0.2xx", true)]
    [InlineData("7", true)]
    [InlineData("7.0", true)]
    [InlineData("7.0.1999", false)]
    [InlineData("7.1.10", false)]
    [InlineData("10.10", false)]
    public void IsValidFormat(string version, bool expected) =>
        DotnetVersion.IsValidFormat(version).Should().Be(expected);

    [Theory]
    [InlineData("8.0.301", 0, true, false)]  // Auto
    [InlineData("8.0.7", 0, false, true)]    // Auto
    [InlineData("8.0.301", 1, true, false)]  // Sdk
    [InlineData("8.0.7", 2, false, true)]    // Runtime
    [InlineData("8.0.7", 1, true, false)]    // Sdk
    public void VersionTypeDetection(string version, int typeInt, bool isSdk, bool isRuntime)
    {
        var type = (DotnetVersionType)typeInt;
        var v = new DotnetVersion(version, type);
        v.IsSdkVersion.Should().Be(isSdk);
        v.IsRuntimeVersion.Should().Be(isRuntime);
    }

    [Theory]
    [InlineData("8.0.301+abc123def456", "abc123def456")]
    [InlineData("8.0.301-preview.1.abc123", "abc123")]
    [InlineData("8.0.301-abc123def", "abc123def")]
    [InlineData("8.0.301", null)]
    [InlineData("8.0.301-preview.1", null)]
    public void GetBuildHash(string version, string? expected) =>
        new DotnetVersion(version).GetBuildHash().Should().Be(expected);

    [Theory]
    [InlineData("8.0.301+abc123def456", "8.0.301")]
    [InlineData("8.0.301-preview.1.abc123", "8.0.301-preview.1")]
    [InlineData("8.0.301", "8.0.301")]
    public void GetVersionWithoutBuildHash(string version, string expected) =>
        new DotnetVersion(version).GetVersionWithoutBuildHash().Should().Be(expected);

    [Theory]
    [InlineData("8.0.301", "8.0.302", -1)]
    [InlineData("8.0.302", "8.0.301", 1)]
    [InlineData("8.0.301", "8.0.301", 0)]
    public void Comparison(string v1, string v2, int expected)
    {
        var result = new DotnetVersion(v1).CompareTo(new DotnetVersion(v2));
        if (expected < 0) result.Should().BeNegative();
        else if (expected > 0) result.Should().BePositive();
        else result.Should().Be(0);
    }

    [Fact]
    public void FactoryMethods()
    {
        var sdk = DotnetVersion.FromSdk("8.0.7");
        var runtime = DotnetVersion.FromRuntime("8.0.301");

        sdk.IsSdkVersion.Should().BeTrue();
        sdk.IsRuntimeVersion.Should().BeFalse();
        runtime.IsSdkVersion.Should().BeFalse();
        runtime.IsRuntimeVersion.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversions()
    {
        DotnetVersion version = "8.0.301";
        string versionString = version;

        version.Value.Should().Be("8.0.301");
        versionString.Should().Be("8.0.301");
    }

    [Fact]
    public void TryParse()
    {
        DotnetVersion.TryParse("8.0.301", out var valid).Should().BeTrue();
        valid.Value.Should().Be("8.0.301");

        DotnetVersion.TryParse("invalid", out _).Should().BeFalse();
    }

    [Fact]
    public void Parse() =>
        new Action(() => DotnetVersion.Parse("invalid")).Should().Throw<ArgumentException>();
}
