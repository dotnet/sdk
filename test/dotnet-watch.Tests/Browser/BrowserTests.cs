// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Watch.UnitTests;

public class BrowserTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task LaunchesBrowserOnStart()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchBrowserLaunchApp")
            .WithSource();

        App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

        // check that all app output is printed out:
        await App.WaitForOutputLineContaining("Content root path:");

        Assert.Contains(App.Process.Output, line => line.Contains("Application started. Press Ctrl+C to shut down."));
        Assert.Contains(App.Process.Output, line => line.Contains("Hosting environment: Development"));

        // Verify we launched the browser.
        App.AssertOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage("https://localhost:5001", ""));
    }

    [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/aspnetcore/issues/63759
    public async Task BrowserDiagnostics()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
                .WithSource();

        App.UseTestBrowser();

        var url = $"http://localhost:{TestOptions.GetTestPort()}";
        var tfm = ToolsetInfo.CurrentTargetFramework;

        App.Start(testAsset, ["--urls", url], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitForOutputLineContaining(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        await App.WaitForOutputLineContaining(MessageDescriptor.ConfiguredToLaunchBrowser);
        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

        // Verify the browser has been launched.
        await App.WaitUntilOutputContains($"🧪 Test browser opened at '{url}'.");

        // Verify the browser connected to the refresh server.
        await App.WaitUntilOutputContains(MessageDescriptor.ConnectedToRefreshServer, "Browser #1");

        App.Process.ClearOutput();

        var homePagePath = Path.Combine(testAsset.Path, "RazorApp", "Components", "Pages", "Home.razor");
        
        // rude edit:
        UpdateSourceFile(homePagePath, src => src.Replace("/* member placeholder */", """
            public virtual int F() => 1;
            """));

        var errorMessage = $"{homePagePath}(13,9): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.";
        var jsonErrorMessage = JsonSerializer.Serialize(errorMessage);

        await App.WaitForOutputLineContaining(errorMessage);

        await App.WaitForOutputLineContaining("Do you want to restart your app?");

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":[{{jsonErrorMessage}}]}
            """);

        // auto restart next time:
        App.SendKey('a');

        // browser page is reloaded when the app restarts:
        await App.WaitForOutputLineContaining(MessageDescriptor.ReloadingBrowser, $"RazorApp ({tfm})");

        // browser page was reloaded after the app restarted:
        await App.WaitUntilOutputContains("""
            🧪 Received: {"type":"Reload"}
            """);

        // no other browser message sent:
        Assert.Equal(2, App.Process.Output.Count(line => line.Contains("🧪")));

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

        App.Process.ClearOutput();

        // another rude edit:
        UpdateSourceFile(homePagePath, src => src.Replace("public virtual int F() => 1;", "/* member placeholder */"));

        errorMessage = $"{homePagePath}(11,5): error ENC0033: Deleting method 'F()' requires restarting the application.";
        await App.WaitForOutputLineContaining("[auto-restart] " + errorMessage);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":["Restarting application to apply changes ..."]}
            """);

        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

        // browser page was reloaded after the app restarted:
        await App.WaitUntilOutputContains("""
            🧪 Received: {"type":"Reload"}
            """);

        // no other browser message sent:
        Assert.Equal(2, App.Process.Output.Count(line => line.Contains("🧪")));

        App.Process.ClearOutput();

        // valid edit:
        UpdateSourceFile(homePagePath, src => src.Replace("/* member placeholder */", "public int F() => 1;"));

        await App.WaitForOutputLineContaining(MessageDescriptor.HotReloadSucceeded);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":[]}
            """);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"RefreshBrowser"}
            """);

        // no other browser message sent:
        Assert.Equal(2, App.Process.Output.Count(line => line.Contains("🧪")));
    }
}
