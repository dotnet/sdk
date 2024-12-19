// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class BrowserLaunchTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchBrowserLaunchApp";

        public BrowserLaunchTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task LaunchesBrowserOnStart()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource();

            App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);

            // check that all app output is printed out:
            await App.AssertOutputLine(line => line.Contains("Content root path:"));

            Assert.Contains(App.Process.Output, line => line.Contains("Application started. Press Ctrl+C to shut down."));
            Assert.Contains(App.Process.Output, line => line.Contains("Hosting environment: Development"));

            // Verify we launched the browser.
            Assert.Contains(App.Process.Output, line => line.Contains("dotnet watch ⌚ Launching browser: https://localhost:5001/"));
        }

        [Fact]
        public async Task UsesBrowserSpecifiedInEnvironment()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource();

            App.EnvironmentVariables.Add("DOTNET_WATCH_BROWSER_PATH", "mycustombrowser.bat");

            App.Start(testAsset, [], testFlags: TestFlags.MockBrowser);
            await App.AssertOutputLineStartsWith(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            await App.AssertOutputLineStartsWith(MessageDescriptor.ConfiguredToLaunchBrowser);

            // Verify we launched the browser.
            await App.AssertOutputLineStartsWith("dotnet watch ⌚ Launching browser: mycustombrowser.bat https://localhost:5001/");
        }
    }
}
