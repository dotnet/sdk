// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class WellKnownResponseHeaderValuesTest
{
    [Theory]
    [InlineData("bytes")]
    [InlineData("no-cache")]
    [InlineData("max-age=31536000, immutable")]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("application/octet-stream")]
    [InlineData("text/javascript")]
    [InlineData("text/css")]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/svg+xml")]
    [InlineData("Content-Encoding")]
    public void TryGetInternedHeaderValue_KnownHeaderValues_ReturnsInternedStrings(string headerValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerValue);

        // Act
        var result1 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);
        var result2 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeSameAs(result2, "multiple calls should return the same interned string instance");
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("no-cache")]
    [InlineData("max-age=31536000, immutable")]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("application/octet-stream")]
    [InlineData("text/javascript")]
    [InlineData("text/css")]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/svg+xml")]
    [InlineData("Content-Encoding")]
    public void TryGetInternedHeaderValue_KnownHeaderValues_ReturnsSameReference(string input)
    {
        // Arrange
        var span = ConvertToUtf8Span(input);

        // Act
        var result1 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);
        var result2 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result1.Should().NotBeNull("method should recognize the well-known header value");
        result2.Should().NotBeNull("method should recognize the well-known header value");
        result1.Should().BeSameAs(result2, "multiple calls with the same input should return the same reference");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("BYTES")] // Different casing
    [InlineData("No-Cache")] // Different casing
    [InlineData("TEXT/CSS")] // Different casing
    [InlineData("application/XML")] // Different type
    [InlineData("text/plain")] // Different type
    [InlineData("")]
    [InlineData("byte")] // Too short
    [InlineData("bytesx")] // Too long
    [InlineData("no-cach")] // Too short by one
    [InlineData("no-cachex")] // Too long by one
    [InlineData("deflate")] // Different compression
    public void TryGetInternedHeaderValue_UnknownHeaderValues_ReturnsNull(string headerValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerValue);

        // Act
        var result = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedHeaderValue_EmptySpan_ReturnsNull()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var result = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedHeaderValue_UsingUtf8Literals_ReturnsInternedStrings()
    {
        // Test using UTF-8 literals directly for compile-time efficiency

        // Act & Assert
        var gzipResult1 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("gzip"u8);
        var gzipResult2 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("gzip"u8);
        gzipResult1.Should().BeSameAs(gzipResult2);
        gzipResult1.Should().BeSameAs(WellKnownResponseHeaderValues.Gzip);

        var cssResult1 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("text/css"u8);
        var cssResult2 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("text/css"u8);
        cssResult1.Should().BeSameAs(cssResult2);
        cssResult1.Should().BeSameAs(WellKnownResponseHeaderValues.TextCss);

        var jsonResult1 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("application/json"u8);
        var jsonResult2 = WellKnownResponseHeaderValues.TryGetInternedHeaderValue("application/json"u8);
        jsonResult1.Should().BeSameAs(jsonResult2);
        jsonResult1.Should().BeSameAs(WellKnownResponseHeaderValues.ApplicationJson);
    }

    [Fact]
    public void Debug_WellKnownResponseHeaderValues_CheckReferences()
    {
        // Debug test to understand what's happening
        var gzipSpan = ConvertToUtf8Span("gzip");
        var result = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(gzipSpan);

        // Let's debug the actual results
        result.Should().NotBeNull("method should find gzip");
        result.Should().Be("gzip", "content should match");

        // Check if we're getting the right reference
        var isExpectedReference = ReferenceEquals(result, WellKnownResponseHeaderValues.Gzip);
        isExpectedReference.Should().BeTrue($"Expected reference to be the same. Got: '{result}', Expected: '{WellKnownResponseHeaderValues.Gzip}'. ReferenceEquals: {isExpectedReference}");
    }

    [Theory]
    [InlineData("text_css")] // Underscore instead of slash
    [InlineData("textcss")] // No slash
    [InlineData("text css")] // Space instead of slash
    [InlineData("image-png")] // Hyphen instead of slash
    [InlineData("applicationjson")] // No slash
    [InlineData("no_cache")] // Underscore instead of hyphen
    [InlineData("nocache")] // No hyphen
    [InlineData("byte5")] // Similar but different
    [InlineData("qzip")] // Similar but different
    public void TryGetInternedHeaderValue_SimilarButDifferentValues_ReturnsNull(string headerValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerValue);

        // Act
        var result = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("max-age=31536000")] // Missing immutable part
    [InlineData("max-age=31536000,immutable")] // Missing space
    [InlineData("max-age=31536001, immutable")] // Different max-age value
    public void TryGetInternedHeaderValue_SimilarCacheControlValues_ReturnsNull(string headerValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerValue);

        // Act
        var result = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(span);

        // Assert
        result.Should().BeNull();
    }

    // Helper method
    private static ReadOnlySpan<byte> ConvertToUtf8Span(string value)
        => Encoding.UTF8.GetBytes(value);

    // Helper method for UTF-8 literals - more efficient for compile-time known strings
    private static ReadOnlySpan<byte> GetUtf8Span(ReadOnlySpan<byte> utf8Literal)
        => utf8Literal;
}
