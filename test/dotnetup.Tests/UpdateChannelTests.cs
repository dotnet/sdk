// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class UpdateChannelTests
{
    [Theory]
    [InlineData("10.0.103", "10.0.103", true)]
    [InlineData("10.0.103", "10.0.104", false)]
    [InlineData("10", "10.0.103", true)]
    [InlineData("10", "11.0.100", false)]
    [InlineData("10.0", "10.0.103", true)]
    [InlineData("10.0", "10.1.100", false)]
    [InlineData("10.0.1xx", "10.0.103", true)]
    [InlineData("10.0.1xx", "10.0.204", false)]
    [InlineData("latest", "10.0.100", true)]
    [InlineData("latest", "11.0.100-preview.1", false)]
    [InlineData("preview", "10.0.100", true)]
    [InlineData("preview", "11.0.100-preview.1", true)]
    [InlineData("lts", "10.0.100", true)]
    [InlineData("lts", "10.0.100-preview.1", false)]
    [InlineData("lts", "9.0.100", false)]
    // Daily channels: behave like their base scope, but only for prerelease versions.
    // A stable release is not a daily build, so it must not satisfy a daily channel.
    [InlineData("daily", "11.0.100-preview.4.25216.37", true)]
    [InlineData("daily", "9.0.103", false)]
    [InlineData("10-daily", "10.0.103-preview.1", true)]
    [InlineData("10-daily", "10.0.103", false)]
    [InlineData("10-daily", "11.0.100", false)]
    [InlineData("10.0-daily", "10.0.103-preview.1", true)]
    [InlineData("10.0-daily", "10.0.103", false)]
    [InlineData("10.0-daily", "10.1.100", false)]
    [InlineData("10.0.1xx-daily", "10.0.103-preview.1", true)]
    [InlineData("10.0.1xx-daily", "10.0.103", false)]
    [InlineData("10.0.1xx-daily", "10.0.204-preview.1", false)]
    public void Matches_ReturnsExpectedResult(string channel, string versionString, bool expected)
    {
        var updateChannel = new UpdateChannel(channel);
        var version = new ReleaseVersion(versionString);

        updateChannel.Matches(version).Should().Be(expected);
    }

    [Theory]
    [InlineData("daily", true, "daily")]
    [InlineData("DAILY", true, "daily")]
    [InlineData("10-daily", true, "10")]
    [InlineData("10.0-daily", true, "10.0")]
    [InlineData("10.0.1xx-daily", true, "10.0.1xx")]
    [InlineData("10.0-DAILY", true, "10.0")]
    [InlineData("10.0", false, "10.0")]
    [InlineData("preview", false, "preview")]
    [InlineData("10.0.100-preview.1", false, "10.0.100-preview.1")]
    public void IsDaily_And_BaseChannel_ReturnExpected(string name, bool expectedIsDaily, string expectedBase)
    {
        var channel = new UpdateChannel(name);

        channel.IsDaily.Should().Be(expectedIsDaily);
        channel.BaseChannel.Should().Be(expectedBase);
    }
}
