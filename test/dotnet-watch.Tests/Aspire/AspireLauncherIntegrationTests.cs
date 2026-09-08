// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.IO.Pipes;
using System.Text.Json;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AspireLauncherIntegrationTests : MSTestFramework::Microsoft.NET.TestFramework.SdkTest
{
    private TestAssetsManager? _testAssetsManager;
    public TestAssetsManager TestAssets => _testAssetsManager ??= new TestAssetsManager(Logger);

    private DualOutputHelper? _logger;
    private DualOutputHelper Logger
        => _logger ??= new DualOutputHelper(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));

    public new void Log(string message, [System.Runtime.CompilerServices.CallerFilePath] string? testPath = null, [System.Runtime.CompilerServices.CallerLineNumber] int testLine = 0)
        => Logger.Log(message, testPath, testLine);

    public static void WriteAllText(string path, string text)
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate);
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(text);
        }

        stream.SetLength(stream.Position);
    }

    public void UpdateSourceFile(string path, string text, [System.Runtime.CompilerServices.CallerFilePath] string? testPath = null, [System.Runtime.CompilerServices.CallerLineNumber] int testLine = 0)
    {
        var existed = File.Exists(path);
        WriteAllText(path, text);
        Log($"File '{path}' " + (existed ? "updated" : "added"), testPath, testLine);
    }

    public void UpdateSourceFile(string path, Func<string, string> contentTransform, [System.Runtime.CompilerServices.CallerFilePath] string? testPath = null, [System.Runtime.CompilerServices.CallerLineNumber] int testLine = 0)
        => UpdateSourceFile(path, contentTransform(File.ReadAllText(path, Encoding.UTF8)), testPath, testLine);

    public void UpdateSourceFile(string path)
        => UpdateSourceFile(path, content => content);

    private WatchableApp CreateHostApp()
        => new(
            Logger,
            executablePath: Path.ChangeExtension(typeof(AspireLauncher).Assembly.Location, PathUtilities.ExecutableExtension).TrimEnd('.'),
            commandName: "host",
            commandArguments: ["--sdk", SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest]);

    private WatchableApp CreateServerApp(string serverPipe)
        => new(
            Logger,
            executablePath: Path.ChangeExtension(typeof(AspireLauncher).Assembly.Location, PathUtilities.ExecutableExtension).TrimEnd('.'),
            commandName: "server",
            commandArguments: ["--sdk", SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "--server", serverPipe]);

    private WatchableApp CreateResourceApp(string serverPipe)
        => new(
            Logger,
            executablePath: Path.ChangeExtension(typeof(AspireLauncher).Assembly.Location, PathUtilities.ExecutableExtension).TrimEnd('.'),
            commandName: "resource",
            commandArguments: ["--server", serverPipe]);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
    public async Task Host()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var projectDir = Path.Combine(testAsset.Path, "AppWithDeps");
        var projectPath = Path.Combine(projectDir, "App.WithDeps.csproj");

        await using var host = CreateHostApp();
        host.Start(testAsset, ["--entrypoint", projectPath]);

        await host.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
        await host.WaitUntilOutputContains("Started");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
    public async Task ServerAndResources()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc")
            .WithSource();

        var tfm = ToolsetInfo.CurrentTargetFramework;
        var serviceDirA = Path.Combine(testAsset.Path, "ServiceA");
        var serviceProjectA = Path.Combine(serviceDirA, "A.csproj");
        var serviceDirB = Path.Combine(testAsset.Path, "ServiceB");
        var serviceProjectB = Path.Combine(serviceDirB, "B.csproj");
        var libDir = Path.Combine(testAsset.Path, "Lib");
        var libSource = Path.Combine(libDir, "Lib.cs");

        var pipeId = Guid.NewGuid();
        var serverPipe = $"SERVER_{pipeId:N}";
        var statusPipeName = $"STATUS_{pipeId:N}";
        var controlPipeName = $"CONTROL_{pipeId:N}";

        await using var server = CreateServerApp(serverPipe);
        await using var serviceA = CreateResourceApp(serverPipe);
        await using var serviceB = CreateResourceApp(serverPipe);

        // resource can be started before the server, they will wait for the server to start:
        serviceA.Start(testAsset, ["--entrypoint", serviceProjectA]);
        serviceB.Start(testAsset, ["--entrypoint", serviceProjectB]);

        // The expected status events delivered by the server over the status pipe (listed in causal order;
        // the assertion below compares them order-independently). The reader stops once it has received this
        // many events, so we don't need to cancel based on the server's output, which races with delivery.
        string[] expectedStatusEvents =
        [
            $"type=build_complete, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=building, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=hot_reload_applied, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=process_started, projects=[{serviceProjectA}]",
            $"type=process_started, projects=[{serviceProjectA}]",
            $"type=process_started, projects=[{serviceProjectB}]",
            $"type=restarting, projects=[{serviceProjectA}]",
        ];

        var statusReaderTask = PipeUtilities.ReadStatusEventsAsync(statusPipeName, expectedStatusEvents.Length, TestContext.CancellationToken);

        server.Start(testAsset,
        [
            "--status-pipe", statusPipeName,
            "--control-pipe", controlPipeName,
            "--resource", serviceProjectA,
            "--resource", serviceProjectB,
        ]);

        // Wait until running projects for the services are initialized,
        // so that we can deterministically test updates to their source code:
        await server.WaitUntilOutputContains(MessageDescriptor.Capabilities, $"A ({tfm})");
        await server.WaitUntilOutputContains(MessageDescriptor.Capabilities, $"B ({tfm})");

        await server.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);

        // valid code change:
        UpdateSourceFile(libSource, """
            using System;

            public class Lib
            {
                public static void Common()
                {
                    Console.WriteLine("<Updated Lib>");
                }
            }
            """);

        await server.WaitUntilOutputContains(MessageDescriptor.ManagedCodeChangesApplied);
        await serviceA.WaitUntilOutputContains("<Updated Lib>");
        await serviceB.WaitUntilOutputContains("<Updated Lib>");

        server.Process.ClearOutput();
        serviceA.Process.ClearOutput();
        serviceB.Process.ClearOutput();

        using var controlPipe = new NamedPipeServerStream(controlPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await controlPipe.WaitForConnectionAsync(TestContext.CancellationToken);
        using var controlPipeWriter = new StreamWriter(controlPipe) { AutoFlush = true };

        // restart resource process:
        await controlPipeWriter.WriteLineAsync(JsonSerializer.Serialize(new WatchControlCommand()
        {
            Type = WatchControlCommand.Types.Rebuild,
            Projects = [serviceProjectA],
        }));

        await server.WaitUntilOutputContains("Received request to restart projects");
        await server.WaitUntilOutputContains(MessageDescriptor.ProjectRestarting, $"A ({tfm})");
        await server.WaitUntilOutputContains(MessageDescriptor.ProjectRestarted, $"A ({tfm})");

        // initial updates are applied when the process restarts:
        await server.WaitUntilOutputContains(MessageDescriptor.SendingUpdateBatch.GetMessage(0), $"A ({tfm})");

        // The reader completes once all expected status events have been received (or the test times out),
        // so there is no race between cancellation and status delivery.
        var statusEvents = await statusReaderTask;

        // validate that we received the expected status events from the server, ignoring the order
        // (both sequences are sorted so the comparison does not depend on event arrival order):
        AssertEx.SequenceEqual(
            expectedStatusEvents.Order(),
            statusEvents.Select(e => $"type={e.Type}, projects=[{string.Join(";", e.Projects.Order())}]").Order());
    }
}
