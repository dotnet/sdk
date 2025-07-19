// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class WellKnownEndpointPropertyNamesTest
{
    [Theory]
    [InlineData("label")]
    [InlineData("integrity")]
    public void TryGetInternedPropertyName_KnownPropertyNames_ReturnsInternedStrings(string propertyName)
    {
        // Arrange
        var span = ConvertToUtf8Span(propertyName);

        // Act
        var result = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(propertyName);
    }

    [Theory]
    [InlineData("label")]
    [InlineData("integrity")]
    public void TryGetInternedPropertyName_KnownPropertyNames_ReturnsSameReference(string input)
    {
        // Arrange
        var span = ConvertToUtf8Span(input);

        // Act
        var result1 = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);
        var result2 = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);

        // Assert
        result1.Should().NotBeNull("method should recognize the well-known property name");
        result2.Should().NotBeNull("method should recognize the well-known property name");
        result1.Should().BeSameAs(result2, "multiple calls with the same input should return the same reference");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Label")] // Different casing
    [InlineData("INTEGRITY")] // Different casing
    [InlineData("lab")] // Too short
    [InlineData("labels")] // Too long
    [InlineData("")]
    [InlineData("integrit")] // Too short by one
    [InlineData("integrityx")] // Too long by one
    public void TryGetInternedPropertyName_UnknownPropertyNames_ReturnsNull(string propertyName)
    {
        // Arrange
        var span = ConvertToUtf8Span(propertyName);

        // Act
        var result = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetInternedPropertyName_EmptySpan_ReturnsNull()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var result = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("labe1")] // Similar to "label" but with number
    [InlineData("integral")] // Similar to "integrity" but different
    [InlineData("     ")] // Whitespace
    public void TryGetInternedPropertyName_SimilarButDifferentNames_ReturnsNull(string propertyName)
    {
        // Arrange
        var span = ConvertToUtf8Span(propertyName);

        // Act
        var result = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(span);

        // Assert
        result.Should().BeNull();
    }

    // Helper method
    private static ReadOnlySpan<byte> ConvertToUtf8Span(string value)
        => Encoding.UTF8.GetBytes(value);
}
