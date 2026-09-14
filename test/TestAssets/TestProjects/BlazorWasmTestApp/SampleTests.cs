using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlazorWasmTestApp;

[TestClass]
public sealed class SampleTests
{
    [TestMethod]
    public void RunsInsideBrowserWasm()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
    }

    [TestMethod]
    public void RunsAnotherTestInsideBrowserWasm()
    {
        Assert.IsTrue(OperatingSystem.IsBrowser());
    }

    [TestMethod]
    [Ignore("https://github.com/dotnet/sdk/issues/54091")]
    public void SkipsInsideBrowserWasm()
    {
        Assert.Fail("This test verifies browser-hosted skip reporting.");
    }
}
