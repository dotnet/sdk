// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Moq;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class TelemetryClientTests : SdkTest
{
    public static IEnumerable<object[]> CommandsWithExitCode =>
    [
        [new[] { "--help" }, "0"],
        [new[] { "--info" }, "0"],
        [new[] { "workload", "list" }, "0"],
        [new[] { "sdk", "check" }, "0"],
        [new[] { "build-server", "shutdown" }, "0"],
        [new[] { "solution", "list" }, "1"],
        [new[] { "clean" }, "1"],
        [new[] { "run" }, "1"],
        [new[] { "new", "details" }, "127"]
    ];

    // Only runs on Windows because OTel libraries are only referenced on Windows builds.
    // Thus, this test that writes telemetry logs will not work on other platforms.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DynamicData(nameof(CommandsWithExitCode))]
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    public void ItProcessesMSBuildTelemetryWithTheServerEnabled()
    {
        var testAsset = TestAssetsManager.CopyTestAsset("HelloWorld")
            .WithSource();
        var logFile = Path.Combine(testAsset.TestRoot, "msbuild-server-telemetry.json");
        File.Delete(logFile);

        ShutdownMSBuildServer(testAsset.TestRoot);

        try
        {
            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.TestRoot)
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "true")
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_LOG_PATH", logFile)
                .WithEnvironmentVariable("MSBUILDUSESERVER", "1")
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.TestRoot)
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "true")
                .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_LOG_PATH", logFile)
                .WithEnvironmentVariable("MSBUILDUSESERVER", "1")
                .Execute()
                .Should()
                .Pass();

            var telemetryJson = JsonNode.Parse(File.ReadAllText(logFile));
            var activities = telemetryJson?["activities"]?.AsArray();
            activities.Should().NotBeNull();

            var msbuildActivities = activities.Where(activity =>
                activity?["events"]?.AsArray()
                    .Any(@event => @event?["name"]?.GetValue<string>().StartsWith("dotnet/cli/msbuild/") == true) == true)
                .ToArray();

            var msbuildTraceIds = msbuildActivities
                .Select(activity => activity?["identifiers"]?["traceId"]?.GetValue<string>())
                .Distinct();
            msbuildTraceIds.Should().HaveCount(2);

            var invocationTraceIds = activities
                .Where(activity => activity?["operationName"]?.GetValue<string>() == "invocation")
                .Select(activity => activity?["identifiers"]?["traceId"]?.GetValue<string>())
                .ToHashSet();
            var activityContexts = activities
                .Select(activity => (
                    traceId: activity?["identifiers"]?["traceId"]?.GetValue<string>(),
                    spanId: activity?["identifiers"]?["spanId"]?.GetValue<string>()))
                .ToHashSet();

            var msbuildParentContexts = msbuildActivities
                .Select(activity => (
                    traceId: activity?["identifiers"]?["traceId"]?.GetValue<string>(),
                    spanId: activity?["identifiers"]?["parentSpanId"]?.GetValue<string>()))
                .ToArray();
            msbuildParentContexts.Should().OnlyContain(
                context => invocationTraceIds.Contains(context.traceId) && activityContexts.Contains(context));
        }
        finally
        {
            ShutdownMSBuildServer(testAsset.TestRoot);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void DisabledForTestsDoesNotInitializeTelemetry()
    {
        TelemetryClient.DisabledForTests = true;

        try
        {
            _ = new TelemetryClient();
            TelemetryClient.DisabledForTests = false;

            TelemetryClient.IsInitialized.Should().BeFalse();
            TelemetryClient.Instance.Should().BeNull();
            TelemetryClient.CurrentSessionId.Should().BeNull();
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void MSBuildLoggerDoesNotReinitializeDisabledTelemetry()
    {
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
                .Returns(true);

            var telemetry = new TelemetryClient(sessionId: null, environmentProvider: environmentProvider.Object);
            _ = new MSBuildLogger();

            telemetry.Enabled.Should().BeFalse();
            TelemetryClient.IsInitialized.Should().BeTrue();
            TelemetryClient.Instance.Should().BeSameAs(telemetry);
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }

    [TestMethod]
    [DoNotParallelize]
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

    [TestMethod]
    [DoNotParallelize]
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

    private void ShutdownMSBuildServer(string workingDirectory)
    {
        new BuildServerCommand(Log)
            .WithWorkingDirectory(workingDirectory)
            .Execute("shutdown", "--msbuild")
            .Should()
            .Pass();
    }
}
