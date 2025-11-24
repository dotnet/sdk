// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Provides methods for building projects with automatic telemetry integration.
/// This class handles the complexity of setting up distributed logging for MSBuild.
/// </summary>
public sealed class DotNetProjectBuilder
{
    private readonly DotNetProject _project;
    private readonly ILogger? _telemetryCentralLogger;

    /// <summary>
    /// Initializes a new instance of the DotNetProjectBuilder class.
    /// </summary>
    /// <param name="project">The project to build.</param>
    /// <param name="evaluator">The evaluator to source telemetry logger from (optional).</param>
    public DotNetProjectBuilder(DotNetProject project, DotNetProjectEvaluator? evaluator = null)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _telemetryCentralLogger = evaluator?.TelemetryCentralLogger;
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="targets">The targets to build.</param>
    /// <returns>A BuildResult indicating success or failure.</returns>
    public BuildResult Build(params string[] targets)
    {
        return Build(targets, additionalLoggers: null);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="targets">The targets to build.</param>
    /// <param name="additionalLoggers">Additional loggers to include.</param>
    /// <returns>A BuildResult indicating success or failure.</returns>
    public BuildResult Build(string[] targets, IEnumerable<ILogger>? additionalLoggers)
    {
        var success = BuildInternal(targets, additionalLoggers, remoteLoggers: null, out var targetOutputs);
        return new BuildResult(success, targetOutputs);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="targets">The targets to build.</param>
    /// <param name="targetOutputs">The outputs from the build.</param>
    /// <returns>A BuildResult indicating success or failure.</returns>
    public BuildResult Build(string[] targets, out IDictionary<string, TargetResult> targetOutputs)
    {
        var success = BuildInternal(targets, loggers: null, remoteLoggers: null, out targetOutputs);
        return new BuildResult(success, targetOutputs);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="targets">The targets to build.</param>
    /// <param name="loggers">Loggers to include.</param>
    /// <param name="targetOutputs">The outputs from the build.</param>
    /// <returns>A BuildResult indicating success or failure.</returns>
    public BuildResult Build(string[] targets, IEnumerable<ILogger>? loggers, out IDictionary<string, TargetResult> targetOutputs)
    {
        var success = BuildInternal(targets, loggers, remoteLoggers: null, out targetOutputs);
        return new BuildResult(success, targetOutputs);
    }

    /// <summary>
    /// Builds the project with the specified targets, automatically including telemetry loggers
    /// as a distributed logger (central logger + forwarding logger).
    /// </summary>
    /// <param name="targets">The targets to build.</param>
    /// <param name="loggers">Loggers to include.</param>
    /// <param name="remoteLoggers">Remote/forwarding loggers to include.</param>
    /// <param name="targetOutputs">The outputs from the build.</param>
    /// <returns>A BuildResult indicating success or failure.</returns>
    public BuildResult Build(
        string[] targets,
        IEnumerable<ILogger>? loggers,
        IEnumerable<ForwardingLoggerRecord>? remoteLoggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var success = BuildInternal(targets, loggers, remoteLoggers, out targetOutputs);
        return new BuildResult(success, targetOutputs);
    }

    private bool BuildInternal(
        string[] targets,
        IEnumerable<ILogger>? loggers,
        IEnumerable<ForwardingLoggerRecord>? remoteLoggers,
        out IDictionary<string, TargetResult> targetOutputs)
    {
        var allLoggers = new List<ILogger>();
        var allForwardingLoggers = new List<ForwardingLoggerRecord>();

        // Add telemetry as a distributed logger via ForwardingLoggerRecord
        // Use provided central logger or create a new one
        var centralLogger = _telemetryCentralLogger ?? TelemetryUtilities.CreateTelemetryCentralLogger();
        allForwardingLoggers.AddRange(TelemetryUtilities.CreateTelemetryForwardingLoggerRecords(centralLogger));

        if (loggers != null)
        {
            allLoggers.AddRange(loggers);
        }

        if (remoteLoggers != null)
        {
            allForwardingLoggers.AddRange(remoteLoggers);
        }

        return _project.Build(
            targets,
            allLoggers.Count > 0 ? allLoggers : null,
            allForwardingLoggers.Count > 0 ? allForwardingLoggers : null,
            out targetOutputs);
    }
}

/// <summary>
/// Represents the result of a build operation.
/// </summary>
/// <param name="Success">Whether the build succeeded.</param>
/// <param name="TargetOutputs">The outputs from the build targets, if any.</param>
public record BuildResult(bool Success, IDictionary<string, TargetResult>? TargetOutputs = null);
