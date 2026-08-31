// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class UpdateChannelTests
{
    [TestMethod]
    [DataRow("10.0.103", "10.0.103", true)]
    [DataRow("10.0.103", "10.0.104", false)]
    [DataRow("10", "10.0.103", true)]
    [DataRow("10", "11.0.100", false)]
    [DataRow("10.0", "10.0.103", true)]
    [DataRow("10.0", "10.1.100", false)]
    [DataRow("10.0.1xx", "10.0.103", true)]
    [DataRow("10.0.1xx", "10.0.204", false)]
    [DataRow("latest", "10.0.100", true)]
    [DataRow("latest", "11.0.100-preview.1", false)]
    [DataRow("preview", "10.0.100", true)]
    [DataRow("preview", "11.0.100-preview.1", true)]
    [DataRow("lts", "10.0.100", true)]
    [DataRow("lts", "10.0.100-preview.1", false)]
    [DataRow("lts", "9.0.100", false)]
    // Daily channels: behave like their base scope, but only for prerelease versions.
    // A stable release is not a daily build, so it must not satisfy a daily channel.
    [DataRow("daily", "11.0.100-preview.4.25216.37", true)]
    [DataRow("daily", "9.0.103", false)]
    [DataRow("10-daily", "10.0.103-preview.1", true)]
    [DataRow("10-daily", "10.0.103", false)]
    [DataRow("10-daily", "11.0.100", false)]
    [DataRow("10.0-daily", "10.0.103-preview.1", true)]
    [DataRow("10.0-daily", "10.0.103", false)]
    [DataRow("10.0-daily", "10.1.100", false)]
    [DataRow("10.0.1xx-daily", "10.0.103-preview.1", true)]
    [DataRow("10.0.1xx-daily", "10.0.103", false)]
    [DataRow("10.0.1xx-daily", "10.0.204-preview.1", false)]
    // Phase-qualified daily channels: the version's prerelease label must also match.
    [DataRow("11.0.1xx-preview.5-daily", "11.0.100-preview.5.26302.115", true)]
    [DataRow("11.0.1xx-preview5-daily", "11.0.100-preview.5.26302.115", true)]   // dotless form accepted
    [DataRow("11.0.1xx-preview.5-daily", "11.0.100-preview.6.26302.118", false)] // wrong phase
    [DataRow("11.0.1xx-preview.5-daily", "11.0.100", false)]                     // stable
    [DataRow("11.0.1xx-preview.5-daily", "11.0.204-preview.5.26302.115", false)] // wrong feature band
    [DataRow("10.0.1xx-rc.1-daily", "10.0.100-rc.1.25451.107", true)]
    [DataRow("10.0.1xx-rc1-daily", "10.0.100-rc.1.25451.107", true)]
    // Runtime-form prerelease-qualified daily channels: users type major.minor
    // without an SDK feature band; the channel must still match the version's
    // major.minor + prerelease, since aka.ms maps these to the SDK band internally.
    [DataRow("11.0-preview.5-daily", "11.0.0-preview.5.26302.115", true)]   // runtime
    [DataRow("11.0-preview.5-daily", "11.0.100-preview.5.26302.115", true)] // SDK in same band
    [DataRow("11.0-preview.5-daily", "11.0.0-preview.6.26302.118", false)]  // wrong label
    [DataRow("11.0-preview.5-daily", "11.0.0", false)]                       // stable
    public void Matches_ReturnsExpectedResult(string channel, string versionString, bool expected)
    {
        var updateChannel = new UpdateChannel(channel);
        var version = new ReleaseVersion(versionString);

        updateChannel.Matches(version).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("daily", true)]
    [DataRow("DAILY", true)]
    [DataRow("10-daily", true)]
    [DataRow("10.0-daily", true)]
    [DataRow("10.0.1xx-daily", true)]
    [DataRow("10.0-DAILY", true)]
    [DataRow("11.0.1xx-preview.5-daily", true)]
    [DataRow("11.0.1xx-preview5-daily", true)]
    [DataRow("10.0.1xx-rc1-daily", true)]
    [DataRow("10.0", false)]
    [DataRow("preview", false)]
    [DataRow("10.0.100-preview.1", false)]
    public void IsDaily_ReturnsExpected(string name, bool expectedIsDaily)
    {
        var channel = new UpdateChannel(name);

        channel.IsDaily.Should().Be(expectedIsDaily);
    }

    [TestMethod]
    [DataRow("10-daily", "10")]
    [DataRow("10.0-daily", "10.0")]
    [DataRow("10.0.1xx-daily", "10.0.1xx")]
    [DataRow("10.0-DAILY", "10.0")]
    [DataRow("10.0", "10.0")] // no suffix → returned as-is
    [DataRow("preview", "preview")]
    [DataRow("", "")]
    public void StripDailySuffix_ReturnsScope(string channelName, string expectedScope)
    {
        UpdateChannel.StripDailySuffix(channelName).Should().Be(expectedScope);
    }
}
