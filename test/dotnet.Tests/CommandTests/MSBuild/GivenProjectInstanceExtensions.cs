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
    public void CreateTelemetryCentralLogger_WhenTelemetryDisabled_ReturnsNull()
    {
        // Ensure telemetry is disabled
        Telemetry.Telemetry.CurrentSessionId = null;

        var logger = ProjectInstanceExtensions.CreateTelemetryCentralLogger();

        logger.Should().BeNull();
    }

    [Fact]
    public void CreateTelemetryCentralLogger_WhenTelemetryEnabled_ReturnsLogger()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            var logger = ProjectInstanceExtensions.CreateTelemetryCentralLogger();

            logger.Should().NotBeNull();
            logger.Should().BeOfType<Commands.MSBuild.MSBuildLogger>();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }

    [Fact]
    public void CreateTelemetryForwardingLoggerRecords_WhenTelemetryDisabled_ReturnsEmpty()
    {
        // Ensure telemetry is disabled
        Telemetry.Telemetry.CurrentSessionId = null;

        var loggerRecords = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords();

        loggerRecords.Should().BeEmpty();
    }

    [Fact]
    public void CreateTelemetryForwardingLoggerRecords_WhenTelemetryEnabled_ReturnsLoggerRecords()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            var loggerRecords = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords();

            loggerRecords.Should().NotBeEmpty();
            loggerRecords.Should().HaveCount(1);
            // ForwardingLoggerRecord contains the ForwardingLogger and LoggerDescription
            loggerRecords[0].Should().NotBeNull();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }

    [Fact]
    public void BuildWithTelemetry_WhenTelemetryEnabled_CreatesDistributedLogger()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            // CreateTelemetryCentralLogger should return logger when telemetry is enabled
            var centralLogger = ProjectInstanceExtensions.CreateTelemetryCentralLogger();
            centralLogger.Should().NotBeNull();

            // CreateTelemetryForwardingLoggerRecords should return forwarding logger when telemetry is enabled
            var forwardingLoggers = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords();
            forwardingLoggers.Should().NotBeEmpty();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }
}
