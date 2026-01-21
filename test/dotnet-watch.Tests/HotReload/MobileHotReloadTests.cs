// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class MobileHotReloadTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    // Matches WebSocket URLs like ws://127.0.0.1:12345 where port is non-zero
    private static readonly Regex WebSocketServerStartedPattern = new(@"WebSocket server started at: ws://127\.0\.0\.1:([1-9]\d*)");

    /// <summary>
    /// Tests that hot reload works for projects with the HotReloadWebSockets capability.
    /// Mobile workloads (Android, iOS) add this capability to indicate WebSocket transport should be used.
    /// </summary>
    [Fact]
    public async Task HotReload_WithWebSocketCapability()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchMobileApp")
            .WithSource();

        App.Start(testAsset, []);

        await App.WaitForOutputLineContaining("Started");
        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForChanges);

        // Verify the app is detected as requiring WebSocket transport with a dynamically assigned port
        App.AssertOutputContains(MessageDescriptor.ApplicationKind_WebSockets);
        App.AssertOutputContains(WebSocketServerStartedPattern);
        App.AssertOutputContains("WebSocket client connected");

        // Apply a hot reload change
        var programPath = Path.Combine(testAsset.Path, "Program.cs");
        UpdateSourceFile(programPath, src => src.Replace(
            """Console.WriteLine(".");""",
            """Console.WriteLine("Changed!");"""));

        await App.AssertOutputLineStartsWith("Changed!");
    }
}
