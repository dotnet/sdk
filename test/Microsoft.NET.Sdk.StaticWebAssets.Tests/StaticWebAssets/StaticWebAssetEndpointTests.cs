// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class StaticWebAssetEndpointTests
{
    [Fact]
    public void Constructor_FromTaskItem_InitializesCorrectly()
    {
        // Arrange
        var taskItem = new TaskItem("test-route");
        taskItem.SetMetadata("AssetFile", "/path/to/asset.js");
        taskItem.SetMetadata("Selectors", "[{\"Name\":\"Content-Type\",\"Value\":\"application/javascript\",\"Quality\":\"\"}]");
        taskItem.SetMetadata("ResponseHeaders", "[{\"Name\":\"Cache-Control\",\"Value\":\"max-age=31536000\"}]");
        taskItem.SetMetadata("EndpointProperties", "[{\"Name\":\"integrity\",\"Value\":\"sha256-test\"}]");

        // Act
        var endpoint = StaticWebAssetEndpoint.FromTaskItem(taskItem);

        // Assert
        Assert.Equal("test-route", endpoint.Route);
        Assert.Equal("/path/to/asset.js", endpoint.AssetFile);
        Assert.Single(endpoint.Selectors);
        Assert.Equal("Content-Type", endpoint.Selectors[0].Name);
        Assert.Single(endpoint.ResponseHeaders);
        Assert.Equal("Cache-Control", endpoint.ResponseHeaders[0].Name);
        Assert.Single(endpoint.EndpointProperties);
        Assert.Equal("integrity", endpoint.EndpointProperties[0].Name);
    }

    [Fact]
    public void SetSelectorsString_UpdatesSelectorsProperty()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var selectorsJson = "[{\"Name\":\"Content-Encoding\",\"Value\":\"gzip\",\"Quality\":\"0.8\"}]";

        // Act
        endpoint.SetSelectorsString(selectorsJson);

        // Assert
        Assert.Single(endpoint.Selectors);
        Assert.Equal("Content-Encoding", endpoint.Selectors[0].Name);
        Assert.Equal("gzip", endpoint.Selectors[0].Value);
        Assert.Equal("0.8", endpoint.Selectors[0].Quality);
    }

    [Fact]
    public void SetResponseHeadersString_UpdatesResponseHeadersProperty()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var headersJson = "[{\"Name\":\"Content-Type\",\"Value\":\"text/javascript\"},{\"Name\":\"ETag\",\"Value\":\"\\\"test-etag\\\"\"}]";

        // Act
        endpoint.SetResponseHeadersString(headersJson);

        // Assert
        Assert.Equal(2, endpoint.ResponseHeaders.Length);
        Assert.Contains(endpoint.ResponseHeaders, h => h.Name == "Content-Type" && h.Value == "text/javascript");
        Assert.Contains(endpoint.ResponseHeaders, h => h.Name == "ETag" && h.Value == "\"test-etag\"");
    }

    [Fact]
    public void SetEndpointPropertiesString_UpdatesEndpointPropertiesProperty()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var propertiesJson = "[{\"Name\":\"fingerprint\",\"Value\":\"abc123\"},{\"Name\":\"integrity\",\"Value\":\"sha256-test\"}]";

        // Act
        endpoint.SetEndpointPropertiesString(propertiesJson);

        // Assert
        Assert.Equal(2, endpoint.EndpointProperties.Length);
        Assert.Contains(endpoint.EndpointProperties, p => p.Name == "fingerprint" && p.Value == "abc123");
        Assert.Contains(endpoint.EndpointProperties, p => p.Name == "integrity" && p.Value == "sha256-test");
    }

    [Fact]
    public void Selectors_Setter_SortsArray()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var selectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Content-Type", Value = "text/javascript", Quality = "" },
            new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.8" }
        };

        // Act
        endpoint.Selectors = selectors;

        // Assert - should be sorted by name
        Assert.Equal("Content-Encoding", endpoint.Selectors[0].Name);
        Assert.Equal("Content-Type", endpoint.Selectors[1].Name);
    }

    [Fact]
    public void ResponseHeaders_Setter_SortsArray()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var headers = new[]
        {
            new StaticWebAssetEndpointResponseHeader { Name = "ETag", Value = "\"test\"" },
            new StaticWebAssetEndpointResponseHeader { Name = "Cache-Control", Value = "max-age=31536000" }
        };

        // Act
        endpoint.ResponseHeaders = headers;

        // Assert - should be sorted by name
        Assert.Equal("Cache-Control", endpoint.ResponseHeaders[0].Name);
        Assert.Equal("ETag", endpoint.ResponseHeaders[1].Name);
    }

    [Fact]
    public void EndpointProperties_Setter_SortsArray()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var properties = new[]
        {
            new StaticWebAssetEndpointProperty { Name = "integrity", Value = "sha256-test" },
            new StaticWebAssetEndpointProperty { Name = "fingerprint", Value = "abc123" }
        };

        // Act
        endpoint.EndpointProperties = properties;

        // Assert - should be sorted by name
        Assert.Equal("fingerprint", endpoint.EndpointProperties[0].Name);
        Assert.Equal("integrity", endpoint.EndpointProperties[1].Name);
    }

    [Fact]
    public void Equals_SameData_ReturnsTrue()
    {
        // Arrange
        var endpoint1 = CreateTestEndpoint();
        var endpoint2 = CreateTestEndpoint();

        // Act & Assert
        Assert.True(endpoint1.Equals(endpoint2));
        Assert.Equal(endpoint1.GetHashCode(), endpoint2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentRoute_ReturnsFalse()
    {
        // Arrange
        var endpoint1 = CreateTestEndpoint();
        var endpoint2 = CreateTestEndpoint();
        endpoint2.Route = "different-route";

        // Act & Assert
        Assert.False(endpoint1.Equals(endpoint2));
    }

    [Fact]
    public void Equals_DifferentSelectors_ReturnsFalse()
    {
        // Arrange
        var endpoint1 = CreateTestEndpoint();
        var endpoint2 = CreateTestEndpoint();
        endpoint2.Selectors = new[] { new StaticWebAssetEndpointSelector { Name = "Different", Value = "selector", Quality = "" } };

        // Act & Assert
        Assert.False(endpoint1.Equals(endpoint2));
    }

    [Fact]
    public void CompareTo_DifferentRoutes_ReturnsCorrectOrder()
    {
        // Arrange
        var endpoint1 = CreateTestEndpoint();
        endpoint1.Route = "a-route";
        var endpoint2 = CreateTestEndpoint();
        endpoint2.Route = "z-route";

        // Act
        var comparison = endpoint1.CompareTo(endpoint2);

        // Assert
        Assert.True(comparison < 0);
    }

    [Fact]
    public void ToTaskItem_ReturnsValidTaskItem()
    {
        // Arrange
        var endpoint = CreateTestEndpoint();

        // Act
        var taskItem = endpoint.ToTaskItem();

        // Assert
        Assert.Equal(endpoint.Route, taskItem.ItemSpec);
        Assert.Equal(endpoint.AssetFile, taskItem.GetMetadata("AssetFile"));
        Assert.NotEmpty(taskItem.GetMetadata("Selectors"));
        Assert.NotEmpty(taskItem.GetMetadata("ResponseHeaders"));
        Assert.NotEmpty(taskItem.GetMetadata("EndpointProperties"));
    }

    [Fact]
    public void ITaskItem2_GetMetadataValueEscaped_ReturnsCorrectValues()
    {
        // Arrange
        var endpoint = CreateTestEndpoint();
        var taskItem = (ITaskItem2)endpoint;

        // Act & Assert
        Assert.Equal(endpoint.AssetFile, taskItem.GetMetadataValueEscaped("AssetFile"));
        Assert.NotEmpty(taskItem.GetMetadataValueEscaped("Selectors"));
        Assert.NotEmpty(taskItem.GetMetadataValueEscaped("ResponseHeaders"));
        Assert.NotEmpty(taskItem.GetMetadataValueEscaped("EndpointProperties"));
    }

    [Fact]
    public void ITaskItem2_SetMetadataValueLiteral_UpdatesProperties()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();
        var taskItem = (ITaskItem2)endpoint;

        // Act
        taskItem.SetMetadataValueLiteral("AssetFile", "/new/path/asset.js");
        taskItem.SetMetadataValueLiteral("Selectors", "[{\"Name\":\"Test\",\"Value\":\"value\",\"Quality\":\"\"}]");

        // Assert
        Assert.Equal("/new/path/asset.js", endpoint.AssetFile);
        Assert.Single(endpoint.Selectors);
        Assert.Equal("Test", endpoint.Selectors[0].Name);
    }

    [Fact]
    public void StringSerialization_MaintainsSortingConsistency()
    {
        // Arrange
        var endpoint = new StaticWebAssetEndpoint();

        // Unsorted input arrays
        var selectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Z-Selector", Value = "value1", Quality = "" },
            new StaticWebAssetEndpointSelector { Name = "A-Selector", Value = "value2", Quality = "0.8" }
        };

        var headers = new[]
        {
            new StaticWebAssetEndpointResponseHeader { Name = "Z-Header", Value = "value1" },
            new StaticWebAssetEndpointResponseHeader { Name = "A-Header", Value = "value2" }
        };

        var properties = new[]
        {
            new StaticWebAssetEndpointProperty { Name = "z-property", Value = "value1" },
            new StaticWebAssetEndpointProperty { Name = "a-property", Value = "value2" }
        };

        // Act - Set via array properties (should sort)
        endpoint.Selectors = selectors;
        endpoint.ResponseHeaders = headers;
        endpoint.EndpointProperties = properties;

        // Get string representations
        var selectorsString = ((ITaskItem2)endpoint).GetMetadataValueEscaped("Selectors");
        var headersString = ((ITaskItem2)endpoint).GetMetadataValueEscaped("ResponseHeaders");
        var propertiesString = ((ITaskItem2)endpoint).GetMetadataValueEscaped("EndpointProperties");

        // Create new endpoint from strings
        var endpoint2 = new StaticWebAssetEndpoint();
        endpoint2.SetSelectorsString(selectorsString);
        endpoint2.SetResponseHeadersString(headersString);
        endpoint2.SetEndpointPropertiesString(propertiesString);

        // Assert - Both endpoints should be equal and have sorted data
        Assert.True(endpoint.Equals(endpoint2));

        // Check that arrays are sorted
        Assert.Equal("A-Selector", endpoint.Selectors[0].Name);
        Assert.Equal("Z-Selector", endpoint.Selectors[1].Name);
        Assert.Equal("A-Header", endpoint.ResponseHeaders[0].Name);
        Assert.Equal("Z-Header", endpoint.ResponseHeaders[1].Name);
        Assert.Equal("a-property", endpoint.EndpointProperties[0].Name);
        Assert.Equal("z-property", endpoint.EndpointProperties[1].Name);

        // Check that deserialized arrays are also sorted
        Assert.Equal("A-Selector", endpoint2.Selectors[0].Name);
        Assert.Equal("Z-Selector", endpoint2.Selectors[1].Name);
        Assert.Equal("A-Header", endpoint2.ResponseHeaders[0].Name);
        Assert.Equal("Z-Header", endpoint2.ResponseHeaders[1].Name);
        Assert.Equal("a-property", endpoint2.EndpointProperties[0].Name);
        Assert.Equal("z-property", endpoint2.EndpointProperties[1].Name);
    }

    [Fact]
    public void FromMetadataValue_AndToMetadataValue_RoundTrip()
    {
        // Arrange
        var originalSelectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Content-Type", Value = "text/javascript", Quality = "" },
            new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.8" }
        };

        // Act - Serialize to string
        var json = StaticWebAssetEndpointSelector.ToMetadataValue(originalSelectors);

        // Deserialize back to array
        var deserializedSelectors = StaticWebAssetEndpointSelector.FromMetadataValue(json);

        // Assert
        Assert.Equal(originalSelectors.Length, deserializedSelectors.Length);
        for (int i = 0; i < originalSelectors.Length; i++)
        {
            Assert.Equal(originalSelectors[i].Name, deserializedSelectors[i].Name);
            Assert.Equal(originalSelectors[i].Value, deserializedSelectors[i].Value);
            Assert.Equal(originalSelectors[i].Quality, deserializedSelectors[i].Quality);
        }
    }

    private static StaticWebAssetEndpoint CreateTestEndpoint()
    {
        var endpoint = new StaticWebAssetEndpoint
        {
            Route = "test-route.js",
            AssetFile = "/path/to/asset.js"
        };

        endpoint.Selectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Content-Type", Value = "text/javascript", Quality = "" }
        };

        endpoint.ResponseHeaders = new[]
        {
            new StaticWebAssetEndpointResponseHeader { Name = "Cache-Control", Value = "max-age=31536000" }
        };

        endpoint.EndpointProperties = new[]
        {
            new StaticWebAssetEndpointProperty { Name = "integrity", Value = "sha256-test" }
        };

        return endpoint;
    }
}
