using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlazorWasmTestApp;

public sealed class BrowserTestRunner
{
    public async Task<int> RunAsync()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        builder.AddMSTest(() => [typeof(SampleTests).Assembly]);

        using ITestApplication application = await builder.BuildAsync();
        return await application.RunAsync();
    }
}
