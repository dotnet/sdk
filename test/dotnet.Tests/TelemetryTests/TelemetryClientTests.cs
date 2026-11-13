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
    // The build-server shutdown row affects per-user servers that tests throughout this project can use.
    [DoNotParallelize]
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
    // The process-wide MSBuild server can also be used by tests that cannot participate in a resource lock.
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
    // TelemetryClient static state is process-wide and is accessed by code that cannot participate in a resource lock.
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
    // TelemetryClient static state is process-wide and is accessed by code that cannot participate in a resource lock.
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
    // TelemetryClient static state is process-wide and is accessed by code that cannot participate in a resource lock.
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
    // TelemetryClient static state is process-wide and is accessed by code that cannot participate in a resource lock.
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

    [TestMethod]
    [DataRow("11111111-2222-4333-8444-555555555555", "11111111-2222-4333-8444-555555555555")]
    [DataRow("aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012", "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012")]
    [DataRow("11111111-2222-1333-8444-555555555555", "11111111-2222-1333-8444-555555555555")]
    [DataRow("11111111-2222-8333-B444-555555555555", "11111111-2222-8333-B444-555555555555")]
    [DataRow("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    [DataRow(null, "not_present")]
    [DataRow("", "not_present")]
    [DataRow("not_present", "edcc84b8e36c26b675c1b102b0e42f3507eee0afe5fbda7c203e9cbb758390e3")]
    [DataRow("build-investigation-42", "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2")]
    [DataRow("BUILD-INVESTIGATION-42", "8495a77b0853a7baf0eaccc4f2cb4b53bd36872da0f13744527a2f8c4142a30f")]
    [DataRow("11111111-2222-4333-8444-55555555555z", "da8570b51ec8f24bb7194a2999c1fd4f360aeb7f5c00dd67626a8219af4d8cf4")]
    [DataRow("11111111222243338444555555555555", "a57c45f998b7a947797b97e2a2f9f5193c3f3b0d25846743ffe0ce9d41b8d31a")]
    [DataRow("{11111111-2222-4333-8444-555555555555}", "317fed678c8ba3993d5679e3e6f9e8812c05a45b6f18f2436fccb6900ad1c8ed")]
    [DataRow(" 11111111-2222-4333-8444-555555555555", "509590cf9e16ef2e2c18fa3c850252cd38d7a5f933c9b3d9be955a375bc3b424")]
    [DataRow("11111111-2222-4333-8444-555555555555 ", "a55397b892e7d810f57cf4891c72eb2f4be672aa98ce3666cee9dceb683b2cb0")]
    [DataRow("11111111-2222-4333-8444-555555555555\n", "1ea86e0cda2cdfa70440c9d56e86fd20e61b6a51daed6a369d403d1e997da00d")]
    [DataRow("11111111-2222-0333-8444-555555555555", "8b777695ee7f9a53bb76f167e98a835c582ee2da5aeba14055efda55a786eb8d")]
    [DataRow("11111111-2222-9333-8444-555555555555", "b478764bfaad8e44fbec9869e9287cd58ccb738bb299477b8d77935ede317765")]
    [DataRow("11111111-2222-4333-7444-555555555555", "b16908ebd945a172e8201459e14ea359bf33417005810f1b28aef64b7d7e32b5")]
    [DataRow("11111111-2222-4333-c444-555555555555", "d9bb2db9d5fbd352725181efa0c6869e672ad1b9522ed249284b674b5e7c605d")]
    [DataRow("ffffffff-ffff-ffff-ffff-ffffffffffff", "13984ab878f915021670cd855c9399ebc339676b2581855e4efd60c685f67c8e")]
    [DataRow(" ", "36a9e7f1c95b82ffb99743e0c5c4ce95d83c9a430aac59f84ef3cbfab6145068")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "635361c48bb9eab14198e76ea8ab7f1a41685d6ad62aa9146d301d4f17eb0ae0")]
    [DataRow("r\u00e9union-\U0001F527", "4f0d2b876136e17123ad90cae429ad8a4672b8acd187746136640f98da7a515e")]
    // TelemetryClient's initialization and providers are process-wide, beyond the scope of a resource lock.
    [DoNotParallelize]
    public void ItMatchesCopilotSessionNormalizationForEnabledEvents(string? sessionId, string expectedId)
    {
        const string sdkSessionId = "sdk-correlation-test";
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
            .Returns(false);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.COPILOT_AGENT_SESSION_ID))
            .Returns(sessionId);

        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            var telemetry = new TelemetryClient(sdkSessionId, environmentProvider.Object);
            using var activity = new Activity("copilot-correlation").Start();
            telemetry.ThreadBlockingTrackEvent("blocking-correlation", new Dictionary<string, string?>
            {
                ["CopilotAgentSessionId"] = "caller-supplied-value",
            });
            telemetry.TrackEvent("queued-correlation", new Dictionary<string, string?>
            {
                ["CopilotAgentSessionId"] = "caller-supplied-value",
            });
            telemetry.WaitForPendingEvents();

            activity.Events.Select(e => e.Name).Should().Equal(
                "dotnet/cli/blocking-correlation", "dotnet/cli/queued-correlation");
            foreach (ActivityEvent telemetryEvent in activity.Events)
            {
                var tags = telemetryEvent.Tags.ToDictionary();
                tags["SessionId"].Should().Be(sdkSessionId);
                tags["CopilotAgentSessionId"].Should().Be(expectedId);
            }

            TelemetryClient.CurrentSessionId.Should().Be(sdkSessionId);
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }

    [TestMethod]
    // TelemetryClient's initialization and providers are process-wide, beyond the scope of a resource lock.
    [DoNotParallelize]
    public void ItDoesNotReadOrEmitCopilotCorrelationWhenOptedOut()
    {
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
            .Returns(true);

        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            var telemetry = new TelemetryClient("sdk-correlation-test", environmentProvider.Object);
            using var activity = new Activity("opted-out-copilot-correlation").Start();
            telemetry.ThreadBlockingTrackEvent("blocking-correlation", null);
            telemetry.TrackEvent("queued-correlation", null);
            telemetry.WaitForPendingEvents();

            activity.Events.Should().BeEmpty();
            environmentProvider.Verify(
                p => p.GetEnvironmentVariable(EnvironmentVariableNames.COPILOT_AGENT_SESSION_ID),
                Times.Never);
        }
        finally
        {
            TelemetryClient.DisabledForTests = true;
        }
    }

    [TestMethod]
    // TelemetryClient's initialization and providers are process-wide, beyond the scope of a resource lock.
    [DoNotParallelize]
    public void ItSnapshotsCopilotCorrelationBeforeQueueingAndClearsItForLaterRequests()
    {
        const string firstSessionId = "build-investigation-42";
        const string secondSessionId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        string? currentSessionId = firstSessionId;
        var environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, It.IsAny<bool>()))
            .Returns(false);
        environmentProvider
            .Setup(p => p.GetEnvironmentVariable(EnvironmentVariableNames.COPILOT_AGENT_SESSION_ID))
            .Returns(() => currentSessionId);

        var pendingEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryClient? telemetry = null;
        TelemetryClient.DisabledForTests = true;
        TelemetryClient.DisabledForTests = false;

        try
        {
            telemetry = new TelemetryClient("sdk-correlation-test", environmentProvider.Object);
            typeof(TelemetryClient).GetField("_trackEventTask", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(telemetry, pendingEvent.Task);

            using var activity = new Activity("queued-copilot-correlation").Start();
            telemetry.TrackEvent("first-build", null);
            currentSessionId = secondSessionId;
            telemetry.TrackEvent("second-build", null);
            currentSessionId = null;
            telemetry.TrackEvent("manual-build", null);
            currentSessionId = "99999999-8888-4777-8666-555555555555";

            pendingEvent.SetResult();
            telemetry.WaitForPendingEvents();

            ActivityEvent[] events = activity.Events.ToArray();
            events.Select(e => e.Name).Should().Equal(
                "dotnet/cli/first-build", "dotnet/cli/second-build", "dotnet/cli/manual-build");
            events[0].Tags.ToDictionary()["CopilotAgentSessionId"].Should().Be(
                "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2");
            events[1].Tags.ToDictionary()["CopilotAgentSessionId"].Should().Be(secondSessionId);
            events[2].Tags.ToDictionary()["CopilotAgentSessionId"].Should().Be("not_present");
        }
        finally
        {
            pendingEvent.TrySetResult();
            telemetry?.WaitForPendingEvents();
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
