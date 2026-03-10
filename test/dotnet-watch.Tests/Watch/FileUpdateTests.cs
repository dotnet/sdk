// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
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
    }
}
