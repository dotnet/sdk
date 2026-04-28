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
    public void Matches_ReturnsExpectedResult(string channel, string versionString, bool expected)
    {
        var updateChannel = new UpdateChannel(channel);
        var version = new ReleaseVersion(versionString);

        updateChannel.Matches(version).Should().Be(expected);
    }
}
