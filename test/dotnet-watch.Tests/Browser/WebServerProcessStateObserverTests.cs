// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class WebServerProcessStateObserverTests
{
    [TestMethod]
    [DataRow("Now listening on: https://localhost:1234", false, "https://localhost:1234")]
    [DataRow("Now listening on: https://localhost:1234 \t   ", false, "https://localhost:1234")]
    [DataRow("Now listening on: ", false, null)]
    [DataRow("Now listening on: badurl", false, null)]
    [DataRow("Now listening on: file:///test.html", false, null)]
    [DataRow("Login to the dashboard at file:///test.html", true, null)]
    [DataRow("Now listening on: ftp://localhost/example", false, null)]
    [DataRow("Login to the dashboard at ftp://localhost/example", true, null)]
    [DataRow("Now listening on: http://localhost:1234", false, "http://localhost:1234")]
    [DataRow("Login to the dashboard at http://localhost:1234", true, "http://localhost:1234")]
    [DataRow("Now listening on:", false, null)]
    [DataRow("Now listening on", false, null)]
    [DataRow("Login to the dashboard at https://localhost:1234", true, "https://localhost:1234")]
    [DataRow("Login to the dashboard at https://localhost:1234 \t   ", true, "https://localhost:1234")]
    [DataRow("Login to the dashboard at ", true, null)]
    [DataRow("Login to the dashboard at  badurl", true, null)]
    [DataRow("Login to the dashboard at", true, null)]
    [DataRow("Login", true, null)]
    public void UrlMatching(string outputLine, bool isAspireHost, string? expectedUrl)
    {
        string? observedUrl = null;
        var observer = WebServerProcessStateObserver.GetObserver(isAspireHost, url => observedUrl = url);
        observer(new OutputLine(outputLine, IsError: false));
        Assert.AreEqual(expectedUrl, observedUrl);
    }
}
