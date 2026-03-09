// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class MauiHotReloadTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        /// <summary>
        /// Currently only works on Windows.
        /// Add TestPlatforms.OSX once https://github.com/dotnet/sdk/issues/45521 is fixed.
        /// </summary>
        [PlatformSpecificFact(TestPlatforms.Windows)]
        public async Task MauiBlazor()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchMauiBlazor")
                .WithSource();

            var workloadInstallCommandSpec = new DotnetCommand(Logger, ["workload", "install", "maui", "--include-previews"])
            {
                WorkingDirectory = testAsset.Path,
            };

            var result = workloadInstallCommandSpec.Execute();
            Assert.Equal(0, result.ExitCode);

            var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows10.0.19041.0" : "maccatalyst";
            var tfm = $"{ToolsetInfo.CurrentTargetFramework}-{platform}";
            App.Start(testAsset, ["-f", tfm]);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

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
        }
    }
}
