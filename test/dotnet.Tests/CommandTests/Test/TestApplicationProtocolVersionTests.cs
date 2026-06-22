// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace dotnet.Tests.CommandTests.Test;

public class TestApplicationProtocolVersionTests
{
    [Theory]
    [InlineData("1.0.0;1.1.0", "1.1.0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.1.0", "1.1.0")]
    [InlineData("2.0.0", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("0.9.0;1.0.0;9.9.9", "1.0.0")]
    public void GetSupportedProtocolVersion_ReturnsHighestCommonVersion(string? advertisedVersions, string expectedVersion)
    {
        var properties = new Dictionary<byte, string>();
        if (advertisedVersions is not null)
        {
            properties[HandshakeMessagePropertyNames.SupportedProtocolVersions] = advertisedVersions;
        }

        var handshake = new HandshakeMessage(properties);

        string supportedVersion = TestApplication.GetSupportedProtocolVersion(handshake);

        supportedVersion.Should().Be(expectedVersion);
    }
}
