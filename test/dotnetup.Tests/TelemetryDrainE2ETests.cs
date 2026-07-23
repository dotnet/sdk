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
    public async Task NativeAotDrainMode_UploadsAndDeletesPersistedTelemetry()
    {
        DotnetupTestUtilities.GetNativeDotnetupExecutablePath();
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        (int fixtureExitCode, string fixtureOutput) = environment.RunEnvScript();
        fixtureExitCode.Should().Be(0, fixtureOutput);
        environment.TelemetryBlobPaths.Should().NotBeEmpty(
            "the real persistent exporter should write the telemetry fixture");
        environment.EnvironmentVariables[Constants.Telemetry.DrainModeEnvVar] = "1";

        (int exitCode, string output) = environment.RunDotnetup([]);

        exitCode.Should().Be(0, output);
        await server.WaitForRequestAsync(s_timeout);
        environment.TelemetryBlobPaths.Should().BeEmpty("accepted telemetry blobs should be deleted");
    }

    [TestMethod]
    public async Task NativeAotNormalMode_SpawnsDetachedDrainerThatUploadsTelemetry()
    {
        DotnetupTestUtilities.GetNativeDotnetupExecutablePath();
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);

        (int exitCode, string output) = environment.RunDotnetup(["--help"]);

        exitCode.Should().Be(0, output);
        await server.WaitForRequestAsync(s_timeout);
        await environment.WaitForTelemetryBlobsDeletedAsync(s_timeout);
        environment.TelemetryBlobPaths.Should().BeEmpty("the detached child should delete accepted blobs");
    }

    [TestMethod]
    public void NativeAotEnvScript_UsesShellStartupShutdownBudget()
    {
        DotnetupTestUtilities.GetNativeDotnetupExecutablePath();
        using var server = new MockTelemetryIngestionServer();
        using var environment = new TelemetryTestEnvironment(server.IngestionEndpoint);
        environment.ConfigureShutdownBudgetObservation();

        (int exitCode, string output) = environment.RunEnvScript();

        exitCode.Should().Be(0, output);
        output.Should().NotBeEmpty("env script should emit the requested PowerShell environment script");
        File.ReadAllText(environment.ShutdownBudgetPath).Should().Be("ShutdownBudgetMs=10");
    }
}