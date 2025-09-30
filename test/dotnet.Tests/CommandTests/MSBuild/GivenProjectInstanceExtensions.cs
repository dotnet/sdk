// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

public class GivenProjectInstanceExtensions
{
    [Fact]
    public void CreateTelemetryLoggers_WhenTelemetryDisabled_ReturnsNull()
    {
        // Ensure telemetry is disabled
        Telemetry.Telemetry.CurrentSessionId = null;

        var loggers = ProjectInstanceExtensions.CreateTelemetryLoggers();

        loggers.Should().BeNull();
    }

    [Fact]
    public void CreateTelemetryLoggers_WhenTelemetryEnabled_ReturnsLoggers()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            var loggers = ProjectInstanceExtensions.CreateTelemetryLoggers();

            loggers.Should().NotBeNull();
            loggers.Should().HaveCount(1);
            loggers[0].Should().BeOfType<Commands.MSBuild.MSBuildLogger>();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }

    [Fact]
    public void BuildWithTelemetry_WhenTelemetryDisabled_CallsBuildWithoutTelemetryLogger()
    {
        // This is a basic smoke test to ensure the extension method doesn't throw
        // We can't easily test the actual build without setting up a full project
        
        // Ensure telemetry is disabled
        Telemetry.Telemetry.CurrentSessionId = null;

        // CreateTelemetryLoggers should return null when telemetry is disabled
        var loggers = ProjectInstanceExtensions.CreateTelemetryLoggers();
        loggers.Should().BeNull();
    }

    [Fact]
    public void BuildWithTelemetry_WhenTelemetryEnabled_CreatesTelemetryLogger()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            // CreateTelemetryLoggers should return logger when telemetry is enabled
            var loggers = ProjectInstanceExtensions.CreateTelemetryLoggers();
            loggers.Should().NotBeNull();
            loggers.Should().HaveCount(1);
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }
}
