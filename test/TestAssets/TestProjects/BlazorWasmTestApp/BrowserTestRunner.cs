using Microsoft.Testing.Platform.Builder;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlazorWasmTestApp;

public sealed class BrowserTestRunner(IJSRuntime jsRuntime)
{
    public async Task<int> RunAsync()
    {
        bool packageIntegration = IsPackageIntegration;
        bool completionAttempted = false;
        try
        {
            int contractVersion = await jsRuntime.InvokeAsync<int>("browserWasmTest.getContractVersion");
            string[] args;
            if (!packageIntegration && contractVersion == 0)
            {
                args = [];
            }
            else if (packageIntegration && contractVersion == 1)
            {
                args = await jsRuntime.InvokeAsync<string[]>("browserWasmTest.getArguments");
            }
            else
            {
                throw new InvalidOperationException($"Unsupported browser testing contract version '{contractVersion}'.");
            }

            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            builder.AddMSTest(() => [typeof(SampleTests).Assembly]);

            using ITestApplication application = await builder.BuildAsync();
            int exitCode = await application.RunAsync();
            if (packageIntegration)
            {
                completionAttempted = true;
                await jsRuntime.InvokeVoidAsync("browserWasmTest.setResult", exitCode);
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (packageIntegration && !completionAttempted)
            {
                try
                {
                    completionAttempted = true;
                    await jsRuntime.InvokeVoidAsync("browserWasmTest.setResult", 1);
                }
                catch (JSException completionException)
                {
                    await ReportFatalErrorAsync(completionException);
                    throw new AggregateException("The browser test failed and its failure could not be reported to the launcher.", ex, completionException);
                }
            }
            else if (packageIntegration)
            {
                await ReportFatalErrorAsync(ex);
            }

            throw;
        }
    }

    private static bool IsPackageIntegration
    {
        get
        {
#if BROWSER_WASM_MTP_PACKAGE
            return true;
#else
            return false;
#endif
        }
    }

    private async Task ReportFatalErrorAsync(Exception exception)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("browserWasmTest.reportFatalError", exception.ToString());
        }
        catch (JSException reportingException)
        {
            Console.Error.WriteLine(reportingException);
        }
    }
}
