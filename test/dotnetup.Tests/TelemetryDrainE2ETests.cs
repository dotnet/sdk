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

    [TestMethod]
    public async Task DrainMode_UploadsAndDeletesPersistedTelemetry()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        (int fixtureExitCode, string fixtureOutput) = environment.RunEnvScript();
        fixtureExitCode.Should().Be(0, fixtureOutput);
        environment.SeedStoredTelemetry();
        environment.TelemetryBlobPaths.Should().NotBeEmpty(
            "the fixture must contain data before the detached exporter starts");
        using var drainer = environment.StartDrainer();
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestAsync(s_timeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        drainer.HasExited.Should().BeFalse("the child retains its three-minute lifetime even after the first upload");
        environment.TelemetryBlobPaths.Should().BeEmpty("accepted telemetry blobs should be deleted");
        server.Payloads.Should().Contain(payload => payload.Contains("dotnetup/drain-fixture", StringComparison.Ordinal));
        drainer.WaitForExit(200_000).Should().BeTrue("the detached process must terminate after its bounded lifetime");
        drainer.ExitCode.Should().Be(0);
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
    public void EnvScript_UsesShellStartupShutdownBudget()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);
        environment.ConfigureShutdownBudgetObservation();

        (int exitCode, string output) = environment.RunEnvScript();

        exitCode.Should().Be(0, output);
        output.Should().NotBeEmpty("env script should emit the requested PowerShell environment script");
        File.ReadAllText(environment.ShutdownBudgetPath).Should().Be("ShutdownBudgetMs=10");
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