﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Utilities;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

/// <summary>
/// Receives telemetry emitted by MSBuild and SDK build logic and forwards it through the
/// .NET SDK telemetry pipeline.
/// </summary>
/// <remarks>
/// MSBuild loads this type from <c>dotnet.dll</c> as a distributed logger. This makes the
/// logger a separate SDK entry point: it can run inside the managed CLI process, in a child
/// MSBuild process, or in a persistent MSBuild server where neither CLI bootstrap runs.
/// Consequently, process-wide telemetry is initialized here when necessary, while
/// request-specific activity state is created and cleared at MSBuild's per-build event
/// boundaries.
/// </remarks>
public sealed class MSBuildLogger : INodeLogger
{
    /// <summary>
    /// The process-wide telemetry client used by this logger instance.
    /// </summary>
    /// <remarks>
    /// In-process builds reuse the client initialized by the managed CLI. An out-of-process
    /// host, including the persistent MSBuild server, initializes the same process-wide
    /// client through the parameterless constructor.
    /// </remarks>
    private readonly ITelemetryClient? _telemetry;

    /// <summary>
    /// The activity owned by the current build.
    /// </summary>
    /// <remarks>
    /// This must never outlive <c>BuildFinished</c>: a persistent MSBuild server can handle
    /// later builds with unrelated parent trace contexts in the same process.
    /// </remarks>
    private Activity? _activity;

    internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
    internal const string BuildTelemetryEventName = "build";
    internal const string LoggingConfigurationTelemetryEventName = "loggingConfiguration";
    internal const string BuildcheckAcquisitionFailureEventName = "buildcheck/acquisitionfailure";
    internal const string BuildcheckRunEventName = "buildcheck/run";
    internal const string BuildcheckRuleStatsEventName = "buildcheck/rule";

    // These two events are aggregated and sent at the end of the build.
    internal const string TaskFactoryTelemetryAggregatedEventName = "build/tasks/taskfactory";
    internal const string TasksTelemetryAggregatedEventName = "build/tasks";
    internal const string TasksDetailsTelemetryEventName = "build/tasks/details";

    internal const string SdkTaskBaseCatchExceptionTelemetryEventName = "taskBaseCatchException";
    internal const string PublishPropertiesTelemetryEventName = "PublishProperties";
    internal const string WorkloadPublishPropertiesTelemetryEventName = "WorkloadPublishProperties";
    internal const string ReadyToRunTelemetryEventName = "ReadyToRun";

    internal const string TargetFrameworkVersionTelemetryPropertyKey = "TargetFrameworkVersion";
    internal const string RuntimeIdentifierTelemetryPropertyKey = "RuntimeIdentifier";
    internal const string SelfContainedTelemetryPropertyKey = "SelfContained";
    internal const string UseApphostTelemetryPropertyKey = "UseApphost";
    internal const string OutputTypeTelemetryPropertyKey = "OutputType";
    internal const string UseArtifactsOutputTelemetryPropertyKey = "UseArtifactsOutput";
    internal const string ArtifactsPathLocationTypeTelemetryPropertyKey = "ArtifactsPathLocationType";

    /// <summary>
    /// This is defined in <see cref="ComputeDotnetBaseImageAndTag.cs"/>
    /// </summary>
    internal const string SdkContainerPublishBaseImageInferenceEventName = "sdk/container/inference";
    /// <summary>
    /// This is defined in <see cref="CreateNewImage.cs"/>
    /// </summary>
    internal const string SdkContainerPublishSuccessEventName = "sdk/container/publish/success";
    /// <summary>
    /// This is defined in <see cref="CreateNewImage.cs"/>
    /// </summary>
    internal const string SdkContainerPublishErrorEventName = "sdk/container/publish/error";

    /// <summary>
    /// Stores aggregated telemetry data by event name and property name.
    /// </summary>
    /// <remarks>
    /// Key: event name, Value: property name to aggregated count.
    /// Aggregation is very basic. Only integer properties are aggregated by summing values. Non-integer properties are ignored.
    /// </remarks>
    private Dictionary<string, Dictionary<string, int>> _aggregatedEvents = new();

    /// <summary>
    /// Initializes telemetry for the process hosting the logger.
    /// </summary>
    /// <remarks>
    /// MSBuild constructs loggers through their parameterless constructor. The managed CLI
    /// may already have initialized telemetry for an in-process build, but a child process
    /// or persistent server enters the SDK through this logger and has no such guarantee.
    /// Reusing an initialized client preserves CLI-owned state; initializing only when
    /// necessary gives standalone MSBuild hosts the same enablement and session behavior.
    /// Telemetry failures are isolated because diagnostics must never fail the build.
    /// </remarks>
    public MSBuildLogger()
    {
        try
        {
            string? sessionId = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID);

            if (!TelemetryClient.IsInitialized)
            {
                _ = new TelemetryClient(sessionId);
            }

            _telemetry = TelemetryClient.Instance;
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    /// <summary>
    /// Constructor for testing purposes.
    /// </summary>
    internal MSBuildLogger(ITelemetryClient telemetry)
    {
        _telemetry = telemetry;
    }

    /// <summary>
    /// Connects this node logger to MSBuild's event lifecycle.
    /// </summary>
    /// <remarks>
    /// The node-count overload exists for <see cref="INodeLogger"/> and intentionally shares
    /// the same subscriptions as the standard logger overload. Activity ownership follows
    /// build events rather than logger construction because a server process can execute
    /// multiple builds and supply different request context to each one.
    /// </remarks>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    /// <summary>
    /// Connects this logger to the events needed to collect telemetry and delimit a build.
    /// </summary>
    /// <remarks>
    /// Telemetry events and <c>BuildStarted</c> are observed only when telemetry is enabled,
    /// avoiding collection and activity work for opted-out builds. <c>BuildFinished</c> is
    /// always observed as a defensive cleanup boundary so any activity owned by this logger
    /// cannot leak into a later request.
    /// </remarks>
    public void Initialize(IEventSource eventSource)
    {
        // Declare lack of dependency on having properties/items in ProjectStarted events
        // (since this logger doesn't ever care about those events it's irrelevant)
        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }

        try
        {
            if (_telemetry != null && _telemetry.Enabled)
            {
                if (eventSource is IEventSource2 eventSource2)
                {
                    eventSource2.TelemetryLogged += OnTelemetryLogged;
                }

                eventSource.BuildStarted += OnBuildStarted;
            }

            eventSource.BuildFinished += OnBuildFinished;
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    /// <summary>
    /// Starts the activity that contains telemetry for one MSBuild request.
    /// </summary>
    /// <remarks>
    /// The parent is resolved here, not in the constructor, because a persistent server's
    /// environment and trace context can change for every request. An ambient activity is
    /// preferred for in-process builds; otherwise the context forwarded by the invoking CLI
    /// is re-read from the current request environment. The activity is internal because it
    /// represents SDK work within the invoking command rather than a remote client call.
    /// </remarks>
    private void OnBuildStarted(object sender, BuildStartedEventArgs e)
    {
        ActivityContext parentContext =
            Activity.Current?.Context
            ?? TelemetryClient.GetParentActivityContext()
            ?? TelemetryClient.ParentActivityContext;
        _activity = Activities.Source.StartActivity(
            "msbuild",
            ActivityKind.Internal,
            parentContext);
    }

    /// <summary>
    /// Completes telemetry and activity state for one MSBuild request.
    /// </summary>
    /// <remarks>
    /// Aggregated events are emitted before the activity is stopped so they remain attached
    /// to the build span. The overall MSBuild result supplies the span status. Stopping and
    /// clearing the activity here prevents a persistent server from parenting a later build
    /// to the completed request.
    /// </remarks>
    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        SendAggregatedEventsOnBuildFinished(_telemetry);
        _activity?.SetStatus(e.Succeeded ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        StopActivity();
    }

    /// <summary>
    /// Emits telemetry that is intentionally accumulated across nodes during a build.
    /// </summary>
    /// <remarks>
    /// Removing each emitted aggregate is essential for persistent servers: process state
    /// can survive into another build, but counts from the completed request must not.
    /// </remarks>
    internal void SendAggregatedEventsOnBuildFinished(ITelemetryClient? telemetry)
    {
        if (telemetry is null) return;
        if (_aggregatedEvents.TryGetValue(TaskFactoryTelemetryAggregatedEventName, out var taskFactoryData))
        {
            Dictionary<string, string?> taskFactoryProperties = ConvertToStringDictionary(taskFactoryData);

            TrackEvent(telemetry, $"msbuild/{TaskFactoryTelemetryAggregatedEventName}", taskFactoryProperties, toBeHashed: []);
            _aggregatedEvents.Remove(TaskFactoryTelemetryAggregatedEventName);
        }

        if (_aggregatedEvents.TryGetValue(TasksTelemetryAggregatedEventName, out var tasksData))
        {
            Dictionary<string, string?> tasksProperties = ConvertToStringDictionary(tasksData);

            TrackEvent(telemetry, $"msbuild/{TasksTelemetryAggregatedEventName}", tasksProperties, toBeHashed: []);
            _aggregatedEvents.Remove(TasksTelemetryAggregatedEventName);
        }
    }

    private static Dictionary<string, string?> ConvertToStringDictionary(Dictionary<string, int> properties)
    {
        Dictionary<string, string?> stringProperties = new();
        foreach (var kvp in properties)
        {
            stringProperties[kvp.Key] = kvp.Value.ToString(CultureInfo.InvariantCulture);
        }

        return stringProperties;
    }

    internal void AggregateEvent(TelemetryEventArgs args)
    {
        if (args.EventName is null) return;
        if (!_aggregatedEvents.TryGetValue(args.EventName, out Dictionary<string, int>? eventData) || eventData is null)
        {
            eventData = new Dictionary<string, int>();
            _aggregatedEvents[args.EventName] = eventData;
        }

        foreach (var kvp in args.Properties)
        {
            if (int.TryParse(kvp.Value, CultureInfo.InvariantCulture, out int count))
            {
                if (!eventData.ContainsKey(kvp.Key))
                {
                    eventData[kvp.Key] = count;
                }
                else
                {
                    eventData[kvp.Key] += count;
                }
            }
        }
    }

    internal static void FormatAndSend(ITelemetryClient? telemetry, TelemetryEventArgs args)
    {
        switch (args.EventName)
        {
            case TargetFrameworkTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{TargetFrameworkTelemetryEventName}", args.Properties);
                break;
            case BuildTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{BuildTelemetryEventName}", args.Properties,
                    toBeHashed: ["ProjectPath", "BuildTarget"]
                );
                break;
            case LoggingConfigurationTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{LoggingConfigurationTelemetryEventName}", args.Properties,
                    toBeHashed: []
                );
                break;
            case BuildcheckAcquisitionFailureEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckAcquisitionFailureEventName}", args.Properties,
                    toBeHashed: ["AssemblyName", "ExceptionType", "ExceptionMessage"]
                );
                break;
            case BuildcheckRunEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRunEventName}", args.Properties);
                break;
            case BuildcheckRuleStatsEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRuleStatsEventName}", args.Properties,
                    toBeHashed: ["RuleId", "CheckFriendlyName"]
                );
                break;
            case TasksDetailsTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{TasksDetailsTelemetryEventName}", args.Properties,
                    toBeHashed: []
                );
                break;
            // Pass through events that don't need special handling
            case SdkTaskBaseCatchExceptionTelemetryEventName:
            case PublishPropertiesTelemetryEventName:
            case ReadyToRunTelemetryEventName:
            case WorkloadPublishPropertiesTelemetryEventName:
            case SdkContainerPublishBaseImageInferenceEventName:
            case SdkContainerPublishSuccessEventName:
            case SdkContainerPublishErrorEventName:
                TrackEvent(telemetry, args.EventName, args.Properties);
                break;
            default:
                // Ignore unknown events
                break;
        }
    }

    private static void TrackEvent(ITelemetryClient? telemetry, string eventName, IDictionary<string, string?> eventProperties, string[]? toBeHashed = null)
    {
        if (telemetry == null || !telemetry.Enabled)
        {
            return;
        }

        Dictionary<string, string?>? properties = null;

        if (toBeHashed is not null)
        {
            foreach (var propertyToBeHashed in toBeHashed)
            {
                if (eventProperties.TryGetValue(propertyToBeHashed, out var value))
                {
                    // Lets lazy allocate in case there is tons of telemetry
                    properties ??= new(eventProperties);
                    properties[propertyToBeHashed] = Sha256Hasher.HashWithNormalizedCasing(value!);
                }
            }
        }

        telemetry?.TrackEvent(eventName, properties ?? eventProperties);
    }

    private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
    {
        if (args.EventName == TaskFactoryTelemetryAggregatedEventName || args.EventName == TasksTelemetryAggregatedEventName)
        {
            AggregateEvent(args);
        }
        else
        {
            FormatAndSend(_telemetry, args);
        }
    }

    /// <summary>
    /// Completes this MSBuild logger instance and drains its telemetry.
    /// </summary>
    /// <remarks>
    /// <c>BuildFinished</c> is the normal activity boundary. The additional stop is an
    /// idempotent fallback for aborted builds whose finish event was not delivered.
    /// Exporter draining and diagnostic-log writing belong here rather than in
    /// <c>BuildFinished</c> because MSBuild defines <see cref="Shutdown"/> as the logger
    /// completion hook. In a persistent server this does not imply process shutdown; the
    /// process-wide telemetry client remains reusable by a later logger instance.
    /// </remarks>
    public void Shutdown()
    {
        StopActivity();

        if (_telemetry is TelemetryClient telemetryClient)
        {
            telemetryClient.WaitForPendingEvents();
        }

        TelemetryClient.WriteLogIfNecessary();
    }

    /// <summary>
    /// Stops only the activity owned by this logger and clears the reference.
    /// </summary>
    /// <remarks>
    /// The ambient parent belongs to the invoking host and must not be stopped here.
    /// Clearing the field also makes cleanup safe when both <c>BuildFinished</c> and
    /// <see cref="Shutdown"/> run.
    /// </remarks>
    private void StopActivity()
    {
        _activity?.Stop();
        _activity = null;
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }
}
