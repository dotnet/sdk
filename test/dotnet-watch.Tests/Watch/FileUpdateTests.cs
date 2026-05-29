// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class FileUpdateTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task RestartProcessOnFileChange()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        App.Start(testAsset, ["--no-hot-reload", "--no-exit"]);
        var processIdentifier = await App.WaitUntilOutputContains("Process identifier =");
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        App.Process.ClearOutput();

        UpdateSourceFile(Path.Combine(testAsset.Path, "Program.cs"));

        await App.WaitUntilOutputContains("Started");

        var processIdentifier2 = await App.WaitUntilOutputContains("Process identifier =");
        Assert.NotEqual(processIdentifier, processIdentifier2);
        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }

    [Fact]
    public async Task RestartProcessThatTerminatesAfterFileChange()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        App.Start(testAsset, []);

        var processIdentifier = await App.WaitUntilOutputContains("Process identifier =");

        // process should exit after run
        await App.WaitUntilOutputContains("Exiting");

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);
        App.Process.ClearOutput();

        UpdateSourceFile(Path.Combine(testAsset.Path, "Program.cs"));
        await App.WaitUntilOutputContains("Started");

        var processIdentifier2 = await App.WaitUntilOutputContains("Process identifier =");
        Assert.NotEqual(processIdentifier, processIdentifier2);
        await App.WaitUntilOutputContains("Exiting"); // process should exit after run
    }

    /// <summary>
    /// Validates `dotnet watch test` scenario: https://github.com/dotnet/sdk/issues/52528
    /// </summary>
    [Fact(Skip = "https://github.com/dotnet/sdk/issues/54176")]
    public async Task TestCommand()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchXUnit")
            .WithSource();

        var testFile = Path.Combine(testAsset.Path, "UnitTest1.cs");
        File.WriteAllText(testFile, """
            using Xunit;

            public class UnitTest1
            {
                [Fact]
                public void Test1()
                {
                    Assert.True(false);
                }
            }
            """);

        App.Start(testAsset, ["test"]);

        await App.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        await App.WaitUntilOutputContains("Failed!");
        await App.WaitForOutputLineContaining(MessageDescriptor.WaitingForFileChangeBeforeRestarting);
        App.Process.ClearOutput();

        UpdateSourceFile(testFile, """
            using Xunit;
            
            public class UnitTest1
            {
                [Fact]
                public void Test1()
                {
                    Assert.True(true);
                }
            }
            """);

        await App.WaitUntilOutputContains("Passed!");
    }
}
