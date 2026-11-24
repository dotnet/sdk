// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class TelemetryDrainE2ETests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan s_storageRetryTimeout = TimeSpan.FromMinutes(4.5);

    [TestMethod]
    public async Task DrainMode_UploadsAndDeletesSdkPersistedTelemetry()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        environment.SeedSdkStoredTelemetry();
        environment.TelemetryBlobPaths.Should().NotBeEmpty(
            "the SDK trace-exporter partition must contain data before the dotnetup drainer starts");
        using var drainer = environment.StartDrainer();
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestAsync(s_timeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        drainer.HasExited.Should().BeFalse("the child retains its bounded lifetime after the first upload");
        environment.TelemetryBlobPaths.Should().BeEmpty("accepted telemetry blobs should be deleted");
        server.Payloads.Should().Contain(payload => payload.Contains("dotnetup/drain-fixture", StringComparison.Ordinal));
    }

    [TestMethod]
    [Timeout(360_000, CooperativeCancellation = true)]
    public async Task DrainMode_UploadsAndDeletesDotnetupPersistedTelemetry()
    {
        using var server = new MockTelemetryIngestionServer();
        server.ResponseStatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        (int fixtureExitCode, string fixtureOutput) = environment.RunWithoutDetachedDrainer(["--help"]);
        fixtureExitCode.Should().Be(0, fixtureOutput);
        await server.WaitForRequestAsync(s_timeout);
        environment.TelemetryBlobPaths.Should().NotBeEmpty(
            "the real persistent exporter should write the telemetry fixture");
        environment.TelemetryBlobPaths.Select(File.ReadAllText).Should().Contain(
            blob => blob.Contains("dotnetup.access_mode", StringComparison.Ordinal),
            "persisted records must carry the producing process's access mode because the drainer uploads them verbatim");
        server.ResponseStatusCode = System.Net.HttpStatusCode.OK;
        using var drainer = environment.StartDrainer();
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestCountAsync(2, s_storageRetryTimeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        server.Payloads.Should().Contain(
            payload => payload.Contains("dotnetup.access_mode", StringComparison.Ordinal),
            "the drain command should upload the telemetry persisted by the fixture process");
        environment.TelemetryBlobPaths.Should().BeEmpty("accepted telemetry blobs should be deleted");
        drainer.HasExited.Should().BeFalse("the drainer remains available for the retry after the shutdown-created lease expires");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NormalMode_SpawnsDetachedDrainerThatUploadsTelemetry(bool perfTrace)
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        environment.EnvironmentVariables[Constants.Telemetry.EnablePerfTraceEnvVar] = perfTrace ? "1" : "0";

        using var foreground = environment.StartCommand("--help");
        foreground.WaitForExit(10_000).Should().BeTrue("the foreground must not wait for the three-minute child");
        foreground.ExitCode.Should().Be(0);
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestAsync(s_timeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        environment.TelemetryBlobPaths.Should().BeEmpty("the detached child should delete accepted blobs");
        await environment.WaitForCommandOutputClosedAsync(TimeSpan.FromSeconds(5));
        server.Payloads.Should().Contain(payload => payload.Contains("MessageData", StringComparison.Ordinal)
            && payload.Contains("dotnetup/root", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CiMode_UploadsCompletionLogsWithoutSpawningDrainer()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);
        environment.EnvironmentVariables[Constants.Telemetry.ForceLocalDeliveryEnvVar] = "0";
        environment.EnvironmentVariables[Constants.Telemetry.ShutdownTimeoutOverrideEnvVar] = "20000";
        environment.EnvironmentVariables["CI"] = "true";
        environment.ConfigureShutdownBudgetObservation();

        var (exitCode, output) = environment.RunDotnetup(["--help"]);

        exitCode.Should().Be(0, output);
        await server.WaitForRequestAsync(s_timeout);
        server.Payloads.Should().Contain(payload => payload.Contains("dotnetup/root", StringComparison.Ordinal));
        File.Exists(environment.DrainerProcessPath).Should().BeFalse();
        File.ReadAllText(environment.ShutdownBudgetPath).Should().Be("ShutdownBudgetMs=20000");
    }

    [TestMethod]
    public void EnvScript_DoesNotRecordTelemetry()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);
        environment.ConfigureShutdownBudgetObservation();

        (int exitCode, string output) = environment.RunEnvScript();

        exitCode.Should().Be(0, output);
        output.Should().NotBeEmpty("env script should emit the requested PowerShell environment script");
        environment.TelemetryBlobPaths.Should().BeEmpty("env script runs on every shell start and skips telemetry");
        File.Exists(environment.ShutdownBudgetPath).Should().BeFalse("telemetry should not be set up or flushed for env script");
        File.Exists(environment.DrainerProcessPath).Should().BeFalse();
    }

    [TestMethod]
    public async Task FailedCommand_PreservesFailureBudgetAndEmitsCompletion()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);
        environment.ConfigureShutdownBudgetObservation();
        using var foreground = environment.StartCommand("--not-a-command");

        foreground.WaitForExit(10_000).Should().BeTrue();
        foreground.ExitCode.Should().NotBe(0);
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestAsync(s_timeout);
        File.ReadAllText(environment.ShutdownBudgetPath).Should().Be("ShutdownBudgetMs=400");
        server.Payloads.Should().Contain(payload => payload.Contains("dotnetup/root", StringComparison.Ordinal)
            && payload.Contains("error.type", StringComparison.Ordinal));
        await environment.WaitForCommandOutputClosedAsync(TimeSpan.FromSeconds(5));
    }
}
