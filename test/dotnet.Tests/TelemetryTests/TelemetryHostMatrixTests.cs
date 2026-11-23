// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
// The matrix starts and stops per-user build servers that tests throughout this project can use.
[DoNotParallelize]
public class TelemetryHostMatrixTests : SdkTest
{
    [TestMethod]
    [DataRow(TelemetryHost.MSBuildServerCold)]
    [DataRow(TelemetryHost.MSBuildServerHot)]
    [DataRow(TelemetryHost.InProcess)]
    [DataRow(TelemetryHost.ServerFallback)]
    [DataRow(TelemetryHost.OptOut)]
    public async Task BuildTelemetryIsExportedForEachHost(TelemetryHost host)
    {
        const string copilotSessionId = "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012";
        var testAsset = TestAssetsManager
            .CopyTestAsset("HelloWorld", identifier: host.ToString())
            .WithSource();

        new DotnetCommand(Log, "restore", testAsset.Path)
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .Execute()
            .Should()
            .Pass();

        ShutdownBuildServers();
        await using var collector = await TelemetryCollectorFixture.CreateAsync(TestContext.CancellationToken);

        try
        {
            if (host == TelemetryHost.MSBuildServerHot)
            {
                const string firstCopilotSessionId = "build-investigation-42";
                RunBuild(testAsset.Path, collector, host, firstCopilotSessionId).Should().Pass();
                IReadOnlyList<CollectedEvent> warmupEvents = await AssertBuildEventsAsync(
                    collector, expectedBuildEventCount: 1, expectedServerState: "cold");
                warmupEvents.Single(e => e.Name == "dotnet/cli/msbuild/build")
                    .Attributes["CopilotAgentSessionId"].Should().Be(
                        "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2");
            }

            RunBuild(testAsset.Path, collector, host, copilotSessionId).Should().Pass();

            if (host == TelemetryHost.OptOut)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
                (await collector.GetEventsAsync(TestContext.CancellationToken)).Should().BeEmpty();
                return;
            }

            int expectedBuildEventCount = host == TelemetryHost.MSBuildServerHot ? 2 : 1;
            string? expectedServerState = host switch
            {
                TelemetryHost.MSBuildServerCold => "cold",
                TelemetryHost.MSBuildServerHot => "hot",
                _ => null,
            };

            IReadOnlyList<CollectedEvent> events = await AssertBuildEventsAsync(
                collector,
                expectedBuildEventCount,
                expectedServerState);

            CollectedEvent buildEvent = events
                .Where(e => e.Name == "dotnet/cli/msbuild/build")
                .Last();
            buildEvent.Attributes["CopilotAgentSessionId"].Should().Be(copilotSessionId);

            if (host == TelemetryHost.ServerFallback)
            {
                buildEvent.Attributes["ServerFallbackReason"].Should().Be("Arguments");
            }
            else
            {
                buildEvent.Attributes.Should().NotContainKey("ServerFallbackReason");
            }

            if (host == TelemetryHost.MSBuildServerHot)
            {
                RunBuild(testAsset.Path, collector, host, copilotSessionId: null).Should().Pass();
                IReadOnlyList<CollectedEvent> manualBuildEvents = await AssertBuildEventsAsync(
                    collector, expectedBuildEventCount: 3, expectedServerState: "hot");
                manualBuildEvents.Last(e => e.Name == "dotnet/cli/msbuild/build")
                    .Attributes["CopilotAgentSessionId"].Should().Be("not_present");
            }
        }
        finally
        {
            ShutdownBuildServers();
        }
    }

    private CommandResult RunBuild(
        string projectPath,
        TelemetryCollectorFixture collector,
        TelemetryHost host,
        string? copilotSessionId)
    {
        var command = new DotnetCommand(
                Log,
                "build",
                projectPath,
                "--no-restore",
                "--no-incremental")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", host == TelemetryHost.OptOut ? "1" : "0")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER", "1")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "1")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS", "15000")
            .WithEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint.ToString())
            .WithEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

        if (copilotSessionId is null)
        {
            command.EnvironmentToRemove.Add("COPILOT_AGENT_SESSION_ID");
        }
        else
        {
            command.WithEnvironmentVariable("COPILOT_AGENT_SESSION_ID", copilotSessionId);
        }

        // The shared test environment disables node reuse to prevent cross-test interference.
        // The MSBuild server requires node reuse, and each matrix row shuts down its own server.
        command.EnvironmentToRemove.Add("MSBUILDDISABLENODEREUSE");
        command.EnvironmentToRemove.Add("DOTNET_CLI_USE_MSBUILD_SERVER");

        if (host == TelemetryHost.InProcess)
        {
            command = command.WithEnvironmentVariable("MSBUILDUSESERVER", "0");
        }
        else
        {
            // Exercise the SDK's default server behavior without inheriting a machine-level
            // opt-in or opt-out from the test host.
            command.EnvironmentToRemove.Add("MSBUILDUSESERVER");
        }

        if (host == TelemetryHost.ServerFallback)
        {
            command.Arguments.Add("-nr:false");
        }

        return command.Execute();
    }

    private async Task<IReadOnlyList<CollectedEvent>> AssertBuildEventsAsync(
        TelemetryCollectorFixture collector,
        int expectedBuildEventCount,
        string? expectedServerState)
    {
        IReadOnlyList<CollectedEvent> events = await collector.WaitForEventsAsync(
            currentEvents =>
                currentEvents.Count(e => e.Name == "dotnet/cli/msbuild/build") >= expectedBuildEventCount
                && currentEvents.Any(e => e.Name == "dotnet/cli/toplevelparser/command")
                && currentEvents.Any(e => e.Name == "dotnet/cli/command/finish"),
            cancellationToken: TestContext.CancellationToken);

        CollectedEvent[] buildEvents = events
            .Where(e => e.Name == "dotnet/cli/msbuild/build")
            .ToArray();
        buildEvents.Should().HaveCount(expectedBuildEventCount);

        CollectedEvent latestBuildEvent = buildEvents[^1];
        latestBuildEvent.Attributes["BuildSuccess"].Should().Be("True");

        if (expectedServerState is null)
        {
            latestBuildEvent.Attributes.Should().NotContainKey("InitialMSBuildServerState");
        }
        else
        {
            latestBuildEvent.Attributes["InitialMSBuildServerState"].Should().Be(expectedServerState);
        }

        return events;
    }

    private void ShutdownBuildServers()
    {
        // Every row shuts down the per-user server before and after its trial so the cold/hot state belongs to this test.
        new DotnetCommand(Log, "build-server", "shutdown")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .Execute()
            .Should()
            .Pass();
    }

    public enum TelemetryHost
    {
        MSBuildServerCold,
        MSBuildServerHot,
        InProcess,
        ServerFallback,
        OptOut,
    }
}
