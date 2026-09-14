using Microsoft.Testing.Platform.Builder;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlazorWasmTestApp;

public sealed class BrowserTestRunner(IJSRuntime jsRuntime)
{
    public async Task<int> RunAsync()
    {
        string[] args;
        try
        {
            int contractVersion = await jsRuntime.InvokeAsync<int>("browserWasmTest.getContractVersion");
            if (contractVersion != 1)
            {
                throw new InvalidOperationException($"Unsupported browser testing contract version '{contractVersion}'.");
            }

            args = await jsRuntime.InvokeAsync<string[]>("browserWasmTest.getArguments");
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("Unable to read the browser test arguments.", ex);
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [typeof(SampleTests).Assembly]);

        using ITestApplication application = await builder.BuildAsync();
        int exitCode = await application.RunAsync();
        try
        {
            await jsRuntime.InvokeVoidAsync("browserWasmTest.setResult", exitCode);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("Unable to report the browser test result.", ex);
        }

        return exitCode;
    }
}
