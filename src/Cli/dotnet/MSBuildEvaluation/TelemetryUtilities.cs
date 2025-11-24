// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.MSBuild;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Internal utility class for creating and managing telemetry loggers for MSBuild operations.
/// This class consolidates the telemetry logic that was previously in ProjectInstanceExtensions.
/// </summary>
internal static class TelemetryUtilities
{
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
    /// Creates the forwarding logger record for distributed builds using the provided central logger.
    /// This should be used with the remoteLoggers parameter of ProjectInstance.Build.
    /// The same central logger instance from ProjectCollection should be reused here.
    /// Returns an empty collection if the central logger is null or if there's an error.
    /// </summary>
    /// <param name="centralLogger">The central logger instance (typically the same one used in ProjectCollection).</param>
    /// <returns>An array containing the forwarding logger record, or empty array if central logger is null.</returns>
    public static ForwardingLoggerRecord[] CreateTelemetryForwardingLoggerRecords(ILogger? centralLogger)
    {
        if (centralLogger is MSBuildLogger msbuildLogger)
        {
            try
            {
                // LoggerDescription describes the forwarding logger that worker nodes will create
                var forwardingLoggerDescription = new Microsoft.Build.Logging.LoggerDescription(
                    loggerClassName: typeof(MSBuildForwardingLogger).FullName!,
                    loggerAssemblyName: typeof(MSBuildForwardingLogger).Assembly.Location,
                    loggerAssemblyFile: null,
                    loggerSwitchParameters: null,
                    verbosity: LoggerVerbosity.Normal);

                var loggerRecord = new ForwardingLoggerRecord(msbuildLogger, forwardingLoggerDescription);
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
    /// Creates a logger collection that includes the telemetry central logger.
    /// This is useful for ProjectCollection scenarios where evaluation needs telemetry.
    /// Returns both the logger array and the telemetry central logger instance for reuse in subsequent builds.
    /// </summary>
    /// <param name="additionalLoggers">Additional loggers to include in the collection.</param>
    /// <returns>A tuple containing the logger array and the telemetry central logger (or null if no telemetry).</returns>
    public static (ILogger[]? loggers, ILogger? telemetryCentralLogger) CreateLoggersWithTelemetry(IEnumerable<ILogger>? additionalLoggers = null)
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

        return (loggers.Count > 0 ? loggers.ToArray() : null, telemetryCentralLogger);
    }
}
