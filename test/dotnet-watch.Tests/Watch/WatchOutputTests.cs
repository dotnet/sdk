// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class WatchOutputTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task CapturesStdOutWithNoHotReload()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        App.Start(testAsset, ["--no-hot-reload"]);

        // Verify stdout is captured - application prints "Started" and "Process identifier"
        await App.WaitUntilOutputContains("Started");
        await App.WaitUntilOutputContains("Process identifier =");
        await App.WaitUntilOutputContains("Exiting");
    }
}
