// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Watch.UnitTests;

public class BrowserConnectorTests
{
    [Theory]
    [InlineData(null, "https://localhost:1234", "https://localhost:1234")]
    [InlineData("", "https://localhost:1234", "https://localhost:1234")]
    [InlineData("   ", "https://localhost:1234", "https://localhost:1234")]
    [InlineData("", "a/b", "a/b")]
    [InlineData("x/y", "a/b", "a/b")]
    [InlineData("a/b?X=1", "https://localhost:1234", "https://localhost:1234/a/b?X=1")]
    [InlineData("https://localhost:1000/a/b", "https://localhost:1234", "https://localhost:1000/a/b")]
    [InlineData("https://localhost:1000/x/y?z=u", "https://localhost:1234/a?b=c", "https://localhost:1000/x/y?z=u")]
    public void GetLaunchUrl(string? profileLaunchUrl, string outputLaunchUrl, string expected)
    {
        Assert.Equal(expected, BrowserConnector.GetLaunchUrl(profileLaunchUrl, outputLaunchUrl));
    }
}
