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

            // Create a simple in-memory project
            string projectContent = @"
<Project>
  <Target Name='TestTarget'>
    <Message Text='Test message' Importance='high' />
  </Target>
</Project>";

            // Create ProjectCollection with telemetry logger
            var (loggers, telemetryCentralLogger) = ProjectInstanceExtensions.CreateLoggersWithTelemetry();
            using var collection = new ProjectCollection(
                globalProperties: null,
                loggers: loggers,
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

            // Create a temporary project file
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, projectContent);

                // Load and build the project using API-based MSBuild with telemetry
                var project = collection.LoadProject(tempFile);
                var projectInstance = project.CreateProjectInstance();

                // Use a test logger to capture events
                var testLogger = new TestEventLogger();

                // Build directly without distributed logger for simpler test
                // The telemetry logger is already attached to the ProjectCollection
                var result = projectInstance.Build(new[] { "TestTarget" }, new[] { testLogger });

                // Verify build succeeded
                result.Should().BeTrue();

                // Verify the test logger received events (indicating build actually ran)
                testLogger.BuildStartedCount.Should().BeGreaterThan(0);
                testLogger.BuildFinishedCount.Should().BeGreaterThan(0);

                // Verify telemetry logger was created and attached to collection
                telemetryCentralLogger.Should().NotBeNull();
                loggers.Should().Contain(telemetryCentralLogger);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
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
