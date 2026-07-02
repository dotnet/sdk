// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class VersionSanitizerTests
{
    #region Channel Keywords

    [TestMethod]
    [DataRow("latest", "latest")]
    [DataRow("LATEST", "latest")]
    [DataRow("lts", "lts")]
    [DataRow("LTS", "lts")]
    [DataRow("preview", "preview")]
    [DataRow("PREVIEW", "preview")]
    public void Sanitize_ChannelKeywords_ReturnsLowercase(string input, string expected)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Empty/Null Input

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Sanitize_EmptyOrNull_ReturnsUnspecified(string? input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("unspecified");
    }

    #endregion

    #region Valid Version Patterns - Major Only

    [TestMethod]
    [DataRow("8")]
    [DataRow("9")]
    [DataRow("10")]
    public void Sanitize_MajorVersionOnly_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Major.Minor

    [TestMethod]
    [DataRow("8.0")]
    [DataRow("9.0")]
    [DataRow("10.0")]
    public void Sanitize_MajorMinorVersion_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Feature Band Wildcards

    [TestMethod]
    [DataRow("8.0.1xx")]
    [DataRow("9.0.3xx")]
    [DataRow("10.0.1xx")]
    [DataRow("10.0.2xx")]
    public void Sanitize_FeatureBandWildcard_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    [TestMethod]
    [DataRow("10.0.10x")]
    [DataRow("10.0.20x")]
    [DataRow("8.0.40x")]
    [DataRow("9.0.30x")]
    public void Sanitize_SingleDigitWildcard_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - Specific Versions

    [TestMethod]
    [DataRow("8.0.100")]
    [DataRow("9.0.304")]
    [DataRow("10.0.100")]
    [DataRow("10.0.102")]
    public void Sanitize_SpecificVersion_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Valid Version Patterns - With Prerelease Tokens

    [TestMethod]
    [DataRow("10.0.100-preview.1")]
    [DataRow("10.0.100-preview.1.24234.5")]
    [DataRow("10.0.0-preview.1.25080.5")]
    [DataRow("9.0.0-rc.1.24431.7")]
    [DataRow("10.0.100-rc.1")]
    [DataRow("10.0.100-rc.2.25502.107")]
    public void Sanitize_PreviewAndRcVersions_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    [TestMethod]
    [DataRow("8.0.100-alpha")]
    [DataRow("8.0.100-alpha.1")]
    [DataRow("8.0.100-beta")]
    [DataRow("8.0.100-beta.2")]
    [DataRow("8.0.100-rtm")]
    [DataRow("8.0.100-ga")]
    [DataRow("8.0.100-dev.1")]
    [DataRow("8.0.100-ci.12345")]
    [DataRow("8.0.100-servicing.1")]
    public void Sanitize_OtherKnownPrereleaseTokens_ReturnsAsIs(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(input);
    }

    #endregion

    #region Invalid Patterns - PII Protection

    [TestMethod]
    [DataRow("10.0.0-mycreditcardisblank")]
    [DataRow("10.0.100-mysecretpassword")]
    [DataRow("10.0.100-user@email.com")]
    [DataRow("10.0.100-johndoe")]
    [DataRow("10.0.100-unknown")]
    public void Sanitize_UnknownPrereleaseToken_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    [TestMethod]
    [DataRow("not-a-version")]
    [DataRow("hello world")]
    [DataRow("/path/to/file")]
    [DataRow("C:\\Users\\secret")]
    [DataRow("some random text with pii")]
    public void Sanitize_ArbitraryText_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    #endregion

    #region IsSafePattern Tests

    [TestMethod]
    [DataRow("latest", true)]
    [DataRow("10.0.100", true)]
    [DataRow("10.0.20x", true)]
    [DataRow("10.0.1xx", true)]
    [DataRow("10.0.100-preview.1", true)]
    [DataRow(null, true)]
    [DataRow("", true)]
    public void IsSafePattern_SafeInputs_ReturnsTrue(string? input, bool expected)
    {
        var result = VersionSanitizer.IsSafePattern(input);
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("10.0.0-mypii")]
    [DataRow("random-text")]
    [DataRow("user@example.com")]
    public void IsSafePattern_UnsafeInputs_ReturnsFalse(string input)
    {
        var result = VersionSanitizer.IsSafePattern(input);
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    [DataRow("  10.0.100  ", "10.0.100")] // Whitespace trimmed
    [DataRow("  latest  ", "latest")] // Keyword with whitespace
    public void Sanitize_WhitespacePadding_TrimmedCorrectly(string input, string expected)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be(expected);
    }

    [TestMethod]
    public void KnownPrereleaseTokens_ContainsExpectedTokens()
    {
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("preview");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("rc");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("alpha");
        VersionSanitizer.KnownPrereleaseTokens.Should().Contain("beta");
    }

    #endregion

    #region Invalid Wildcard Patterns

    [TestMethod]
    [DataRow("10.0.4xxx")]   // Too many x's
    [DataRow("10.0.10xx")]   // Two digits + xx would exceed 3-digit patch limit
    [DataRow("10.0.100x")]   // Three digits + x would exceed 3-digit patch limit
    [DataRow("10.0.1xxx")]   // Too many x's
    [DataRow("10.0.xxxx")]   // No digits, all x's
    [DataRow("10.0.xxx")]    // No digits, all x's
    [DataRow("10.0.xx")]     // No digits, just xx
    [DataRow("10.0.x")]      // No digits, just x
    public void Sanitize_InvalidWildcardPatterns_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);
        result.Should().Be("invalid");
    }

    #endregion
}
