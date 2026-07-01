// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationProtocolVersionTests
{
    // Mirrors Microsoft.Testing.Platform.IPC.HandshakeMessagePropertyNames.SupportedProtocolVersions from the
    // Microsoft.Testing.Platform.Internal.DotnetTest package. That type is [Embedded] and therefore not referenceable
    // from this (separate) test assembly, so the wire value is inlined here.
    private const byte SupportedProtocolVersionsProperty = 4;

    [TestMethod]
    [DataRow("1.0.0;1.1.0", "1.1.0")]
    [DataRow("1.0.0", "1.0.0")]
    [DataRow("1.1.0", "1.1.0")]
    [DataRow("2.0.0", "")]
    [DataRow("", "")]
    [DataRow(null, "")]
    [DataRow("0.9.0;1.0.0;9.9.9", "1.0.0")]
    public void GetSupportedProtocolVersion_ReturnsHighestCommonVersion(string? advertisedVersions, string expectedVersion)
    {
        var properties = new Dictionary<byte, string>();
        if (advertisedVersions is not null)
        {
            properties[SupportedProtocolVersionsProperty] = advertisedVersions;
        }

        var handshake = new HandshakeMessage(properties);

        string supportedVersion = TestApplication.GetSupportedProtocolVersion(handshake);

        supportedVersion.Should().Be(expectedVersion);
    }
}
