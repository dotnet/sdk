// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BrowserWasmTestApp;

[TestClass]
public sealed class BrowserWasmTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RunsInsideBrowserWasm()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
        string artifact = Path.Combine(Path.GetTempPath(), "browser-runtime.txt");
        File.WriteAllText(artifact, "Created inside browser WebAssembly.");
        TestContext.AddResultFile(artifact);
    }
}
