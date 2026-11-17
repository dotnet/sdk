// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class TelemetryClientTests : SdkTest
{
#if MICROSOFT_ENABLE_TELEMETRY_AZURE_MONITOR
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(127, false)]
    [DataRow(-1, false)]
    [DataRow(1, true)]
    [DoNotParallelize]
    public void ShutdownUsesExitCodeAndRestoresDrainSetting(int exitCode, bool throwOnShutdown)
    {
        const string drainBudgetName = "Azure.Monitor.OpenTelemetry.Exporter.ShutdownDrainBudgetMilliseconds";
        FieldInfo tracerField = typeof(TelemetryClient).GetField("s_tracerProvider", BindingFlags.Static | BindingFlags.NonPublic)!;
        FieldInfo meterField = typeof(TelemetryClient).GetField("s_metricsProvider", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool boundedExport = (bool)typeof(TelemetryClient).GetField("s_isCIEnvironment", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!
            || (bool)typeof(TelemetryClient).GetField("s_enableOtlpExporter", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        int boundedTimeout = (int)typeof(TelemetryClient).GetField("s_shutdownTimeoutMs", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object? previousTracer = tracerField.GetValue(null);
        object? previousMeter = meterField.GetValue(null);
        object? previousBudget = AppContext.GetData(drainBudgetName);
        var processor = new ShutdownRecordingProcessor(throwOnShutdown);
        using var provider = Sdk.CreateTracerProviderBuilder().AddProcessor(processor).Build();
        try
        {
            tracerField.SetValue(null, provider);
            meterField.SetValue(null, null);
            AppContext.SetData(drainBudgetName, 17);
            Action shutdown = () => TelemetryClient.FlushProviders(exitCode);
            shutdown.Should().NotThrow();
            processor.Timeout.Should().Be(boundedExport ? boundedTimeout : exitCode == 0 ? Timeout.Infinite : 300);
            processor.DrainBudget.Should().Be(boundedExport || exitCode == 0 ? 17 : 300);
            AppContext.GetData(drainBudgetName).Should().Be(17);
        }
        finally
        {
            tracerField.SetValue(null, previousTracer);
            meterField.SetValue(null, previousMeter);
            AppContext.SetData(drainBudgetName, previousBudget);
        }
    }

    private sealed class ShutdownRecordingProcessor(bool throwOnShutdown) : BaseProcessor<Activity>
    {
        internal int? Timeout { get; private set; }
        internal object? DrainBudget { get; private set; }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            Timeout = timeoutMilliseconds;
            DrainBudget = AppContext.GetData("Azure.Monitor.OpenTelemetry.Exporter.ShutdownDrainBudgetMilliseconds");
            if (throwOnShutdown)
            {
                throw new InvalidOperationException("Test shutdown failure");
            }
            return true;
        }
    }
#endif

    [TestMethod]
    [DataRow(null, 5_000)]
    [DataRow("", 5_000)]
    [DataRow("invalid", 5_000)]
    [DataRow("0", 5_000)]
    [DataRow("-1", 5_000)]
    [DataRow("100", 100)]
    [DataRow("5000", 5_000)]
    [DataRow("20000", 20_000)]
    [DataRow("2147483647", int.MaxValue)]
    [DataRow("2147483648", 5_000)]
    public void ShutdownTimeoutUsesPositiveOverrideOrFiveSecondDefault(string? value, int expected)
    {
        TelemetryClient.GetShutdownTimeoutMs(value).Should().Be(expected);
    }

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
