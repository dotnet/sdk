// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

public class MetadataUpdateHandlerTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task NoActions()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
            .WithSource();

        var sourcePath = Path.Combine(testAsset.Path, "Program.cs");

        var source = File.ReadAllText(sourcePath, Encoding.UTF8)
            .Replace("// <metadata update handler placeholder>", """
            [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(AppUpdateHandler))]
            """)
            + """
            class AppUpdateHandler
            {
            }
            """;

        File.WriteAllText(sourcePath, source, Encoding.UTF8);

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<Updated>\");"));

        await App.WaitUntilOutputContains("<Updated>");

        await App.WaitUntilOutputContains(
            $"dotnet watch ⚠ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Expected to find a static method 'ClearCache', 'UpdateApplication' or 'UpdateContent' on type 'AppUpdateHandler, WatchHotReloadApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' but neither exists.");
    }

    [Theory]
    [CombinatorialData]
    public async Task Exception(bool verbose)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: verbose.ToString())
            .WithSource();

        var sourcePath = Path.Combine(testAsset.Path, "Program.cs");

        var source = File.ReadAllText(sourcePath, Encoding.UTF8)
            .Replace("// <metadata update handler placeholder>", """
            [assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(AppUpdateHandler))]
            """)
            + """
            class AppUpdateHandler
            {
                public static void ClearCache(Type[] types) => throw new System.InvalidOperationException("Bug!");
            }
            """;

        File.WriteAllText(sourcePath, source, Encoding.UTF8);

        if (!verbose)
        {
            App.SuppressVerboseLogging();
        }

        App.Start(testAsset, []);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        UpdateSourceFile(sourcePath, source.Replace("Console.WriteLine(\".\");", "Console.WriteLine(\"<Updated>\");"));

        await App.WaitUntilOutputContains("<Updated>");

        await App.WaitUntilOutputContains($"dotnet watch ⚠ [WatchHotReloadApp ({ToolsetInfo.CurrentTargetFramework})] Exception from 'AppUpdateHandler.ClearCache': System.InvalidOperationException: Bug!");

        if (verbose)
        {
            await App.WaitUntilOutputContains(MessageDescriptor.UpdateBatchCompleted);
        }
        else
        {
            // shouldn't see any agent messages:
            App.AssertOutputDoesNotContain("🕵️");
        }
    }
}
