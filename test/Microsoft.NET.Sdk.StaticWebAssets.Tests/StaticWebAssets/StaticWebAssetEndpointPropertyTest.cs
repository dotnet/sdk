// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class StaticWebAssetEndpointPropertyTest
{
    [Fact]
    public void PopulateFromMetadataValue_ValidJson_ParsesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"label","Value":"test-label"},{"Name":"integrity","Value":"sha256-abc123"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().HaveCount(2);
        properties[0].Name.Should().Be("label");
        properties[0].Value.Should().Be("test-label");
        properties[1].Name.Should().Be("integrity");
        properties[1].Value.Should().Be("sha256-abc123");
    }

    [Fact]
    public void PopulateFromMetadataValue_WellKnownProperties_UsesInternedStrings()
    {
        // Arrange
        var json = """[{"Name":"label","Value":"test-value"},{"Name":"integrity","Value":"test-integrity"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().HaveCount(2);

        // Should use interned strings for well-known property names
        properties[0].Name.Should().BeSameAs(WellKnownEndpointPropertyNames.Label);
        properties[1].Name.Should().BeSameAs(WellKnownEndpointPropertyNames.Integrity);
    }

    [Fact]
    public void PopulateFromMetadataValue_UnknownProperties_UsesOriginalStrings()
    {
        // Arrange
        var json = """[{"Name":"custom-property","Value":"custom-value"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().HaveCount(1);
        properties[0].Name.Should().Be("custom-property");
        properties[0].Value.Should().Be("custom-value");

        // Should not be the same instance as interned strings
        properties[0].Name.Should().NotBeSameAs(WellKnownEndpointPropertyNames.Label);
        properties[0].Name.Should().NotBeSameAs(WellKnownEndpointPropertyNames.Integrity);
    }

    [Fact]
    public void PopulateFromMetadataValue_EmptyJson_DoesNotAddProperties()
    {
        // Arrange
        var json = "[]";
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_NullOrEmptyString_DoesNotAddProperties()
    {
        // Arrange
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act & Assert - should not throw
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(null, properties);
        properties.Should().BeEmpty();

        StaticWebAssetEndpointProperty.PopulateFromMetadataValue("", properties);
        properties.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = """[{"Name":"label","Value":}]"""; // Invalid JSON
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act & Assert
        var action = () => StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);
        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void PopulateFromMetadataValue_MixedProperties_HandlesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"label","Value":"known-value"},{"Name":"custom-prop","Value":"custom-value"},{"Name":"integrity","Value":"known-integrity"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>();

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().HaveCount(3);

        // Well-known properties should use interned strings
        properties[0].Name.Should().BeSameAs(WellKnownEndpointPropertyNames.Label);
        properties[2].Name.Should().BeSameAs(WellKnownEndpointPropertyNames.Integrity);

        // Custom property should not use interned strings
        properties[1].Name.Should().Be("custom-prop");
        properties[1].Name.Should().NotBeSameAs(WellKnownEndpointPropertyNames.Label);
        properties[1].Name.Should().NotBeSameAs(WellKnownEndpointPropertyNames.Integrity);
    }

    [Fact]
    public void PopulateFromMetadataValue_ExistingList_ClearsExistingItems_BeforeAppendingElements()
    {
        // Arrange
        var json = """[{"Name":"label","Value":"new-value"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "existing", Value = "existing-value" }
        };

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert
        properties.Should().HaveCount(1);
        properties[0].Name.Should().Be("label");
        properties[0].Value.Should().Be("new-value");
    }

    [Fact]
    public void PopulateFromMetadataValue_ExistingList_ClearsExistingItems_ValidatesClearingBehavior()
    {
        // Arrange
        var json = """[{"Name":"newProp","Value":"newValue"}]""";
        var properties = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "existing1", Value = "existingValue1" },
            new() { Name = "existing2", Value = "existingValue2" }
        };

        // Act
        StaticWebAssetEndpointProperty.PopulateFromMetadataValue(json, properties);

        // Assert - List should be cleared and contain only the new property
        properties.Should().HaveCount(1);
        properties[0].Name.Should().Be("newProp");
        properties[0].Value.Should().Be("newValue");
    }

    [Fact]
    public void ToMetadataValue_List_EmptyList_ReturnsEmptyJsonArray()
    {
        // Arrange
        var properties = new List<StaticWebAssetEndpointProperty>();
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_NullList_ReturnsEmptyJsonArray()
    {
        // Arrange
        List<StaticWebAssetEndpointProperty>? properties = null;
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_SingleProperty_ReturnsValidJson()
    {
        // Arrange
        var properties = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "label", Value = "test-label" }
        };
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert
        result.Should().Be("""[{"Name":"label","Value":"test-label"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_MultipleProperties_ReturnsValidJson()
    {
        // Arrange
        var properties = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "label", Value = "test-label" },
            new() { Name = "integrity", Value = "sha256-abc123" }
        };
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert
        result.Should().Be("""[{"Name":"label","Value":"test-label"},{"Name":"integrity","Value":"sha256-abc123"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_SpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var properties = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "quote", Value = "\"quoted\"" },
            new() { Name = "backslash", Value = "back\\slash" },
            new() { Name = "newline", Value = "line\nbreak" }
        };
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert
        // The result should be valid JSON that can be parsed back
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointProperty[]>(result);
        parsed.Should().HaveCount(3);
        parsed[0].Name.Should().Be("quote");
        parsed[0].Value.Should().Be("\"quoted\"");
        parsed[1].Name.Should().Be("backslash");
        parsed[1].Value.Should().Be("back\\slash");
        parsed[2].Name.Should().Be("newline");
        parsed[2].Value.Should().Be("line\nbreak");
    }

    [Fact]
    public void ToMetadataValue_List_ReuseBufferAndWriter_WorksCorrectly()
    {
        // Arrange
        var properties1 = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "label1", Value = "value1" }
        };
        var properties2 = new List<StaticWebAssetEndpointProperty>
        {
            new() { Name = "label2", Value = "value2" }
        };
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act - First serialization
        var result1 = StaticWebAssetEndpointProperty.ToMetadataValue(properties1, context);

        // Act - Second serialization with same context
        var result2 = StaticWebAssetEndpointProperty.ToMetadataValue(properties2, context);

        // Assert
        result1.Should().Be("""[{"Name":"label1","Value":"value1"}]""");
        result2.Should().Be("""[{"Name":"label2","Value":"value2"}]""");
    }

    [Fact]
    public void ToMetadataValue_ArrayAndList_SameInput_ProduceSameOutput()
    {
        // Arrange
        var arrayProperties = new[]
        {
            new StaticWebAssetEndpointProperty { Name = "label", Value = "test-label" },
            new StaticWebAssetEndpointProperty { Name = "integrity", Value = "sha256-abc123" }
        };
        var listProperties = new List<StaticWebAssetEndpointProperty>(arrayProperties);
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var arrayResult = StaticWebAssetEndpointProperty.ToMetadataValue(arrayProperties);
        var listResult = StaticWebAssetEndpointProperty.ToMetadataValue(listProperties, context);

        // Assert - Both should produce semantically equivalent JSON
        var arrayParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointProperty[]>(arrayResult);
        var listParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointProperty[]>(listResult);

        arrayParsed.Should().BeEquivalentTo(listParsed);
    }

    [Fact]
    public void ToMetadataValue_List_LargeInput_HandlesCorrectly()
    {
        // Arrange - Create a large list to test buffer resizing
        var properties = new List<StaticWebAssetEndpointProperty>();
        for (int i = 0; i < 100; i++)
        {
            properties.Add(new StaticWebAssetEndpointProperty { Name = $"prop{i}", Value = $"value{i}" });
        }
        using var context = StaticWebAssetEndpointProperty.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointProperty.ToMetadataValue(properties, context);

        // Assert - Should be valid JSON and contain all properties
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointProperty[]>(result);
        parsed.Should().HaveCount(100);

        for (int i = 0; i < 100; i++)
        {
            parsed[i].Name.Should().Be($"prop{i}");
            parsed[i].Value.Should().Be($"value{i}");
        }
    }
}
