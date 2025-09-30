// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
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
    /// Creates telemetry loggers for API-based MSBuild usage if telemetry is enabled.
    /// Returns null if telemetry is not enabled or if there's an error creating the loggers.
    /// </summary>
    /// <returns>A list of loggers to use with ProjectInstance.Build, or null if telemetry is disabled.</returns>
    public static ILogger[]? CreateTelemetryLoggers()
    {
        if (Telemetry.Telemetry.CurrentSessionId != null)
        {
            try
            {
                return [new MSBuildLogger()];
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }
        return null;
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers.
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? additionalLoggers = null)
    {
        var loggers = new List<ILogger>();

        var telemetryLoggers = CreateTelemetryLoggers();
        if (telemetryLoggers != null)
        {
            loggers.AddRange(telemetryLoggers);
        }

        if (additionalLoggers != null)
        {
            loggers.AddRange(additionalLoggers);
        }

        return projectInstance.Build(targets, loggers.Count > 0 ? loggers : null);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers.
    /// Overload for Build with targetOutputs parameter.
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var allLoggers = new List<ILogger>();

        var telemetryLoggers = CreateTelemetryLoggers();
        if (telemetryLoggers != null)
        {
            allLoggers.AddRange(telemetryLoggers);
        }

        if (loggers != null)
        {
            allLoggers.AddRange(loggers);
        }

        return projectInstance.Build(targets, allLoggers.Count > 0 ? allLoggers : null, out targetOutputs);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers.
    /// Overload for Build with loggers, remoteLoggers, and targetOutputs parameters.
    /// </summary>
    public static bool BuildWithTelemetry(
        this ProjectInstance projectInstance,
        string[] targets,
        IEnumerable<ILogger>? loggers,
        IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord>? remoteLoggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var allLoggers = new List<ILogger>();

        var telemetryLoggers = CreateTelemetryLoggers();
        if (telemetryLoggers != null)
        {
            allLoggers.AddRange(telemetryLoggers);
        }

        if (loggers != null)
        {
            allLoggers.AddRange(loggers);
        }

        return projectInstance.Build(targets, allLoggers.Count > 0 ? allLoggers : null, remoteLoggers, out targetOutputs);
    }
}
