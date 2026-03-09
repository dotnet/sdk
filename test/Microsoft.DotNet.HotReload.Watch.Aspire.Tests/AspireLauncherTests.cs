// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection.Metadata;
using System.Text.Json;
using Elfie.Serialization;
using Xunit.Runners;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireLauncherTests(ITestOutputHelper logger) : WatchSdkTest(logger)
{
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

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.Linux)] // https://github.com/dotnet/sdk/issues/53061
    public async Task Host()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var projectDir = Path.Combine(testAsset.Path, "AppWithDeps");
        var projectPath = Path.Combine(projectDir, "App.WithDeps.csproj");

        await using var host = CreateHostApp();
        host.Start(testAsset, ["--entrypoint", projectPath]);

        await host.WaitUntilOutputContains(MessageDescriptor.WaitingForChanges);
    }

    [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.Linux)] // https://github.com/dotnet/sdk/issues/53061
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

        using var statusCancellationSource = new CancellationTokenSource();
        var statusReaderTask = PipeUtilities.ReadStatusEventsAsync(statusPipeName, statusCancellationSource.Token);

        server.Start(testAsset,
        [
            "--status-pipe", statusPipeName,
            "--control-pipe", controlPipeName,
            "--resource", serviceProjectA,
            "--resource", serviceProjectB,
        ]);

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
        await controlPipe.WaitForConnectionAsync();
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

        statusCancellationSource.Cancel();
        var statusEvents = await statusReaderTask;

        // validate that we received the expected status events from the server, ignoring the order:
        AssertEx.SequenceEqual(
        [
            $"type=build_complete, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=building, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=hot_reload_applied, projects=[{serviceProjectA};{serviceProjectB}]",
            $"type=process_started, projects=[{serviceProjectA}]",
            $"type=process_started, projects=[{serviceProjectA}]",
            $"type=process_started, projects=[{serviceProjectB}]",
            $"type=restarting, projects=[{serviceProjectA}]",
        ], statusEvents.Select(e => $"type={e.Type}, projects=[{string.Join(";", e.Projects.Order())}]").Order());
    }
}
