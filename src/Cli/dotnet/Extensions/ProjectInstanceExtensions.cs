// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.MSBuild;

namespace Microsoft.DotNet.Cli.Extensions;

public static class ProjectInstanceExtensions
{
    public static string GetProjectId(this ProjectInstance projectInstance)
    {
        var projectGuidProperty = projectInstance.GetPropertyValue("ProjectGuid");
        var projectGuid = string.IsNullOrEmpty(projectGuidProperty)
            ? Guid.NewGuid()
            : new Guid(projectGuidProperty);
        return projectGuid.ToString("B").ToUpper();
    }

    public static string GetDefaultProjectTypeGuid(this ProjectInstance projectInstance)
    {
        string projectTypeGuid = projectInstance.GetPropertyValue("DefaultProjectTypeGuid");
        if (string.IsNullOrEmpty(projectTypeGuid) && projectInstance.FullPath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase))
        {
            projectTypeGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";
        }
        return projectTypeGuid;
    }

    public static IEnumerable<string> GetPlatforms(this ProjectInstance projectInstance)
    {
        return (projectInstance.GetPropertyValue("Platforms") ?? "")
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .DefaultIfEmpty("AnyCPU");
    }

    public static IEnumerable<string> GetConfigurations(this ProjectInstance projectInstance)
    {
        string foundConfig = projectInstance.GetPropertyValue("Configurations") ?? "Debug;Release";
        if (string.IsNullOrWhiteSpace(foundConfig))
        {
            foundConfig = "Debug;Release";
        }

        return foundConfig
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .DefaultIfEmpty("Debug");
    }

    /// <summary>
    /// Creates the central telemetry logger for API-based MSBuild usage if telemetry is enabled.
    /// This logger should be used for evaluation (ProjectCollection) and as a central logger for builds.
    /// Returns null if telemetry is not enabled or if there's an error creating the logger.
    /// </summary>
    /// <returns>The central telemetry logger, or null if telemetry is disabled.</returns>
    public static ILogger? CreateTelemetryCentralLogger()
    {
        if (Telemetry.Telemetry.CurrentSessionId != null)
        {
            try
            {
                return new MSBuildLogger();
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }
        return null;
    }

    /// <summary>
    /// Creates the forwarding logger record for distributed builds if telemetry is enabled.
    /// This should be used with the remoteLoggers parameter of ProjectInstance.Build.
    /// Returns an empty collection if telemetry is not enabled or if there's an error creating the logger.
    /// </summary>
    /// <returns>An array containing the forwarding logger record, or empty array if telemetry is disabled.</returns>
    public static ForwardingLoggerRecord[] CreateTelemetryForwardingLoggerRecords()
    {
        if (Telemetry.Telemetry.CurrentSessionId != null)
        {
            try
            {
                var forwardingLogger = new MSBuildForwardingLogger();
                var loggerRecord = new ForwardingLoggerRecord(forwardingLogger, new Microsoft.Build.Logging.LoggerDescription(
                    loggerClassName: typeof(MSBuildLogger).FullName!,
                    loggerAssemblyName: typeof(MSBuildLogger).Assembly.Location,
                    loggerAssemblyFile: null,
                    loggerSwitchParameters: null,
                    verbosity: LoggerVerbosity.Normal));
                return [loggerRecord];
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }
        return [];
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? additionalLoggers = null)
    {
        var loggers = new List<ILogger>();
        var forwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add central telemetry logger
        var telemetryCentralLogger = CreateTelemetryCentralLogger();
        if (telemetryCentralLogger != null)
        {
            loggers.Add(telemetryCentralLogger);

            // Add forwarding logger for distributed builds
            forwardingLoggers.AddRange(CreateTelemetryForwardingLoggerRecords());
        }

        if (additionalLoggers != null)
        {
            loggers.AddRange(additionalLoggers);
        }

        // Use the overload that accepts forwarding loggers for proper distributed logging
        return projectInstance.Build(
            targets,
            loggers.Count > 0 ? loggers : null,
            forwardingLoggers.Count > 0 ? forwardingLoggers : null,
            out _);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var allLoggers = new List<ILogger>();
        var forwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add central telemetry logger
        var telemetryCentralLogger = CreateTelemetryCentralLogger();
        if (telemetryCentralLogger != null)
        {
            allLoggers.Add(telemetryCentralLogger);

            // Add forwarding logger for distributed builds
            forwardingLoggers.AddRange(CreateTelemetryForwardingLoggerRecords());
        }

        if (loggers != null)
        {
            allLoggers.AddRange(loggers);
        }

        // Use the overload that accepts forwarding loggers for proper distributed logging
        return projectInstance.Build(
            targets,
            allLoggers.Count > 0 ? allLoggers : null,
            forwardingLoggers.Count > 0 ? forwardingLoggers : null,
            out targetOutputs);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        IEnumerable<ForwardingLoggerRecord>? remoteLoggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var allLoggers = new List<ILogger>();
        var allForwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add central telemetry logger
        var telemetryCentralLogger = CreateTelemetryCentralLogger();
        if (telemetryCentralLogger != null)
        {
            allLoggers.Add(telemetryCentralLogger);

            // Add forwarding logger for distributed builds
            allForwardingLoggers.AddRange(CreateTelemetryForwardingLoggerRecords());
        }

        if (loggers != null)
        {
            allLoggers.AddRange(loggers);
        }

        if (remoteLoggers != null)
        {
            allForwardingLoggers.AddRange(remoteLoggers);
        }

        return projectInstance.Build(
            targets,
            allLoggers.Count > 0 ? allLoggers : null,
            allForwardingLoggers.Count > 0 ? allForwardingLoggers : null,
            out targetOutputs);
    }

    /// <summary>
    /// Creates a logger collection that includes the telemetry central logger.
    /// This is useful for ProjectCollection scenarios where evaluation needs telemetry.
    /// </summary>
    /// <param name="additionalLoggers">Additional loggers to include in the collection.</param>
    /// <returns>An array of loggers including telemetry logger if enabled, or null if no loggers.</returns>
    public static ILogger[]? CreateLoggersWithTelemetry(IEnumerable<ILogger>? additionalLoggers = null)
    {
        var loggers = new List<ILogger>();

        // Add central telemetry logger for evaluation
        var telemetryCentralLogger = CreateTelemetryCentralLogger();
        if (telemetryCentralLogger != null)
        {
            loggers.Add(telemetryCentralLogger);
        }

        if (additionalLoggers != null)
        {
            loggers.AddRange(additionalLoggers);
        }

        return loggers.Count > 0 ? loggers.ToArray() : null;
    }
}
