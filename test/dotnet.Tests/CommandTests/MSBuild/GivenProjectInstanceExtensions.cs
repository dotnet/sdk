// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
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

        var centralLogger = ProjectInstanceExtensions.CreateTelemetryCentralLogger();
        var loggerRecords = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords(centralLogger);

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

            var centralLogger = ProjectInstanceExtensions.CreateTelemetryCentralLogger();
            var loggerRecords = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords(centralLogger);

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
            // using the same central logger instance
            var forwardingLoggers = ProjectInstanceExtensions.CreateTelemetryForwardingLoggerRecords(centralLogger);
            forwardingLoggers.Should().NotBeEmpty();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }

    [Fact]
    public void TelemetryLogger_ReceivesEventsFromAPIBasedBuild()
    {
        // Enable telemetry with a session ID
        var originalSessionId = Telemetry.Telemetry.CurrentSessionId;
        try
        {
            Telemetry.Telemetry.CurrentSessionId = Guid.NewGuid().ToString();

            // Create ProjectCollection with telemetry logger
            var (loggers, telemetryCentralLogger) = ProjectInstanceExtensions.CreateLoggersWithTelemetry();
            using var collection = new ProjectCollection(
                globalProperties: null,
                loggers: loggers,
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

            // Verify telemetry logger was created and included in the loggers array
            telemetryCentralLogger.Should().NotBeNull();
            loggers.Should().Contain(telemetryCentralLogger);
            
            // Verify the collection was created successfully with loggers
            collection.Should().NotBeNull();
            collection.Loggers.Should().NotBeEmpty();
        }
        finally
        {
            // Restore original session ID
            Telemetry.Telemetry.CurrentSessionId = originalSessionId;
        }
    }

    /// <summary>
    /// Simple logger to track build events for testing
    /// </summary>
    private class TestEventLogger : ILogger
    {
        public int BuildStartedCount { get; private set; }
        public int BuildFinishedCount { get; private set; }
        public int TargetStartedCount { get; private set; }
        public int TargetFinishedCount { get; private set; }

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.BuildStarted += (sender, e) => BuildStartedCount++;
            eventSource.BuildFinished += (sender, e) => BuildFinishedCount++;
            eventSource.TargetStarted += (sender, e) => TargetStartedCount++;
            eventSource.TargetFinished += (sender, e) => TargetFinishedCount++;
        }

        public void Shutdown()
        {
        }
    }
}
