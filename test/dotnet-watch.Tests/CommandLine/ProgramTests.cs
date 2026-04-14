// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class ProgramTests(ITestOutputHelper output) : DotNetWatchTestBase(output)
{
    [Fact]
    public async Task ConsoleCancelKey()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchKitchenSink")
            .WithSource();

        var console = new TestConsole(Logger);
        var reporter = new TestReporter(Logger);
        var eventObserver = new TestEventObserver();
        var loggerFactory = new LoggerFactory(reporter, LogLevel.Debug);
        var observableLoggerFactory = new TestObservableLoggerFactory(eventObserver, loggerFactory);

        var watching = eventObserver.RegisterSemaphore(MessageDescriptor.WatchingWithHotReload);
        var shutdownRequested = eventObserver.RegisterSemaphore(MessageDescriptor.ShutdownRequested);

        var program = Program.TryCreate(
            TestOptions.GetCommandLineOptions(["--verbose"]),
            console,
            TestOptions.GetEnvironmentOptions(workingDirectory: testAsset.Path, testAsset),
            observableLoggerFactory,
            reporter,
            out var errorCode);

        Assert.Equal(0, errorCode);
        Assert.NotNull(program);

        var run = program.RunAsync();

        await watching.WaitAsync();

        console.PressKey(new ConsoleKeyInfo('C', ConsoleKey.C, shift: false, alt: false, control: true));

        var exitCode = await run;
        Assert.Equal(0, exitCode);

        await shutdownRequested.WaitAsync();
    }

    [Fact]
    public async Task ProjectGraphLoadFailure()
    {
        var testAsset = TestAssets
            .CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource()
            .WithProjectChanges((path, proj) =>
            {
                if (Path.GetFileName(path) == "App.WithDeps.csproj")
                {
                    proj.Root.Descendants()
                        .Single(e => e.Name.LocalName == "ItemGroup")
                        .Add(XElement.Parse("""
                        <ProjectReference Include="NonExistentDirectory\X.csproj" />
                        """));
                }
            });

        App.Start(testAsset, [], "AppWithDeps");

        await App.WaitUntilOutputContains($"dotnet watch ❌ The project file could not be loaded. Could not find a part of the path '{Path.Combine(testAsset.Path, "AppWithDeps", "NonExistentDirectory", "X.csproj")}'.");
        await App.WaitUntilOutputContains(@"dotnet watch 🔨 Failed to load project graph.");
        await App.WaitUntilOutputContains("dotnet watch ⌚ Fix the error to continue or press Ctrl+C to exit.");
    }

    [Fact]
    public async Task ListsFiles()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchGlobbingApp")
           .WithSource();

        App.SuppressVerboseLogging();
        App.Start(testAsset, ["--list"]);
        await App.Process.WaitUntilOutputCompleted();

        var files = App.Process.Output.Where(l => !l.StartsWith("dotnet watch ⌚") && l.Trim() != "");

        AssertEx.EqualFileList(
            testAsset.Path,
            new[]
            {
                "Program.cs",
                "include/Foo.cs",
                "WatchGlobbingApp.csproj",
            },
            files);
    }
}
