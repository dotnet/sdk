// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class VersionSanitizerTests
{
    #region Channel Keywords

    [Theory]
    [InlineData("latest", "latest")]
    [InlineData("LATEST", "latest")]
    [InlineData("lts", "lts")]
    [InlineData("LTS", "lts")]
    [InlineData("sts", "sts")]
    [InlineData("STS", "sts")]
    [InlineData("preview", "preview")]
    [InlineData("PREVIEW", "preview")]
    public void Sanitize_ChannelKeywords_ReturnsLowercase(string input, string expected)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Empty/Null Input

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_EmptyOrNull_ReturnsUnspecified(string? input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("unspecified");
    }

    #endregion

    #region Valid Version Patterns - Major Only

    [Theory]
    [InlineData("8")]
    [InlineData("9")]
    [InlineData("10")]
    public void Sanitize_MajorVersionOnly_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Major.Minor

    [Theory]
    [InlineData("8.0")]
    [InlineData("9.0")]
    [InlineData("10.0")]
    public void Sanitize_MajorMinorVersion_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Feature Band Wildcards

    [Theory]
    [InlineData("8.0.1xx")]
    [InlineData("9.0.3xx")]
    [InlineData("10.0.1xx")]
    [InlineData("10.0.2xx")]
    public void Sanitize_FeatureBandWildcard_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("10.0.10x")]
    [InlineData("10.0.20x")]
    [InlineData("8.0.40x")]
    [InlineData("9.0.30x")]
    public void Sanitize_SingleDigitWildcard_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Specific Versions

    [Theory]
    [InlineData("8.0.100")]
    [InlineData("9.0.304")]
    [InlineData("10.0.100")]
    [InlineData("10.0.102")]
    public void Sanitize_SpecificVersion_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - With Prerelease Tokens

    [Theory]
    [InlineData("10.0.100-preview.1")]
    [InlineData("10.0.100-preview.1.24234.5")]
    [InlineData("10.0.0-preview.1.25080.5")]
    [InlineData("9.0.0-rc.1.24431.7")]
    [InlineData("10.0.100-rc.1")]
    [InlineData("10.0.100-rc.2.25502.107")]
    public void Sanitize_PreviewAndRcVersions_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("8.0.100-alpha")]
    [InlineData("8.0.100-alpha.1")]
    [InlineData("8.0.100-beta")]
    [InlineData("8.0.100-beta.2")]
    [InlineData("8.0.100-rtm")]
    [InlineData("8.0.100-ga")]
    [InlineData("8.0.100-dev.1")]
    [InlineData("8.0.100-ci.12345")]
    [InlineData("8.0.100-servicing.1")]
    public void Sanitize_OtherKnownPrereleaseTokens_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Invalid Patterns - PII Protection

    [Theory]
    [InlineData("10.0.0-mycreditcardisblank")]
    [InlineData("10.0.100-mysecretpassword")]
    [InlineData("10.0.100-user@email.com")]
    [InlineData("10.0.100-johndoe")]
    [InlineData("10.0.100-unknown")]
    public void Sanitize_UnknownPrereleaseToken_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("hello world")]
    [InlineData("/path/to/file")]
    [InlineData("C:\\Users\\secret")]
    [InlineData("some random text with pii")]
    public void Sanitize_ArbitraryText_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    #endregion

    #region IsSafePattern Tests

    [Theory]
    [InlineData("latest", true)]
    [InlineData("10.0.100", true)]
    [InlineData("10.0.20x", true)]
    [InlineData("10.0.1xx", true)]
    [InlineData("10.0.100-preview.1", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void IsSafePattern_SafeInputs_ReturnsTrue(string? input, bool expected)
    {
        var result = VersionSanitizer.IsSafePattern(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("10.0.0-mypii")]
    [InlineData("random-text")]
    [InlineData("user@example.com")]
    public void IsSafePattern_UnsafeInputs_ReturnsFalse(string input)
    {
        var result = VersionSanitizer.IsSafePattern(input);
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("  10.0.100  ", "10.0.100")] // Whitespace trimmed
    [InlineData("  latest  ", "latest")] // Keyword with whitespace
    public void Sanitize_WhitespacePadding_TrimmedCorrectly(string input, string expected)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void KnownPrereleaseTokens_ContainsExpectedTokens()
    {
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("preview");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("rc");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("alpha");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("beta");
    }

    #endregion

    #region Invalid Wildcard Patterns

    [Theory]
    [InlineData("10.0.4xxx")]   // Too many x's
    [InlineData("10.0.10xx")]   // Two digits + xx would exceed 3-digit patch limit
    [InlineData("10.0.100x")]   // Three digits + x would exceed 3-digit patch limit
    [InlineData("10.0.1xxx")]   // Too many x's
    [InlineData("10.0.xxxx")]   // No digits, all x's
    [InlineData("10.0.xxx")]    // No digits, all x's
    [InlineData("10.0.xx")]     // No digits, just xx
    [InlineData("10.0.x")]      // No digits, just x
    public void Sanitize_InvalidWildcardPatterns_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    #endregion
}
