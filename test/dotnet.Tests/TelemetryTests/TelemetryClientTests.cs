// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[Collection(TestConstants.UsesStaticTelemetryState)]
public class TelemetryClientTests(ITestOutputHelper log) : SdkTest(log)
{
    public static TheoryData<string[], string> CommandsWithExitCode => new()
    {
        { new[] { "--help" }, "0" },
        { new[] { "--info" }, "0" },
        { new[] { "workload", "list" }, "0" },
        { new[] { "sdk", "check" }, "0" },
        { new[] { "build-server", "shutdown" }, "0" },
        { new[] { "solution", "list" }, "1" },
        { new[] { "clean" }, "1" },
        { new[] { "run" }, "1" },
        { new[] { "new", "details" }, "127" }
    };

    // Only runs on Windows because OTel libraries are only referenced on Windows builds.
    // Thus, this test that writes telemetry logs will not work on other platforms.
    [PlatformSpecificTheory(TestPlatforms.Windows)]
    [MemberData(nameof(CommandsWithExitCode))]
    public void ItProcessesTelemetryData(string[] commandArgs, string exitCodeExpected)
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;
        var commandString = string.Join(' ', commandArgs);
        var logFile = Path.Combine(testDir, $"TelemLog_{commandString}.json");

        new DotnetCommand(Log, commandArgs)
            .WithWorkingDirectory(testDir)
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "true")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_LOG_PATH", logFile)
            .Execute();

        var logFileInfo = new FileInfo(logFile);
        logFileInfo.Should().Exist();

        var telemetryJson = JsonNode.Parse(logFileInfo.ReadAllText());
        telemetryJson.Should().NotBeNull();

        var activities = telemetryJson["activities"]?.AsArray();
        activities.Should().NotBeNull();

        var mainOperation = activities.FirstOrDefault(n => n?["operationName"]?.GetValue<string>() == "main");
        mainOperation.Should().NotBeNull();

        var displayName = mainOperation["displayName"]?.GetValue<string>();
        displayName.Should().Be($"dotnet {commandString}");

        var events = mainOperation["events"]?.AsArray();
        events.Should().NotBeNull();

        var finishEvent = events.FirstOrDefault(n => n?["name"]?.GetValue<string>() == "dotnet/cli/command/finish");
        finishEvent.Should().NotBeNull();

        var tags = finishEvent["tags"];
        tags.Should().NotBeNull();

        var exitCode = tags["exitCode"]?.GetValue<string>();
        exitCode.Should().Be(exitCodeExpected);
    }

    [Fact]
    public void ItSeedsCurrentSessionIdFromEnvironmentWhenSessionIdIsNotProvided()
    {
        const string sessionId = "gha-12345-1";
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
                .Returns(false);
            environmentProvider
                .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID))
                .Returns(sessionId);

            var telemetry = new TelemetryClient(sessionId: null, environmentProvider: environmentProvider.Object);

            telemetry.Enabled.Should().BeTrue();
            TelemetryClient.CurrentSessionId.Should().Be(sessionId);
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }

    [Fact]
    public void ItPrefersExplicitSessionIdOverEnvironmentSeed()
    {
        const string sessionId = "explicit-session";
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
                .Returns(false);

            var telemetry = new TelemetryClient(sessionId, environmentProvider: environmentProvider.Object);

            telemetry.Enabled.Should().BeTrue();
            TelemetryClient.CurrentSessionId.Should().Be(sessionId);
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }
}
