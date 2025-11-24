// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.MSBuildEvaluation;

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
    [Obsolete("Use DotNetProjectEvaluatorFactory.CreateForCommand() or DotNetProjectEvaluator constructor instead. This method will be removed in a future release.")]
    public static ILogger? CreateTelemetryCentralLogger()
    {
        return TelemetryUtilities.CreateTelemetryCentralLogger();
    }

    /// <summary>
    /// Creates the forwarding logger record for distributed builds using the provided central logger.
    /// This should be used with the remoteLoggers parameter of ProjectInstance.Build.
    /// The same central logger instance from ProjectCollection should be reused here.
    /// Returns an empty collection if the central logger is null or if there's an error.
    /// </summary>
    /// <param name="centralLogger">The central logger instance (typically the same one used in ProjectCollection).</param>
    /// <returns>An array containing the forwarding logger record, or empty array if central logger is null.</returns>
    [Obsolete("Use DotNetProjectBuilder for build operations instead. This method will be removed in a future release.")]
    public static ForwardingLoggerRecord[] CreateTelemetryForwardingLoggerRecords(ILogger? centralLogger)
    {
        return TelemetryUtilities.CreateTelemetryForwardingLoggerRecords(centralLogger);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="projectInstance">The project instance to build.</param>
    /// <param name="targets">The targets to build.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <param name="telemetryCentralLogger">Optional telemetry central logger from ProjectCollection. If null, creates a new one.</param>
    [Obsolete("Use DotNetProjectBuilder.Build() instead. This method will be removed in a future release.")]
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? additionalLoggers = null,
        ILogger? telemetryCentralLogger = null)
    {
        var loggers = new List<ILogger>();
        var forwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add telemetry as a distributed logger via ForwardingLoggerRecord
        // Use provided central logger or create a new one
        var centralLogger = telemetryCentralLogger ?? TelemetryUtilities.CreateTelemetryCentralLogger();
        forwardingLoggers.AddRange(TelemetryUtilities.CreateTelemetryForwardingLoggerRecords(centralLogger));

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
    /// <param name="projectInstance">The project instance to build.</param>
    /// <param name="targets">The targets to build.</param>
    /// <param name="loggers">Loggers to include.</param>
    /// <param name="targetOutputs">The outputs from the build.</param>
    /// <param name="telemetryCentralLogger">Optional telemetry central logger from ProjectCollection. If null, creates a new one.</param>
    [Obsolete("Use DotNetProjectBuilder.Build() instead. This method will be removed in a future release.")]
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        out IDictionary<string, TargetResult> targetOutputs,
        ILogger? telemetryCentralLogger = null)
    {
        var allLoggers = new List<ILogger>();
        var forwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add telemetry as a distributed logger via ForwardingLoggerRecord
        // Use provided central logger or create a new one
        var centralLogger = telemetryCentralLogger ?? TelemetryUtilities.CreateTelemetryCentralLogger();
        forwardingLoggers.AddRange(TelemetryUtilities.CreateTelemetryForwardingLoggerRecords(centralLogger));

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
    /// <param name="projectInstance">The project instance to build.</param>
    /// <param name="targets">The targets to build.</param>
    /// <param name="loggers">Loggers to include.</param>
    /// <param name="remoteLoggers">Remote/forwarding loggers to include.</param>
    /// <param name="targetOutputs">The outputs from the build.</param>
    /// <param name="telemetryCentralLogger">Optional telemetry central logger from ProjectCollection. If null, creates a new one.</param>
    [Obsolete("Use DotNetProjectBuilder.Build() instead. This method will be removed in a future release.")]
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        IEnumerable<ForwardingLoggerRecord>? remoteLoggers,
        out IDictionary<string, TargetResult> targetOutputs,
        ILogger? telemetryCentralLogger = null)
    {
        var allLoggers = new List<ILogger>();
        var allForwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add telemetry as a distributed logger via ForwardingLoggerRecord
        // Use provided central logger or create a new one
        var centralLogger = telemetryCentralLogger ?? TelemetryUtilities.CreateTelemetryCentralLogger();
        allForwardingLoggers.AddRange(TelemetryUtilities.CreateTelemetryForwardingLoggerRecords(centralLogger));

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
    /// Returns both the logger array and the telemetry central logger instance for reuse in subsequent builds.
    /// </summary>
    /// <param name="additionalLoggers">Additional loggers to include in the collection.</param>
    /// <returns>A tuple containing the logger array and the telemetry central logger (or null if no telemetry).</returns>
    [Obsolete("Use DotNetProjectEvaluatorFactory.CreateForCommand() or DotNetProjectEvaluator constructor instead. This method will be removed in a future release.")]
    public static (ILogger[]? loggers, ILogger? telemetryCentralLogger) CreateLoggersWithTelemetry(IEnumerable<ILogger>? additionalLoggers = null)
    {
        return TelemetryUtilities.CreateLoggersWithTelemetry(additionalLoggers);
    }
}
