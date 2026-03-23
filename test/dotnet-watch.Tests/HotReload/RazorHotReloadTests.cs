// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

public class RazorHotReloadTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [PlatformSpecificTheory(TestPlatforms.Windows | TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/53114
    [CombinatorialData]
    public async Task BlazorWasm(bool projectSpecifiesCapabilities)
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;

        var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm", identifier: projectSpecifiesCapabilities.ToString())
            .WithSource();

        if (projectSpecifiesCapabilities)
        {
            testAsset = testAsset.WithProjectChanges(proj =>
            {
                proj.Root.Descendants()
                    .First(e => e.Name.LocalName == "PropertyGroup")
                    .Add(XElement.Parse("""
                        <WebAssemblyHotReloadCapabilities>Baseline;AddMethodToExistingType</WebAssemblyHotReloadCapabilities>
                        """));
            });
        }

        var port = TestOptions.GetTestPort();
        App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.MockBrowser);

        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);

        // env variable passed when launching the server:
        await App.WaitUntilOutputContains($"HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES=dotnet watch 🕵️ [blazorwasm ({tfm})]");

        // Middleware should have been loaded to blazor-devserver before the browser is launched:
        await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BlazorWasmHotReloadMiddleware[0]");
        await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserScriptMiddleware[0]");
        await App.WaitUntilOutputContains("Middleware loaded. Script /_framework/aspnetcore-browser-refresh.js");
        await App.WaitUntilOutputContains("Middleware loaded. Script /_framework/blazor-hotreload.js");
        await App.WaitUntilOutputContains("dbug: Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware");
        await App.WaitUntilOutputContains("Middleware loaded: DOTNET_MODIFIABLE_ASSEMBLIES=debug, __ASPNETCORE_BROWSER_TOOLS=true");

        // shouldn't see any agent messages (agent is not loaded into blazor-devserver):
        App.AssertOutputDoesNotContain("Loaded into process");

        // Browser is launched based on blazor-devserver output "Now listening on: ...".
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}"));

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        var newSource = """
            @page "/"
            <h1>Updated</h1>
            """;

        UpdateSourceFile(Path.Combine(testAsset.Path, "Pages", "Index.razor"), newSource);

        // WebAssemblyHotReloadCapabilities set by project is overwritten in WASM SDK targets:
        await App.WaitUntilOutputContains("dotnet watch 🔥 Hot reload capabilities: AddExplicitInterfaceImplementation AddInstanceFieldToExistingType AddMethodToExistingType AddStaticFieldToExistingType Baseline ChangeCustomAttributes GenericAddFieldToExistingType GenericAddMethodToExistingType GenericUpdateMethod NewTypeDefinition UpdateParameters.");
        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
    }

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/53114
    public async Task BlazorWasm_MSBuildWarning()
    {
        var testAsset = TestAssets
            .CopyTestAsset("WatchBlazorWasm")
            .WithSource()
            .WithProjectChanges(proj =>
            {
                proj.Root.Descendants()
                    .Single(e => e.Name.LocalName == "ItemGroup")
                    .Add(XElement.Parse("""
                        <AdditionalFiles Include="Pages\Index.razor" />
                        """));
            });

        var port = TestOptions.GetTestPort();
        App.Start(testAsset, ["--urls", "http://localhost:" + port], testFlags: TestFlags.MockBrowser);

        await App.WaitUntilOutputContains("dotnet watch ⚠ msbuild: [Warning] Duplicate source file");
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/53114
    public async Task BlazorWasm_Restart()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm")
            .WithSource();

        var port = TestOptions.GetTestPort();
        App.Start(testAsset, ["--urls", "http://localhost:" + port, "--non-interactive"], testFlags: TestFlags.ReadKeyFromStdin | TestFlags.MockBrowser);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
        await App.WaitUntilOutputContains(MessageDescriptor.PressCtrlRToRestart);

        // Browser is launched based on blazor-devserver output "Now listening on: ...".
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}"));

        App.SendControlR();

        await App.WaitUntilOutputContains(MessageDescriptor.ReloadingBrowser);
    }

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/53114
    public async Task BlazorWasmHosted()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasmHosted")
            .WithSource();

        var port = TestOptions.GetTestPort();
        App.Start(testAsset, ["--urls", "http://localhost:" + port], "blazorhosted", testFlags: TestFlags.MockBrowser);

        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
        await App.WaitUntilOutputContains(MessageDescriptor.ApplicationKind_BlazorHosted);
    }

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/53114
    public async Task Razor_Component_ScopedCssAndStaticAssets()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
            .WithSource();

        var port = TestOptions.GetTestPort();
        App.Start(testAsset, ["--urls", "http://localhost:" + port], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.MockBrowser);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage($"http://localhost:{port}"));
        App.Process.ClearOutput();

        var scopedCssPath = Path.Combine(testAsset.Path, "RazorClassLibrary", "Components", "Example.razor.css");

        var newCss = """
            .example {
                color: blue;
            }
            """;

        UpdateSourceFile(scopedCssPath, newCss);
        await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
        await App.WaitUntilOutputContains(MessageDescriptor.NoManagedCodeChangesToApply);

        await App.WaitUntilOutputContains(MessageDescriptor.SendingStaticAssetUpdateRequest.GetMessage("wwwroot/RazorClassLibrary.bundle.scp.css"));
        App.Process.ClearOutput();

        var cssPath = Path.Combine(testAsset.Path, "RazorApp", "wwwroot", "app.css");
        UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

        await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
        await App.WaitUntilOutputContains(MessageDescriptor.NoManagedCodeChangesToApply);

        await App.WaitUntilOutputContains(MessageDescriptor.SendingStaticAssetUpdateRequest.GetMessage("wwwroot/app.css"));
        App.Process.ClearOutput();
    }
}
