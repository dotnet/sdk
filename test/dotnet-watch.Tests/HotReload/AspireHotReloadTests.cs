// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireHotReloadTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/sdk/issues/53058, https://github.com/dotnet/sdk/issues/53061, https://github.com/dotnet/sdk/issues/53114
    public async Task Aspire_BuildError_ManualRestart()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAspire")
            .WithSource();

        var serviceProjectDisplay = $"WatchAspire.ApiService ({ToolsetInfo.CurrentTargetFramework})";
        var webProjectDisplay = $"WatchAspire.Web ({ToolsetInfo.CurrentTargetFramework})";
        var hostProjectDisplay = $"WatchAspire.AppHost ({ToolsetInfo.CurrentTargetFramework})";

        var serviceSourcePath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "Program.cs");
        var serviceProjectPath = Path.Combine(testAsset.Path, "WatchAspire.ApiService", "WatchAspire.ApiService.csproj");
        var serviceSource = File.ReadAllText(serviceSourcePath, Encoding.UTF8);

        var webSourcePath = Path.Combine(testAsset.Path, "WatchAspire.Web", "Program.cs");
        var webProjectPath = Path.Combine(testAsset.Path, "WatchAspire.Web", "WatchAspire.Web.csproj");

        App.Start(testAsset, ["-lp", "http"], relativeProjectDirectory: "WatchAspire.AppHost", testFlags: TestFlags.ReadKeyFromStdin);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // check that Aspire server output is logged via dotnet-watch reporter:
        await App.WaitUntilOutputContains("dotnet watch ⭐ Now listening on:");

        // wait until after all DCP sessions have started:
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Session started");
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#2] Session started");
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#3] Session started");

        // MigrationService terminated:
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");

        // working directory of the service should be its project directory:
        await App.WaitUntilOutputContains($"ApiService working directory: '{Path.GetDirectoryName(serviceProjectPath)}'");

        // Service -- valid code change:
        UpdateSourceFile(
            serviceSourcePath,
            serviceSource.Replace("Enumerable.Range(1, 5)", "Enumerable.Range(1, 10)"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

        await App.WaitUntilOutputContains("Using Aspire process launcher.");

        // Only one browser should be launched (dashboard). The child process shouldn't launch a browser.
        Assert.Equal(1, App.Process.Output.Count(line => line.StartsWith("dotnet watch ⌚ Launching browser: ")));
        App.Process.ClearOutput();

        // rude edit with build error:
        UpdateSourceFile(
            serviceSourcePath,
            serviceSource.Replace("record WeatherForecast", "record WeatherForecast2"));

        // the prompt is printed into stdout while the error is printed into stderr, so they might arrive in any order:
        await App.WaitUntilOutputContains("  ❔ Do you want to restart these projects? Yes (y) / No (n) / Always (a) / Never (v)");
        await App.WaitUntilOutputContains(MessageDescriptor.RestartNeededToApplyChanges);

        await App.WaitUntilOutputContains($"dotnet watch ❌ [{serviceProjectDisplay}] {serviceSourcePath}(40,1): error ENC0020: Renaming record 'WeatherForecast' requires restarting the application.");
        await App.WaitUntilOutputContains("dotnet watch ⌚ Affected projects:");
        await App.WaitUntilOutputContains("dotnet watch ⌚   WatchAspire.ApiService");
        App.Process.ClearOutput();

        App.SendKey('y');

        await App.WaitUntilOutputContains(MessageDescriptor.FixBuildError);

        await App.WaitUntilOutputContains("Application is shutting down...");

        await App.WaitUntilOutputContains(MessageDescriptor.Exited, serviceProjectDisplay);

        await App.WaitUntilOutputContains(MessageDescriptor.Building);
        await App.WaitUntilOutputContains("error CS0246: The type or namespace name 'WeatherForecast' could not be found");
        App.Process.ClearOutput();

        // fix build error:
        UpdateSourceFile(
            serviceSourcePath,
            serviceSource.Replace("WeatherForecast", "WeatherForecast2"));

        await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRestarted.GetMessage(1));

        await App.WaitUntilOutputContains(MessageDescriptor.BuildSucceeded);
        await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRebuilt);
        await App.WaitUntilOutputContains($"Starting: '{serviceProjectPath}'");

        // Wait for the process to start before shutting down, so we can reliably verify Exited message below.
        // The agent startup hook might not be initialized yet (signal handlers registered),
        // so the process might need to be forcefully killed. We could wait until the agent is initialized
        // but it's good to test this scenario.
        await App.WaitUntilOutputContains(MessageDescriptor.LaunchedProcess, serviceProjectDisplay);

        App.Process.ClearOutput();

        App.SendControlC();

        await App.WaitUntilOutputContains(MessageDescriptor.ShutdownRequested);

        // Not checking specific exited message since on shutdown we might see non-zero exit codes
        await App.WaitUntilOutputContains($"[{serviceProjectDisplay}] Exited");
        await App.WaitUntilOutputContains($"[{webProjectDisplay}] Exited");
        await App.WaitUntilOutputContains($"[{hostProjectDisplay}] Exited");

        await App.WaitUntilOutputContains("dotnet watch ⭐ Disposing server ...");

        // TODO: these are not reliably reported: https://github.com/dotnet/sdk/issues/53308
        //await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Stop session");
        //await App.WaitUntilOutputContains("dotnet watch ⭐ [#2] Stop session");
        //await App.WaitUntilOutputContains("dotnet watch ⭐ [#3] Stop session");

        // Note: do not check that 'sessionTerminated' notification is received.
        // It might get cancelled and not delivered on shutdown.
    }

    [PlatformSpecificFact(TestPlatforms.Windows)] // https://github.com/dotnet/sdk/issues/53058, https://github.com/dotnet/sdk/issues/53061, https://github.com/dotnet/sdk/issues/53114
    public async Task Aspire_NoEffect_AutoRestart()
    {
        var tfm = ToolsetInfo.CurrentTargetFramework;
        var testAsset = TestAssets.CopyTestAsset("WatchAspire")
            .WithSource();

        var webSourcePath = Path.Combine(testAsset.Path, "WatchAspire.Web", "Program.cs");
        var webProjectPath = Path.Combine(testAsset.Path, "WatchAspire.Web", "WatchAspire.Web.csproj");
        var webSource = File.ReadAllText(webSourcePath, Encoding.UTF8);

        App.Start(testAsset, ["-lp", "http", "--non-interactive"], relativeProjectDirectory: "WatchAspire.AppHost");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Session started");
        await App.WaitUntilOutputContains(MessageDescriptor.Exited, $"WatchAspire.MigrationService ({tfm})");
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'sessionTerminated'");

        // migration service output should not be printed to dotnet-watch output, it should be sent via DCP as a notification:
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#1] Sending 'serviceLogs': log_message='      Migration complete', is_std_err=False");

        // wait until after DCP sessions have been started for all projects:
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#3] Session started");

        App.AssertOutputDoesNotContain(new Regex("^ +Migration complete"));

        App.Process.ClearOutput();

        // no-effect edit:
        UpdateSourceFile(webSourcePath, src => src.Replace("/* top-level placeholder */", "builder.Services.AddRazorComponents();"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        await App.WaitUntilOutputContains("dotnet watch ⭐ [#3] Session started");
        await App.WaitUntilOutputContains(MessageDescriptor.ProjectsRestarted.GetMessage(1));
        App.AssertOutputDoesNotContain("⚠");

        // The process exited and should not participate in Hot Reload:
        App.AssertOutputDoesNotContain($"[WatchAspire.MigrationService ({tfm})]");
        App.AssertOutputDoesNotContain("dotnet watch ⭐ [#1]");

        App.Process.ClearOutput();

        // lambda body edit:
        UpdateSourceFile(webSourcePath, src => src.Replace("Hello world!", "<Updated>"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        await App.WaitUntilOutputContains($"dotnet watch 🕵️ [WatchAspire.Web ({tfm})] Updates applied.");
        App.AssertOutputDoesNotContain(MessageDescriptor.ProjectsRebuilt);
        App.AssertOutputDoesNotContain(MessageDescriptor.ProjectsRestarted);
        App.AssertOutputDoesNotContain("⚠");

        // The process exited and should not participate in Hot Reload:
        App.AssertOutputDoesNotContain($"[WatchAspire.MigrationService ({tfm})]");
        App.AssertOutputDoesNotContain("dotnet watch ⭐ [#1]");
    }
}
