// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

public class MauiHotReloadTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    /// <summary>
    /// Currently only works on Windows.
    /// Add TestPlatforms.OSX once https://github.com/dotnet/sdk/issues/45521 is fixed.
    /// </summary>
    [PlatformSpecificTheory(TestPlatforms.Windows, Skip = "https://github.com/dotnet/sdk/issues/54150")]
    [CombinatorialData]
    public async Task MauiBlazor(bool selectTfm)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchMauiBlazor", identifier: selectTfm.ToString())
            .WithSource();

        var workloadInstallCommandSpec = new DotnetCommand(Logger, ["workload", "install", "maui", "--include-previews"])
        {
            WorkingDirectory = testAsset.Path,
        };

        var result = workloadInstallCommandSpec.Execute();
        Assert.Equal(0, result.ExitCode);

        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows10.0.19041.0" : "maccatalyst";
        var tfm = $"{ToolsetInfo.CurrentTargetFramework}-{platform}";
        App.Start(testAsset, selectTfm ? [] : ["-f", tfm], testFlags: TestFlags.ReadKeyFromStdin);

        if (selectTfm)
        {
            await App.WaitUntilOutputContains(Resources.SelectTargetFrameworkPrompt);

            // Type the target framework to search and select it via Spectre.Console's search
            foreach (var c in tfm)
            {
                App.SendKey(c);
            }
            App.SendKey('\r');
        }

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // only the selected target framework is built:
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), File.Exists(Path.Combine(testAsset.Path, "bin", "Debug", tfm, "win-x64", "maui-blazor.dll")));
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), File.Exists(Path.Combine(testAsset.Path, "bin", "Debug", tfm, "maccatalyst-x64", "maui-blazor.dll")));

        // update code file:
        var razorPath = Path.Combine(testAsset.Path, "Components", "Pages", "Home.razor");
        UpdateSourceFile(razorPath, content => content.Replace("Hello, world!", "Updated"));

        await App.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);

        await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.HotReload.HotReloadManager.UpdateApplication");
        App.Process.ClearOutput();

        // update static asset:
        var cssPath = Path.Combine(testAsset.Path, "wwwroot", "css", "app.css");
        UpdateSourceFile(cssPath, content => content.Replace("background-color: white;", "background-color: red;"));

        await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
        await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.WebView.StaticContentHotReloadManager.UpdateContent");
        await App.WaitUntilOutputContains(MessageDescriptor.NoManagedCodeChangesToApply);
        App.Process.ClearOutput();

        // update scoped css:
        var scopedCssPath = Path.Combine(testAsset.Path, "Components", "Pages", "Counter.razor.css");
        UpdateSourceFile(scopedCssPath, content => content.Replace("background-color: green", "background-color: red"));

        await App.WaitUntilOutputContains(MessageDescriptor.StaticAssetsChangesApplied);
        await App.WaitUntilOutputContains("Microsoft.AspNetCore.Components.WebView.StaticContentHotReloadManager.UpdateContent");
        await App.WaitUntilOutputContains(MessageDescriptor.NoManagedCodeChangesToApply);

        // no warnings - these would be reported if we tried to access web asset manifest from unbuilt TFMs:
        App.AssertOutputDoesNotContain(MessageDescriptor.StaticWebAssetManifestNotFound);
        App.AssertOutputDoesNotContain(MessageDescriptor.ScopedCssBundleFileNotFound);
        App.AssertOutputDoesNotContain(MessageDescriptor.ManifestFileNotFound);
    }

    /// <summary>
    /// Tests device selection in dotnet-watch using the DotnetRunDevices test asset,
    /// which provides ComputeAvailableDevices and DeployToDevice MSBuild targets.
    /// </summary>
    [Fact]
    public async Task SelectsDevice()
    {
        var testAsset = TestAssets.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var tfm = ToolsetInfo.CurrentTargetFramework;

        // Start watch with ReadKeyFromStdin so we can interact with Spectre prompts.
        // Pass --framework to skip TFM selection and focus on device selection.
        App.Start(testAsset, ["-f", tfm], testFlags: TestFlags.ReadKeyFromStdin);

        // Wait for the device selection prompt
        await App.WaitUntilOutputContains(Resources.SelectDevicePrompt);

        // Type to search for "test-device-1" and select it
        foreach (var c in "test-device-1")
        {
            App.SendKey(c);
        }
        App.SendKey('\r');

        // The app should launch and print the selected device
        await App.WaitUntilOutputContains("Device: test-device-1");
    }

    [Fact]
    public async Task AutoSelectsSingleDevice()
    {
        var testAsset = TestAssets.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var tfm = ToolsetInfo.CurrentTargetFramework;

        // SingleDevice=true makes ComputeAvailableDevices return only one device.
        App.Start(testAsset, ["-f", tfm, "--property", "SingleDevice=true"], testFlags: TestFlags.ReadKeyFromStdin);

        // Should auto-select without prompting and launch the app
        await App.WaitUntilOutputContains("Device: single-device");
    }
}
