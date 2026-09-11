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
    }

    [TestMethod]
    public async Task NormalMode_SpawnsDetachedDrainerThatUploadsTelemetry()
    {
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        using var foreground = environment.StartCommand("--help");
        foreground.WaitForExit(10_000).Should().BeTrue("the foreground must not wait for the three-minute child");
        foreground.ExitCode.Should().Be(0);
        await environment.WaitForDrainerStartedAsync(s_timeout);
        await server.WaitForRequestAsync(s_timeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        environment.TelemetryBlobPaths.Should().BeEmpty("the detached child should delete accepted blobs");
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
    }
}