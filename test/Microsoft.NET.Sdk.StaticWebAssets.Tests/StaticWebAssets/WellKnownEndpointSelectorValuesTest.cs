// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class WellKnownEndpointSelectorValuesTest
{
    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public void TryGetInternedSelectorValue_KnownSelectorValues_ReturnsInternedStrings(string selectorValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(selectorValue);

        // Act
        var result = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(selectorValue);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public void TryGetInternedSelectorValue_KnownSelectorValues_ReturnsSameReference(string input)
    {
        // Arrange
        var span = ConvertToUtf8Span(input);

        // Act
        var result1 = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);
        var result2 = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);

        // Assert
        result1.Should().NotBeNull("method should recognize the well-known selector value");
        result2.Should().NotBeNull("method should recognize the well-known selector value");
        result1.Should().BeSameAs(result2, "multiple calls with the same input should return the same reference");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("GZIP")] // Different casing
    [InlineData("BR")] // Different casing
    [InlineData("Gzip")] // Different casing
    [InlineData("deflate")] // Different compression
    [InlineData("")]
    [InlineData("gz")] // Too short
    [InlineData("gzipx")] // Too long
    [InlineData("b")] // Too short
    [InlineData("brx")] // Too long
    public void TryGetInternedSelectorValue_UnknownSelectorValues_ReturnsNull(string selectorValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(selectorValue);

        // Act
        var result = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedSelectorValue_EmptySpan_ReturnsNull()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var result = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("gzlp")] // Similar to gzip but different
    [InlineData("bt")] // Similar length to br but different
    [InlineData("zip")] // Contains gzip letters but different
    public void TryGetInternedSelectorValue_SimilarButDifferentValues_ReturnsNull(string selectorValue)
    {
        // Arrange
        var span = ConvertToUtf8Span(selectorValue);

        // Act
        var result = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(span);

        // Assert
        result.Should().BeNull();
    }

    // Helper method
    private static ReadOnlySpan<byte> ConvertToUtf8Span(string value)
        => Encoding.UTF8.GetBytes(value);
}
