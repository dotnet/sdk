// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class WellKnownEndpointSelectorNamesTest
{
    [Theory]
    [InlineData("Content-Encoding")]
    public void TryGetInternedSelectorName_KnownSelectorNames_ReturnsSameReference(string input)
    {
        // Arrange
        var span = ConvertToUtf8Span(input);

        // Act
        var result1 = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(span);
        var result2 = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(span);

        // Assert
        result1.Should().NotBeNull("method should recognize the well-known endpoint selector name");
        result2.Should().NotBeNull("method should recognize the well-known endpoint selector name");
        result1.Should().BeSameAs(result2, "multiple calls with the same input should return the same reference");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("accept-encoding")] // Different casing
    [InlineData("ACCEPT-ENCODING")] // Different casing
    [InlineData("content-encoding")] // Different casing
    [InlineData("CONTENT-ENCODING")] // Different casing
    [InlineData("Accept-Encoding")] // Different header
    [InlineData("Content-Type")] // Different header
    [InlineData("Content-Length")] // Different header
    [InlineData("")]
    [InlineData("Accept-Encodin")] // Too short by one
    [InlineData("Accept-Encodingx")] // Too long by one
    [InlineData("Content-Encodin")] // Too short by one
    [InlineData("Content-Encodingx")] // Too long by one
    public void TryGetInternedSelectorName_UnknownSelectorNames_ReturnsNull(string selectorName)
    {
        // Arrange
        var span = ConvertToUtf8Span(selectorName);

        // Act
        var result = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedSelectorName_EmptySpan_ReturnsNull()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var result = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(span);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Accept_Encoding")] // Underscore instead of hyphen
    [InlineData("AcceptEncoding")] // No hyphen
    [InlineData("Accept Encoding")] // Space instead of hyphen
    [InlineData("Content_Encoding")] // Underscore instead of hyphen
    [InlineData("ContentEncoding")] // No hyphen
    [InlineData("Content Encoding")] // Space instead of hyphen
    public void TryGetInternedSelectorName_SimilarButDifferentNames_ReturnsNull(string selectorName)
    {
        // Arrange
        var span = ConvertToUtf8Span(selectorName);

        // Act
        var result = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(span);

        // Assert
        result.Should().BeNull();
    }

    // Helper method
    private static ReadOnlySpan<byte> ConvertToUtf8Span(string value)
        => Encoding.UTF8.GetBytes(value);
}
