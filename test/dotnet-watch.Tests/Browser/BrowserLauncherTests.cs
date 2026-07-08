// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class BrowserLauncherTests
{
    [TestMethod]
    [DataRow(null, "https://localhost:1234", "https://localhost:1234")]
    [DataRow(null, "https://localhost:1234/", "https://localhost:1234/")]
    [DataRow("", "https://localhost:1234", "https://localhost:1234")]
    [DataRow("   ", "https://localhost:1234", "https://localhost:1234")]
    [DataRow("", "a/b", "a/b")]
    [DataRow("x/y", "a/b", "a/b")]
    [DataRow("a/b?X=1", "https://localhost:1234", "https://localhost:1234/a/b?X=1")]
    [DataRow("https://localhost:1000/", "https://localhost:1234", "https://localhost:1000/")]
    [DataRow("https://localhost:1000/a/b", "https://localhost:1234", "https://localhost:1000/a/b")]
    [DataRow("https://localhost:1000/x/y?z=u", "https://localhost:1234/a?b=c", "https://localhost:1000/x/y?z=u")]
    public void GetLaunchUrl(string? profileLaunchUrl, string outputLaunchUrl, string expected)
    {
        Assert.AreEqual(expected, BrowserLauncher.GetLaunchUrl(profileLaunchUrl, outputLaunchUrl));
    }
}
