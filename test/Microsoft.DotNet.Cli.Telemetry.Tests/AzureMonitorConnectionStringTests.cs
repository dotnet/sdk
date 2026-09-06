// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry.Implementation;

namespace Microsoft.DotNet.Cli.Telemetry.Tests;

[TestClass]
public class AzureMonitorConnectionStringTests
{
    [TestMethod]
    public void ItParsesInstrumentationKeyAndIngestionEndpoint()
    {
        var parsed = AzureMonitorConnectionString.Parse(
            "InstrumentationKey=abc-123;IngestionEndpoint=https://region-0.in.applicationinsights.azure.com/;LiveEndpoint=https://region.livediagnostics.monitor.azure.com/");

        parsed.Should().NotBeNull();
        parsed!.InstrumentationKey.Should().Be("abc-123");
        parsed.IngestionEndpoint.Should().Be(new Uri("https://region-0.in.applicationinsights.azure.com/"));
        parsed.TrackUri.Should().Be(new Uri("https://region-0.in.applicationinsights.azure.com/v2.1/track"));
    }

    [TestMethod]
    public void ItIsCaseInsensitiveAndTrimsWhitespace()
    {
        var parsed = AzureMonitorConnectionString.Parse(" instrumentationkey = key ; ingestionendpoint = https://host/ ");

        parsed.Should().NotBeNull();
        parsed!.InstrumentationKey.Should().Be("key");
        parsed.TrackUri.Should().Be(new Uri("https://host/v2.1/track"));
    }

    [TestMethod]
    public void ItDefaultsIngestionEndpointWhenMissing()
    {
        var parsed = AzureMonitorConnectionString.Parse("InstrumentationKey=key");

        parsed.Should().NotBeNull();
        parsed!.TrackUri.Should().Be(new Uri("https://dc.services.visualstudio.com/v2.1/track"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("IngestionEndpoint=https://host/")]
    public void ItReturnsNullWhenInstrumentationKeyIsMissing(string? connectionString)
    {
        AzureMonitorConnectionString.Parse(connectionString).Should().BeNull();
    }
}
