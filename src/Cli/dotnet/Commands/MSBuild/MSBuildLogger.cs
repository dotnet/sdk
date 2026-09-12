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
/// Receives telemetry from MSBuild and SDK build logic. The logger sends the telemetry
/// through the .NET SDK telemetry pipeline.
/// </summary>
/// <remarks>
/// MSBuild loads this type from <c>dotnet.dll</c> as a distributed logger. The logger is a
/// separate SDK entry point. It can run in the managed CLI process, a child MSBuild
/// process, or a persistent MSBuild server. Some hosts do not run either CLI bootstrap.
/// The logger initializes process-wide telemetry when necessary. It creates
/// request-specific activity state at <c>BuildStarted</c> and completes it at logger
/// shutdown, after MSBuild has emitted its final telemetry event.
/// </remarks>
public sealed class MSBuildLogger : INodeLogger
{
    /// <summary>
    /// The process-wide telemetry client used by this logger instance.
    /// </summary>
    /// <remarks>
    /// The managed CLI initializes this client before it runs MSBuild in the same process.
    /// Other processes use the parameterless constructor to initialize their own client.
    /// </remarks>
    private readonly ITelemetryClient? _telemetry;

    /// <summary>
    /// Whether this logger initialized the process-wide telemetry client.
    /// </summary>
    /// <remarks>
    /// The initializer flushes providers when this logger instance ends. If this logger did
    /// not initialize the client, the managed CLI controls the provider lifetime.
    /// </remarks>
    private readonly bool _initializedTelemetryClient;

    /// <summary>
    /// The activity owned by the current build.
    /// </summary>
    /// <remarks>
    /// This activity belongs to one build. It remains active until logger shutdown because
    /// MSBuild emits final telemetry after <c>BuildFinished</c>. A persistent server can run
    /// later builds with unrelated parent trace contexts in the same process.
    /// </remarks>
    private Activity? _activity;

    internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
    internal const string BuildTelemetryEventName = "build";
    internal const string LoggingConfigurationTelemetryEventName = "loggingConfiguration";
    internal const string BuildcheckAcquisitionFailureEventName = "buildcheck/acquisitionfailure";
    internal const string BuildcheckRunEventName = "buildcheck/run";
    internal const string BuildcheckRuleStatsEventName = "buildcheck/rule";

    // These events are aggregated and sent at the end of the build.
    internal const string TaskFactoryTelemetryAggregatedEventName = "build/tasks/taskfactory";
    internal const string TasksTelemetryAggregatedEventName = "build/tasks";
    internal const string MSBuildTaskSubclassedTelemetryAggregatedEventName = "build/tasks/msbuild-subclassed";
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
    /// Emitted by the Roslyn <c>Csc</c>/<c>Vbc</c> build task.
    /// </summary>
    internal const string RoslynCompilerCacheEventName = "roslyn/compilercache";

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
    /// MSBuild uses the parameterless constructor to create loggers. The managed CLI can
    /// initialize telemetry before it runs MSBuild in the same process. When another process
    /// loads the logger without an existing client, the constructor initializes one. It
    /// reuses an existing client to preserve CLI state. Telemetry failures must not fail the
    /// build.
    /// </remarks>
    public MSBuildLogger()
    {
        try
        {
            string? sessionId = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID);
            if (!TelemetryClient.IsInitialized)
            {
                _ = new TelemetryClient(sessionId);
                _initializedTelemetryClient = true;
                TelemetryClient.RegisterProviderShutdownOnProcessExit();
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
    /// <see cref="INodeLogger"/> requires this node-count overload. Both overloads use the
    /// same event subscriptions. Build events control the activity lifetime because a
    /// server can run multiple builds. Each build can have different request context.
    /// </remarks>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    /// <summary>
    /// Connects this logger to the events needed to collect telemetry and delimit a build.
    /// </summary>
    /// <remarks>
    /// The logger subscribes to telemetry events and <c>BuildStarted</c> only when telemetry
    /// is enabled. This avoids work for opted-out builds. The logger always subscribes to
    /// <c>BuildFinished</c> so it can record the build result.
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
    /// A persistent server can receive different environment and trace context for each
    /// request. This method resolves the parent at <c>BuildStarted</c>, not in the
    /// constructor. It uses the ambient activity when the managed CLI runs MSBuild in the
    /// same process. Otherwise, it reads the context that the invoking CLI forwarded. The
    /// activity is internal because it represents SDK work in the invoking command, not a
    /// remote client call.
    /// </remarks>
    private void OnBuildStarted(object sender, BuildStartedEventArgs e)
    {
        StopActivity();

        ActivityContext parentContext =
            TelemetryClient.GetParentActivityContext(preferEnvironmentVariables: true)
            ?? Activity.Current?.Context
            ?? TelemetryClient.ParentActivityContext;
        _activity = Activities.Source.StartActivity(
            "msbuild",
            ActivityKind.Internal,
            parentContext);
    }

    /// <summary>
    /// Records the result of one MSBuild request.
    /// </summary>
    /// <remarks>
    /// MSBuild emits its final build telemetry after <c>BuildFinished</c>. This method sets
    /// the span status but leaves the activity open until <see cref="Shutdown"/>, after the
    /// final telemetry event has been delivered to the logger.
    /// </remarks>
    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        SendAggregatedEventsOnBuildFinished(_telemetry);
        _activity?.SetStatus(e.Succeeded ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    /// <summary>
    /// Emits telemetry that is intentionally accumulated across nodes during a build.
    /// </summary>
    /// <remarks>
    /// A persistent server retains process state for the next build. This method removes
    /// each aggregate after it sends the aggregate. The next build cannot reuse counts from
    /// the completed request.
    /// </remarks>
    internal void SendAggregatedEventsOnBuildFinished(ITelemetryClient? telemetry)
    {
        if (telemetry is null) return;

        SendAggregatedEvent(telemetry, TaskFactoryTelemetryAggregatedEventName);
        SendAggregatedEvent(telemetry, TasksTelemetryAggregatedEventName);
        SendAggregatedEvent(telemetry, MSBuildTaskSubclassedTelemetryAggregatedEventName);
    }

    private void SendAggregatedEvent(ITelemetryClient telemetry, string eventName)
    {
        if (_aggregatedEvents.TryGetValue(eventName, out var eventData))
        {
            Dictionary<string, string?> properties = ConvertToStringDictionary(eventData);

            TrackEvent(telemetry, $"msbuild/{eventName}", properties, toBeHashed: []);
            _aggregatedEvents.Remove(eventName);
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
            case RoslynCompilerCacheEventName:
                TrackEvent(telemetry, $"msbuild/{RoslynCompilerCacheEventName}", args.Properties);
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

        if (telemetry is TelemetryClient telemetryClient)
        {
            // Add production events synchronously while the build activity is active.
            // Test clients use ITelemetryClient without a real telemetry client.
            telemetryClient.ThreadBlockingTrackEvent(eventName, properties ?? eventProperties);
        }
        else
        {
            telemetry?.TrackEvent(eventName, properties ?? eventProperties);
        }
    }

    private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
    {
        if (args.EventName == TaskFactoryTelemetryAggregatedEventName ||
            args.EventName == TasksTelemetryAggregatedEventName ||
            args.EventName == MSBuildTaskSubclassedTelemetryAggregatedEventName)
        {
            AggregateEvent(args);
        }
        else
        {
            FormatAndSend(_telemetry, args);
        }
    }

    /// <summary>
    /// Completes this MSBuild logger instance and writes its diagnostic telemetry log.
    /// </summary>
    /// <remarks>
    /// MSBuild calls this method after it has emitted the final build telemetry event. This
    /// method stops the build activity, waits for queued events, and writes the diagnostic
    /// log. If this logger initialized the telemetry client, it flushes the process-wide
    /// providers without shutting them down because a persistent server can run later
    /// builds. When the managed CLI runs MSBuild in the same process, the CLI controls the
    /// provider lifetime.
    /// </remarks>
    public void Shutdown()
    {
        StopActivity();

        if (_telemetry is TelemetryClient telemetryClient)
        {
            if (_initializedTelemetryClient)
            {
                // A persistent MSBuild server creates a logger for each build. Flush this
                // request without shutting down the process-wide providers needed by later
                // builds in the same server process.
                TelemetryClient.ForceFlushProviders();
            }
            else
            {
                telemetryClient.WaitForPendingEvents();
            }
        }

        TelemetryClient.WriteLogIfNecessary();
    }

    /// <summary>
    /// Stops only the activity owned by this logger and clears the reference.
    /// </summary>
    /// <remarks>
    /// The invoking host owns the ambient parent activity. This method does not stop the
    /// parent. Because this method clears the field, both a replacement build and
    /// <see cref="Shutdown"/> can call it safely.
    /// </remarks>
    private void StopActivity()
    {
        _activity?.Stop();
        _activity = null;
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }
}
