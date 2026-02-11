﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Utilities;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

public sealed class MSBuildLogger : INodeLogger
{
    private readonly IFirstTimeUseNoticeSentinel _sentinel =
        new FirstTimeUseNoticeSentinel();
    private readonly ITelemetry? _telemetry;

    internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
    internal const string BuildTelemetryEventName = "build";
    internal const string LoggingConfigurationTelemetryEventName = "loggingConfiguration";
    internal const string BuildcheckAcquisitionFailureEventName = "buildcheck/acquisitionfailure";
    internal const string BuildcheckRunEventName = "buildcheck/run";
    internal const string BuildcheckRuleStatsEventName = "buildcheck/rule";

    // These two events are aggregated and sent at the end of the build.
    internal const string TaskFactoryTelemetryAggregatedEventName = "build/tasks/taskfactory";
    internal const string TasksTelemetryAggregatedEventName = "build/tasks";

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
    /// Stores project evaluation statistics for telemetry.
    /// </summary>
    private int _evaluationCount;
    private readonly List<double> _evaluationDurations = new();

    public MSBuildLogger()
    {
        try
        {
            string? sessionId =
                Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

            if (sessionId != null)
            {
                // senderCount: 0 to disable sender.
                // When senders in different process running at the same
                // time they will read from the same global queue and cause
                // sending duplicated events. Disable sender to reduce it.
                _telemetry = new Telemetry.Telemetry(
                    _sentinel,
                    sessionId,
                    senderCount: 0);
            }
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    /// <summary>
    /// Constructor for testing purposes.
    /// </summary>
    internal MSBuildLogger(ITelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    public void Initialize(IEventSource eventSource)
    {
        try
        {
            if (eventSource is IEventSource4 eventSource4)
            {
                // Declare lack of dependency on having properties/items in ProjectStarted events
                // (since this logger doesn't ever care about those events it's irrelevant)
                eventSource4.IncludeEvaluationPropertiesAndItems();
            }

            if (_telemetry != null && _telemetry.Enabled)
            {
                if (eventSource is IEventSource2 eventSource2)
                {
                    eventSource2.TelemetryLogged += OnTelemetryLogged;
                }

                // Subscribe to status events to capture ProjectEvaluationFinished
                eventSource.StatusEventRaised += OnStatusEventRaised;
            }

            eventSource.BuildFinished += OnBuildFinished;
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        SendAggregatedEventsOnBuildFinished(_telemetry);
    }

    private void OnStatusEventRaised(object sender, BuildStatusEventArgs e)
    {
        if (e is ProjectEvaluationFinishedEventArgs evaluationArgs)
        {
            OnProjectEvaluationFinished(evaluationArgs);
        }
    }

    private void OnProjectEvaluationFinished(ProjectEvaluationFinishedEventArgs e)
    {
        try
        {
            _evaluationCount++;
            
            // Track evaluation duration in milliseconds
            var durationMs = (e.ProfilerResult?.ProfiledLocations?
                .Values
                .Sum(location => location.InclusiveTime.TotalMilliseconds)) ?? 0.0;
            
            _evaluationDurations.Add(durationMs);
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    internal void SendAggregatedEventsOnBuildFinished(ITelemetry? telemetry)
    {
        if (telemetry is null) return;
        if (_aggregatedEvents.TryGetValue(TaskFactoryTelemetryAggregatedEventName, out var taskFactoryData))
        {
            Dictionary<string, string?> taskFactoryProperties = ConvertToStringDictionary(taskFactoryData);

            TrackEvent(telemetry, $"msbuild/{TaskFactoryTelemetryAggregatedEventName}", taskFactoryProperties, toBeHashed: [], toBeMeasured: []);
            _aggregatedEvents.Remove(TaskFactoryTelemetryAggregatedEventName);
        }

        if (_aggregatedEvents.TryGetValue(TasksTelemetryAggregatedEventName, out var tasksData))
        {
            Dictionary<string, string?> tasksProperties = ConvertToStringDictionary(tasksData);

            TrackEvent(telemetry, $"msbuild/{TasksTelemetryAggregatedEventName}", tasksProperties, toBeHashed: [], toBeMeasured: []);
            _aggregatedEvents.Remove(TasksTelemetryAggregatedEventName);
        }

        // Send project evaluation telemetry
        if (_evaluationCount > 0)
        {
            SendEvaluationTelemetry(telemetry);
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

    private void SendEvaluationTelemetry(ITelemetry telemetry)
    {
        var properties = new Dictionary<string, string?>();
        var measurements = new Dictionary<string, double>();

        // Total count of evaluations
        properties["TotalCount"] = _evaluationCount.ToString(CultureInfo.InvariantCulture);

        if (_evaluationDurations.Count > 0)
        {
            // Total evaluation time
            var totalTime = _evaluationDurations.Sum();
            measurements["TotalDurationInMilliseconds"] = totalTime;

            // Average evaluation time
            var avgTime = _evaluationDurations.Average();
            measurements["AverageDurationInMilliseconds"] = avgTime;

            // Min and max evaluation times
            measurements["MinDurationInMilliseconds"] = _evaluationDurations.Min();
            measurements["MaxDurationInMilliseconds"] = _evaluationDurations.Max();

            // Time distribution percentiles
            var sortedDurations = _evaluationDurations.OrderBy(d => d).ToList();
            measurements["P50DurationInMilliseconds"] = GetPercentile(sortedDurations, 0.50);
            measurements["P90DurationInMilliseconds"] = GetPercentile(sortedDurations, 0.90);
            measurements["P95DurationInMilliseconds"] = GetPercentile(sortedDurations, 0.95);
        }

        telemetry.TrackEvent("msbuild/projectevaluations", properties, measurements);
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0.0;
        
        int index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
        
        return sortedValues[index];
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

    internal static void FormatAndSend(ITelemetry? telemetry, TelemetryEventArgs args)
    {
        switch (args.EventName)
        {
            case TargetFrameworkTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{TargetFrameworkTelemetryEventName}", args.Properties);
                break;
            case BuildTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{BuildTelemetryEventName}", args.Properties,
                    toBeHashed: ["ProjectPath", "BuildTarget"],
                    toBeMeasured: ["BuildDurationInMilliseconds", "InnerBuildDurationInMilliseconds"]
                );
                break;
            case LoggingConfigurationTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{LoggingConfigurationTelemetryEventName}", args.Properties,
                    toBeHashed: [],
                    toBeMeasured: []);
                break;
            case BuildcheckAcquisitionFailureEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckAcquisitionFailureEventName}", args.Properties,
                    toBeHashed: ["AssemblyName", "ExceptionType", "ExceptionMessage"]
                );
                break;
            case BuildcheckRunEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRunEventName}", args.Properties,
                    toBeMeasured: ["TotalRuntimeInMilliseconds"]
                );
                break;
            case BuildcheckRuleStatsEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRuleStatsEventName}", args.Properties,
                    toBeHashed: ["RuleId", "CheckFriendlyName"],
                    toBeMeasured: ["TotalRuntimeInMilliseconds"]
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

    private static void TrackEvent(ITelemetry? telemetry, string eventName, IDictionary<string, string?> eventProperties, string[]? toBeHashed = null, string[]? toBeMeasured = null)
    {
        if (telemetry == null || !telemetry.Enabled)
        {
            return;
        }

        Dictionary<string, string?>? properties = null;
        Dictionary<string, double>? measurements = null;

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

        if (toBeMeasured is not null)
        {
            foreach (var propertyToBeMeasured in toBeMeasured)
            {
                if (eventProperties.TryGetValue(propertyToBeMeasured, out var value))
                {
                    // Lets lazy allocate in case there is tons of telemetry
                    properties ??= new(eventProperties);
                    properties.Remove(propertyToBeMeasured);
                    if (double.TryParse(value, CultureInfo.InvariantCulture, out double realValue))
                    {
                        // Lets lazy allocate in case there is tons of telemetry
                        measurements ??= [];
                        measurements[propertyToBeMeasured] = realValue;
                    }
                }
            }
        }

        telemetry.TrackEvent(eventName, properties ?? eventProperties, measurements);
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

    public void Shutdown()
    {
        try
        {
            _sentinel?.Dispose();
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }
}
