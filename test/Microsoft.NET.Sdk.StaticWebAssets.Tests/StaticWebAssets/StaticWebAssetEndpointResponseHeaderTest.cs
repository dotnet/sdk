// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class StaticWebAssetEndpointResponseHeaderTest
{
    [Fact]
    public void PopulateFromMetadataValue_ValidJson_ParsesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":"text/javascript"},{"Name":"Cache-Control","Value":"public, max-age=31536000"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(2);
        headers[0].Name.Should().Be("Content-Type");
        headers[0].Value.Should().Be("text/javascript");
        headers[1].Name.Should().Be("Cache-Control");
        headers[1].Value.Should().Be("public, max-age=31536000");
    }

    [Fact]
    public void PopulateFromMetadataValue_WellKnownHeaders_UsesInternedStrings()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":"application/json"},{"Name":"Cache-Control","Value":"no-cache"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(2);

        // Should use interned strings for well-known header names
        headers[0].Name.Should().BeSameAs(WellKnownResponseHeaders.ContentType);
        headers[1].Name.Should().BeSameAs(WellKnownResponseHeaders.CacheControl);
    }

    [Fact]
    public void PopulateFromMetadataValue_WellKnownHeaderValues_UsesInternedStrings()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":"text/javascript"},{"Name":"Content-Encoding","Value":"gzip"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(2);

        // Should use interned strings for well-known header values
        headers[0].Value.Should().BeSameAs(WellKnownResponseHeaderValues.TextJavascript);
        headers[1].Value.Should().BeSameAs(WellKnownResponseHeaderValues.Gzip);
    }

    [Fact]
    public void PopulateFromMetadataValue_UnknownHeaders_UsesOriginalStrings()
    {
        // Arrange
        var json = """[{"Name":"X-Custom-Header","Value":"custom-value"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(1);
        headers[0].Name.Should().Be("X-Custom-Header");
        headers[0].Value.Should().Be("custom-value");

        // Should not be the same instance as interned strings
        headers[0].Name.Should().NotBeSameAs(WellKnownResponseHeaders.ContentType);
        headers[0].Value.Should().NotBeSameAs(WellKnownResponseHeaderValues.TextJavascript);
    }

    [Fact]
    public void PopulateFromMetadataValue_EmptyJson_DoesNotAddHeaders()
    {
        // Arrange
        var json = "[]";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_NullOrEmptyString_DoesNotAddHeaders()
    {
        // Arrange
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act & Assert - should not throw
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(null, headers);
        headers.Should().BeEmpty();

        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue("", headers);
        headers.Should().BeEmpty();
    }

    [Fact]
    public void PopulateFromMetadataValue_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":}]"""; // Invalid JSON
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act & Assert
        var action = () => StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);
        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void PopulateFromMetadataValue_MixedHeaders_HandlesCorrectly()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":"text/javascript"},{"Name":"X-Custom","Value":"custom"},{"Name":"Cache-Control","Value":"no-cache"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(3);

        // Well-known headers should use interned strings
        headers[0].Name.Should().BeSameAs(WellKnownResponseHeaders.ContentType);
        headers[0].Value.Should().BeSameAs(WellKnownResponseHeaderValues.TextJavascript);
        headers[2].Name.Should().BeSameAs(WellKnownResponseHeaders.CacheControl);
        headers[2].Value.Should().BeSameAs(WellKnownResponseHeaderValues.NoCache);

        // Custom header should not use interned strings
        headers[1].Name.Should().Be("X-Custom");
        headers[1].Value.Should().Be("custom");
        headers[1].Name.Should().NotBeSameAs(WellKnownResponseHeaders.ContentType);
        headers[1].Value.Should().NotBeSameAs(WellKnownResponseHeaderValues.TextJavascript);
    }

    [Fact]
    public void PopulateFromMetadataValue_ExistingList_ClearsExistingItems_BeforeAppendingElements()
    {
        // Arrange
        var json = """[{"Name":"Content-Type","Value":"application/json"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "X-Existing", Value = "existing-value" }
        };

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(1);
        headers[0].Name.Should().Be("Content-Type");
        headers[0].Value.Should().Be("application/json");
    }

    [Fact]
    public void PopulateFromMetadataValue_CaseSensitiveHeaderNames_UsesInternedStrings()
    {
        // Arrange - header names are case-sensitive in our implementation
        var json = """[{"Name":"Content-Type","Value":"text/javascript"},{"Name":"content-type","Value":"no-cache"}]""";
        var headers = new List<StaticWebAssetEndpointResponseHeader>();

        // Act
        StaticWebAssetEndpointResponseHeader.PopulateFromMetadataValue(json, headers);

        // Assert
        headers.Should().HaveCount(2);

        // Exact match should use interned string
        headers[0].Name.Should().BeSameAs(WellKnownResponseHeaders.ContentType);
        headers[0].Value.Should().BeSameAs(WellKnownResponseHeaderValues.TextJavascript);

        // Different case should not use interned string
        headers[1].Name.Should().Be("content-type");
        headers[1].Name.Should().NotBeSameAs(WellKnownResponseHeaders.ContentType);
    }

    [Fact]
    public void ToMetadataValue_List_EmptyList_ReturnsEmptyJsonArray()
    {
        // Arrange
        var headers = new List<StaticWebAssetEndpointResponseHeader>();
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_NullList_ReturnsEmptyJsonArray()
    {
        // Arrange
        List<StaticWebAssetEndpointResponseHeader>? headers = null;
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void ToMetadataValue_List_SingleHeader_ReturnsValidJson()
    {
        // Arrange
        var headers = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "Content-Type", Value = "application/json" }
        };
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert
        result.Should().Be("""[{"Name":"Content-Type","Value":"application/json"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_MultipleHeaders_ReturnsValidJson()
    {
        // Arrange
        var headers = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "Content-Type", Value = "text/javascript" },
            new() { Name = "Cache-Control", Value = "no-cache" }
        };
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert
        result.Should().Be("""[{"Name":"Content-Type","Value":"text/javascript"},{"Name":"Cache-Control","Value":"no-cache"}]""");
    }

    [Fact]
    public void ToMetadataValue_List_SpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var headers = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "X-Quote", Value = "\"quoted\"" },
            new() { Name = "X-Backslash", Value = "back\\slash" },
            new() { Name = "X-Newline", Value = "line\nbreak" }
        };
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert
        // The result should be valid JSON that can be parsed back
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(result);
        parsed.Should().HaveCount(3);
        parsed[0].Name.Should().Be("X-Quote");
        parsed[0].Value.Should().Be("\"quoted\"");
        parsed[1].Name.Should().Be("X-Backslash");
        parsed[1].Value.Should().Be("back\\slash");
        parsed[2].Name.Should().Be("X-Newline");
        parsed[2].Value.Should().Be("line\nbreak");
    }

    [Fact]
    public void ToMetadataValue_List_ReuseBufferAndWriter_WorksCorrectly()
    {
        // Arrange
        var headers1 = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "Content-Type", Value = "application/json" }
        };
        var headers2 = new List<StaticWebAssetEndpointResponseHeader>
        {
            new() { Name = "Cache-Control", Value = "no-cache" }
        };
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act - First serialization
        var result1 = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers1, context);

        // Act - Second serialization with same context
        var result2 = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers2, context);

        // Assert
        result1.Should().Be("""[{"Name":"Content-Type","Value":"application/json"}]""");
        result2.Should().Be("""[{"Name":"Cache-Control","Value":"no-cache"}]""");
    }

    [Fact]
    public void ToMetadataValue_ArrayAndList_SameInput_ProduceSameOutput()
    {
        // Arrange
        var arrayHeaders = new[]
        {
            new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
            new StaticWebAssetEndpointResponseHeader { Name = "Cache-Control", Value = "no-cache" }
        };
        var listHeaders = new List<StaticWebAssetEndpointResponseHeader>(arrayHeaders);
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var arrayResult = StaticWebAssetEndpointResponseHeader.ToMetadataValue(arrayHeaders);
        var listResult = StaticWebAssetEndpointResponseHeader.ToMetadataValue(listHeaders, context);

        // Assert - Both should produce semantically equivalent JSON
        var arrayParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(arrayResult);
        var listParsed = JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(listResult);

        arrayParsed.Should().BeEquivalentTo(listParsed);
    }

    [Fact]
    public void ToMetadataValue_List_LargeInput_HandlesCorrectly()
    {
        // Arrange - Create a large list to test buffer resizing
        var headers = new List<StaticWebAssetEndpointResponseHeader>();
        for (int i = 0; i < 100; i++)
        {
            headers.Add(new StaticWebAssetEndpointResponseHeader { Name = $"X-Header{i}", Value = $"value{i}" });
        }
        using var context = StaticWebAssetEndpointResponseHeader.CreateWriter();

        // Act
        var result = StaticWebAssetEndpointResponseHeader.ToMetadataValue(headers, context);

        // Assert - Should be valid JSON and contain all headers
        var parsed = JsonSerializer.Deserialize<StaticWebAssetEndpointResponseHeader[]>(result);
        parsed.Should().HaveCount(100);

        for (int i = 0; i < 100; i++)
        {
            parsed[i].Name.Should().Be($"X-Header{i}");
            parsed[i].Value.Should().Be($"value{i}");
        }
    }
}
