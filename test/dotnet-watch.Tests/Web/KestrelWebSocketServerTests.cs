// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class KestrelWebSocketServerTests
{
    [TestMethod]
    [DataRow("http://contoso.com:10", "ws://contoso.com:10")]
    [DataRow("https://contoso.com:10", "wss://contoso.com:10")]
    [DataRow("http://127.0.0.10:10", "ws://127.0.0.10:10")]
    [DataRow("https://127.0.0.10:10", "wss://127.0.0.10:10")]
    [DataRow("http://127.0.0.1:10", "ws://localhost:10")]
    [DataRow("https://127.0.0.1:10", "wss://localhost:10")]
    public void Urls(string httpUrl, string wsUrl)
    {
        Assert.AreEqual(wsUrl, KestrelWebSocketServer.GetWebSocketUrl(httpUrl));
    }
}
