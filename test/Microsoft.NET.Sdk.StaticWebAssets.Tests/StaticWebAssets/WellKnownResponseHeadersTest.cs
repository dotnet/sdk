// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class WellKnownResponseHeadersTest
{
    [Theory]
    [InlineData("Accept-Ranges")]
    [InlineData("Cache-Control")]
    [InlineData("Content-Encoding")]
    [InlineData("Content-Length")]
    [InlineData("Content-Type")]
    [InlineData("ETag")]
    [InlineData("Last-Modified")]
    [InlineData("Vary")]
    public void TryGetInternedHeaderName_KnownHeaderNames_ReturnsInternedStrings(string headerName)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerName);

        // Act
        var result = WellKnownResponseHeaders.TryGetInternedHeaderName(span);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(headerName);
    }

    [Theory]
    [InlineData("Accept-Ranges")]
    [InlineData("Cache-Control")]
    [InlineData("Content-Encoding")]
    [InlineData("Content-Length")]
    [InlineData("Content-Type")]
    [InlineData("ETag")]
    [InlineData("Last-Modified")]
    [InlineData("Vary")]
    public void TryGetInternedHeaderName_KnownHeaderNames_ReturnsSameReference(string input)
    {
        // Arrange
        var span = ConvertToUtf8Span(input);

        // Act
        var result1 = WellKnownResponseHeaders.TryGetInternedHeaderName(span);
        var result2 = WellKnownResponseHeaders.TryGetInternedHeaderName(span);

        // Assert
        result1.Should().NotBeNull("method should recognize the well-known response header");
        result2.Should().NotBeNull("method should recognize the well-known response header");
        result1.Should().BeSameAs(result2, "multiple calls with the same input should return the same reference");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("accept-ranges")] // Different casing
    [InlineData("CACHE-CONTROL")] // Different casing
    [InlineData("content-type")] // Different casing
    [InlineData("Authorization")] // Different header
    [InlineData("Host")] // Different header
    [InlineData("")]
    [InlineData("ETag2")] // Too long
    [InlineData("ETa")] // Too short
    [InlineData("Content-Typ")] // Too short by one
    [InlineData("Content-Typex")] // Too long by one
    public void TryGetInternedHeaderName_UnknownHeaderNames_ReturnsNull(string headerName)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerName);

        // Act
        var result = WellKnownResponseHeaders.TryGetInternedHeaderName(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedHeaderName_EmptySpan_ReturnsNull()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var result = WellKnownResponseHeaders.TryGetInternedHeaderName(span);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Accept_Ranges")] // Underscore instead of hyphen
    [InlineData("AcceptRanges")] // No hyphen
    [InlineData("Accept Ranges")] // Space instead of hyphen
    [InlineData("CacheControl")] // No hyphen
    [InlineData("ContentType")] // No hyphen
    [InlineData("ETagx")] // Similar but different
    [InlineData("xETag")] // Similar but different
    [InlineData("Varx")] // Similar but different
    public void TryGetInternedHeaderName_SimilarButDifferentNames_ReturnsNull(string headerName)
    {
        // Arrange
        var span = ConvertToUtf8Span(headerName);

        // Act
        var result = WellKnownResponseHeaders.TryGetInternedHeaderName(span);

        // Assert
        result.Should().BeNull();
    }

    // Helper method
    private static ReadOnlySpan<byte> ConvertToUtf8Span(string value)
        => Encoding.UTF8.GetBytes(value);
}
