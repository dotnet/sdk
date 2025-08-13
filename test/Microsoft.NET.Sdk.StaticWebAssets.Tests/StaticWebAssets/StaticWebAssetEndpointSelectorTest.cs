// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class StaticWebAssetEndpointSelectorTest
{
    [Fact]
    public void PopulateFromMetadataValue_ValidJson_ParsesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Accept","Value":"application/json"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(2);
        selectors[0].Name.Should().Be("Content-Encoding");
        selectors[0].Value.Should().Be("gzip");
        selectors[1].Name.Should().Be("Accept");
        selectors[1].Value.Should().Be("application/json");
    }

    [Fact]
    public void PopulateFromMetadataValue_WellKnownSelectorNames_UsesInternedStrings()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":"gzip"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(1);

        // Should use interned strings for well-known selector names
        selectors[0].Name.Should().BeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
    }

    [Fact]
    public void PopulateFromMetadataValue_WellKnownSelectorValues_UsesInternedStrings()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Content-Encoding","Value":"br"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(2);

        // Should use interned strings for well-known selector values
        selectors[0].Value.Should().BeSameAs(WellKnownEndpointSelectorValues.Gzip);
        selectors[1].Value.Should().BeSameAs(WellKnownEndpointSelectorValues.Brotli);
    }

    [Fact]
    public void PopulateFromMetadataValue_UnknownSelectors_UsesOriginalStrings()
    {
        // Arrange
        var json = """[{"Name":"Custom-Selector","Value":"custom-value"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(1);
        selectors[0].Name.Should().Be("Custom-Selector");
        selectors[0].Value.Should().Be("custom-value");

        // Should not be the same instance as interned strings
        selectors[0].Name.Should().NotBeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
        selectors[0].Value.Should().NotBeSameAs(WellKnownEndpointSelectorValues.Gzip);
    }

    [Fact]
    public void PopulateFromMetadataValue_EmptyJson_DoesNotAddSelectors()
    {
        // Arrange
        var json = "[]";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_NullOrEmptyString_DoesNotAddSelectors()
    {
        // Arrange
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act & Assert - should not throw
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(null, selectors);
        selectors.Should().BeEmpty();

        StaticWebAssetEndpointSelector.PopulateFromMetadataValue("", selectors);
        selectors.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":}]"""; // Invalid JSON
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act & Assert
        var action = () => StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);
        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void PopulateFromMetadataValue_MixedSelectors_HandlesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Custom-Selector","Value":"custom"},{"Name":"Content-Encoding","Value":"br"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(3);

        // Well-known selectors should use interned strings
        selectors[0].Name.Should().BeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
        selectors[0].Value.Should().BeSameAs(WellKnownEndpointSelectorValues.Gzip);
        selectors[2].Name.Should().BeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
        selectors[2].Value.Should().BeSameAs(WellKnownEndpointSelectorValues.Brotli);

        // Custom selector should not use interned strings
        selectors[1].Name.Should().Be("Custom-Selector");
        selectors[1].Value.Should().Be("custom");
        selectors[1].Name.Should().NotBeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
        selectors[1].Value.Should().NotBeSameAs(WellKnownEndpointSelectorValues.Gzip);
    }

    [Fact]
    public void PopulateFromMetadataValue_ExistingList_ClearsExistingItems_BeforeAppendingElements()
    {
        // Arrange
        var json = """[{"Name":"Content-Encoding","Value":"gzip"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Existing-Selector", Value = "existing-value" }
        };

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(1);
        selectors[0].Name.Should().Be("Content-Encoding");
        selectors[0].Value.Should().Be("gzip");
    }

    [Fact]
    public void PopulateFromMetadataValue_ExistingList_ClearsExistingItems_ValidatesClearingBehavior()
    {
        // Arrange
        var json = """[{"Name":"Accept-Encoding","Value":"br"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Content-Encoding", Value = "gzip" },
            new() { Name = "Content-Type", Value = "text/css" }
        };

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert - List should be cleared and contain only the new selector
        selectors.Should().HaveCount(1);
        selectors[0].Name.Should().Be("Accept-Encoding");
        selectors[0].Value.Should().Be("br");
    }

    [Fact]
    public void PopulateFromMetadataValue_CaseSensitiveSelectorNames_UsesInternedStrings()
    {
        // Arrange - Content-Encoding should be case-sensitive for selector names
        var json = """[{"Name":"Content-Encoding","Value":"gzip"},{"Name":"content-encoding","Value":"br"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(2);

        // Exact match should use interned string
        selectors[0].Name.Should().BeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);

        // Different case should not use interned string
        selectors[1].Name.Should().Be("content-encoding");
        selectors[1].Name.Should().NotBeSameAs(WellKnownEndpointSelectorNames.ContentEncoding);
    }

    [Fact]
    public void PopulateFromMetadataValue_CaseSensitiveSelectorValues_UsesInternedStrings()
    {
        // Arrange - Compression values should be case-sensitive
        var json = """[{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Content-Encoding","Value":"GZIP"}]""";
        var selectors = new List<StaticWebAssetEndpointSelector>();

        // Act
        StaticWebAssetEndpointSelector.PopulateFromMetadataValue(json, selectors);

        // Assert
        selectors.Should().HaveCount(2);

        // Exact match should use interned string
        selectors[0].Value.Should().BeSameAs(WellKnownEndpointSelectorValues.Gzip);

        // Different case should not use interned string
        selectors[1].Value.Should().Be("GZIP");
        selectors[1].Value.Should().NotBeSameAs(WellKnownEndpointSelectorValues.Gzip);
    }

    [Fact]
    public void ToMetadataValue_List_EmptyList_ReturnsEmptyJsonArray()
    {
        // Arrange
        var selectors = new List<StaticWebAssetEndpointSelector>();
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_NullList_ReturnsEmptyJsonArray()
    {
        // Arrange
        List<StaticWebAssetEndpointSelector>? selectors = null;
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_SingleSelector_ReturnsValidJson()
    {
        // Arrange
        var selectors = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" }
        };
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert
        result.Should().Be("""[{"Name":"Content-Encoding","Value":"gzip","Quality":"1.0"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_MultipleSelectors_ReturnsValidJson()
    {
        // Arrange
        var selectors = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" },
            new() { Name = "Content-Encoding", Value = "br", Quality = "0.8" }
        };
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert
        result.Should().Be("""[{"Name":"Content-Encoding","Value":"gzip","Quality":"1.0"},{"Name":"Content-Encoding","Value":"br","Quality":"0.8"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_SpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var selectors = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "X-Quote", Value = "\"quoted\"", Quality = "1.0" },
            new() { Name = "X-Backslash", Value = "back\\slash", Quality = "0.9" },
            new() { Name = "X-Newline", Value = "line\nbreak", Quality = "0.8" }
        };
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert
        // The result should be valid JSON that can be parsed back
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointSelector[]>(result);
        parsed.Should().HaveCount(3);
        parsed[0].Name.Should().Be("X-Quote");
        parsed[0].Value.Should().Be("\"quoted\"");
        parsed[0].Quality.Should().Be("1.0");
        parsed[1].Name.Should().Be("X-Backslash");
        parsed[1].Value.Should().Be("back\\slash");
        parsed[1].Quality.Should().Be("0.9");
        parsed[2].Name.Should().Be("X-Newline");
        parsed[2].Value.Should().Be("line\nbreak");
        parsed[2].Quality.Should().Be("0.8");
    }

    [Fact]
    public void ToMetadataValue_List_ReuseBufferAndWriter_WorksCorrectly()
    {
        // Arrange
        var selectors1 = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" }
        };
        var selectors2 = new List<StaticWebAssetEndpointSelector>
        {
            new() { Name = "Content-Encoding", Value = "br", Quality = "0.8" }
        };
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act - First serialization
        var result1 = StaticWebAssetEndpointSelector.ToMetadataValue(selectors1, context);

        // Act - Second serialization with same context
        var result2 = StaticWebAssetEndpointSelector.ToMetadataValue(selectors2, context);

        // Assert
        result1.Should().Be("""[{"Name":"Content-Encoding","Value":"gzip","Quality":"1.0"}]""");
        result2.Should().Be("""[{"Name":"Content-Encoding","Value":"br","Quality":"0.8"}]""");
    }

    [Fact]
    public void ToMetadataValue_ArrayAndList_SameInput_ProduceSameOutput()
    {
        // Arrange
        var arraySelectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" },
            new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "br", Quality = "0.8" }
        };
        var listSelectors = new List<StaticWebAssetEndpointSelector>(arraySelectors);
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var arrayResult = StaticWebAssetEndpointSelector.ToMetadataValue(arraySelectors);
        var listResult = StaticWebAssetEndpointSelector.ToMetadataValue(listSelectors, context);

        // Assert - Both should produce semantically equivalent JSON
        var arrayParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointSelector[]>(arrayResult);
        var listParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointSelector[]>(listResult);

        arrayParsed.Should().BeEquivalentTo(listParsed);
    }

    [Fact]
    public void ToMetadataValue_List_LargeInput_HandlesCorrectly()
    {
        // Arrange - Create a large list to test buffer resizing
        var selectors = new List<StaticWebAssetEndpointSelector>();
        for (int i = 0; i < 100; i++)
        {
            selectors.Add(new StaticWebAssetEndpointSelector { Name = $"Selector{i}", Value = $"value{i}", Quality = $"0.{i:D2}" });
        }
        using var context = StaticWebAssetEndpointSelector.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointSelector.ToMetadataValue(selectors, context);

        // Assert - Should be valid JSON and contain all selectors
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointSelector[]>(result);
        parsed.Should().HaveCount(100);

        for (int i = 0; i < 100; i++)
        {
            parsed[i].Name.Should().Be($"Selector{i}");
            parsed[i].Value.Should().Be($"value{i}");
            parsed[i].Quality.Should().Be($"0.{i:D2}");
        }
    }
}
