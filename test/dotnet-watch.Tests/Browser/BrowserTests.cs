// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class BrowserTests : DotNetWatchTestBase
{
    [TestMethod]
    public async Task LaunchesBrowserOnStart()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchBrowserLaunchApp")
            .WithSource();

        App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

        // check that all app output is printed out:
        await App.WaitUntilOutputContains("Application started. Press Ctrl+C to shut down.");
        await App.WaitUntilOutputContains("Hosting environment: Development");
        await App.WaitUntilOutputContains("Content root path:");

        // Verify we launched the browser.
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchingBrowser.GetMessage("https://localhost:5001"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
    [DataRow("")]
    [DataRow("https://nonlocal")]
    public async Task OriginValidation(string origin)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps", identifier: origin)
            .WithSource();

        App.UseTestBrowser();
        App.EnvironmentVariables.Add("TEST_BROWSER_ORIGIN_HEADER", origin);

        var url = $"http://localhost:{TestOptions.GetTestPort()}";
        App.Start(testAsset, ["--urls", url], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.ReadKeyFromStdin);

        // Verify that the connection has been rejected:
        await App.WaitUntilOutputContains(new Regex("🧪 Error connecting to 'ws://localhost:.*': The server returned status code '403' when status code '101' was expected."));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
    public async Task OriginValidation_Environment()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
            .WithSource();

        var kestrelUrl = $"http://localhost:{TestOptions.GetTestPort()}";
        var browserUrl = $"http://myhost:1234";

        App.UseTestBrowser();
        App.EnvironmentVariables.Add("DOTNET_WATCH_AUTO_RELOAD_WS_ORIGINS", "myhost");
        App.EnvironmentVariables.Add("TEST_BROWSER_ORIGIN_HEADER", browserUrl);

        App.Start(testAsset, ["--urls", kestrelUrl], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.ReadKeyFromStdin);

        // Verify that the connection has been rejected:
        await App.WaitUntilOutputContains($"🧪 Fetching '{kestrelUrl}/_framework/aspnetcore-browser-refresh.js'");
        await App.WaitUntilOutputContains($"🧪 Setting Origin header to '{browserUrl}'.");

        await App.WaitUntilOutputContains(MessageDescriptor.ConnectedToRefreshServer, "Browser #1");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
    public async Task BrowserDiagnostics()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchRazorWithDeps")
            .WithSource();

        App.UseTestBrowser();

        var kestrelUrl = $"http://localhost:{TestOptions.GetTestPort()}";
        var projectDisplay = $"RazorApp ({ToolsetInfo.CurrentTargetFramework})";

        App.Start(testAsset, ["--urls", kestrelUrl], relativeProjectDirectory: "RazorApp", testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(MessageDescriptor.UsingBrowserRefreshMiddleware);
        await App.WaitUntilOutputContains(MessageDescriptor.ConfiguredToLaunchBrowser);
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // Verify the browser has been launched.
        await App.WaitUntilOutputContains($"🧪 Fetching '{kestrelUrl}/_framework/aspnetcore-browser-refresh.js'");
        await App.WaitUntilOutputContains($"🧪 Setting Origin header to '{kestrelUrl}'.");

        // Verify the browser connected to the refresh server.
        await App.WaitUntilOutputContains(MessageDescriptor.ConnectedToRefreshServer, "Browser #1");

        App.Process.ClearOutput();

        var homePagePath = Path.Combine(testAsset.Path, "RazorApp", "Components", "Pages", "Home.razor");
        
        // rude edit:
        UpdateSourceFile(homePagePath, src => src.Replace("/* member placeholder */", """
            public virtual int F() => 1;
            """));

        var errorMessage = $"[{projectDisplay}] {homePagePath}(13,9): error ENC0023: Adding an abstract method or overriding an inherited method requires restarting the application.";
        var jsonErrorMessage = JsonSerializer.Serialize(errorMessage);

        await App.WaitUntilOutputContains(errorMessage);

        await App.WaitUntilOutputContains("Do you want to restart your app?");

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":[{{jsonErrorMessage}}]}
            """);

        // auto restart next time:
        App.SendKey('a');

        // browser page is reloaded when the app restarts:
        await App.WaitUntilOutputContains(MessageDescriptor.ReloadingBrowser, projectDisplay);

        // browser page was reloaded after the app restarted:
        await App.WaitUntilOutputContains("""
            🧪 Received: {"type":"Reload"}
            """);

        // no other browser refresh messages sent:
        Assert.AreEqual(2, App.Process.Output.Count(line => line.Contains("🧪 Received:")));

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        App.Process.ClearOutput();

        // another rude edit:
        UpdateSourceFile(homePagePath, src => src.Replace("public virtual int F() => 1;", "/* member placeholder */"));

        errorMessage = $"[{projectDisplay}] [auto-restart] {homePagePath}(11,5): error ENC0033: Deleting method 'F()' requires restarting the application.";
        await App.WaitUntilOutputContains(errorMessage);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":["Restarting application to apply changes ..."]}
            """);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // browser page was reloaded after the app restarted:
        await App.WaitUntilOutputContains("""
            🧪 Received: {"type":"Reload"}
            """);

        App.Process.ClearOutput();

        // valid edit:
        UpdateSourceFile(homePagePath, src => src.Replace("/* member placeholder */", "public int F() => 1;"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"ReportDiagnostics","diagnostics":[]}
            """);

        await App.WaitUntilOutputContains($$"""
            🧪 Received: {"type":"RefreshBrowser"}
            """);

        // no other browser refresh messages sent:
        Assert.AreEqual(2, App.Process.Output.Count(line => line.Contains("🧪 Received:")));
    }
}
