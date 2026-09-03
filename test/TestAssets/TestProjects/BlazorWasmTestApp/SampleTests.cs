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
}
