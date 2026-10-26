// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class CtrlRTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task RestartsBuild()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
            .WithSource();

        await using var w = CreateInProcWatcher(testAsset, []);

        var buildCounter = 0;

        w.Observer.RegisterAction(MessageDescriptor.Building, () =>
        {
            if (Interlocked.Increment(ref buildCounter) == 1)
            {
                w.Console.PressKey(new ConsoleKeyInfo('R', ConsoleKey.R, shift: false, alt: false, control: true));
            }
        });

        var restarting = w.Observer.RegisterSemaphore(MessageDescriptor.Restarting);

        // Iteration #1 build should be canceled, iteration #2 should build and launch the app.
        var hasExpectedOutput = w.CreateCompletionSource();
        w.Reporter.OnProcessOutput += line =>
        {
            Assert.DoesNotContain("DOTNET_WATCH_ITERATION = 1", line.Content);

            if (line.Content.Contains("DOTNET_WATCH_ITERATION = 2"))
            {
                hasExpectedOutput.TrySetResult();
            }
        };

        w.Start();

        // 🔄 Restarting
        await restarting.WaitAsync(w.ShutdownSource.Token);

        // DOTNET_WATCH_ITERATION = 2
        await hasExpectedOutput.Task;

        Assert.Equal(2, buildCounter);
    }

    [Fact]
    public async Task CancelsWaitForFileChange()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
            .WithSource();

        var programFilePath = Path.Combine(testAsset.Path, "Program.cs");

        File.WriteAllText(programFilePath, """
            System.Console.WriteLine("<Started>");
            """);

        await using var w = CreateInProcWatcher(testAsset, []);

        w.Observer.RegisterAction(MessageDescriptor.WaitingForFileChangeBeforeRestarting, () =>
        {
            w.Console.PressKey(new ConsoleKeyInfo('R', ConsoleKey.R, shift: false, alt: false, control: true));
        });

        var buildCounter = 0;
        w.Observer.RegisterAction(MessageDescriptor.Building, () => Interlocked.Increment(ref buildCounter));

        var counter = 0;
        var hasExpectedOutput = w.CreateCompletionSource();
        w.Reporter.OnProcessOutput += line =>
        {
            if (line.Content.Contains("<Started>") && Interlocked.Increment(ref counter) == 2)
            {
                hasExpectedOutput.TrySetResult();
            }
        };

        w.Start();

        await hasExpectedOutput.Task;

        Assert.Equal(2, buildCounter);
    }
}
