﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests
{
    public class NoDepsAppTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
    {
        private const string AppName = "WatchNoDepsApp";

        [Fact]
        public async Task RestartProcessOnFileChange()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            App.Start(testAsset, ["--no-hot-reload", "--no-exit"]);
            var processIdentifier = await App.AssertOutputLineStartsWith("Process identifier =");

            UpdateSourceFile(Path.Combine(testAsset.Path, "Program.cs"));

            await App.AssertStarted();
            Assert.DoesNotContain(App.Process.Output, l => l.StartsWith("Exited with error code"));

            var processIdentifier2 = await App.AssertOutputLineStartsWith("Process identifier =");
            Assert.NotEqual(processIdentifier, processIdentifier2);
        }

        [Fact]
        public async Task RestartProcessThatTerminatesAfterFileChange()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            App.Start(testAsset, []);

            var processIdentifier = await App.AssertOutputLineStartsWith("Process identifier =");

            // process should exit after run
            await App.AssertExiting();

            await App.AssertWaitingForFileChangeBeforeRestarting();

            UpdateSourceFile(Path.Combine(testAsset.Path, "Program.cs"));
            await App.AssertStarted();

            var processIdentifier2 = await App.AssertOutputLineStartsWith("Process identifier =");
            Assert.NotEqual(processIdentifier, processIdentifier2);
            await App.AssertExiting(); // process should exit after run
        }
    }
}
