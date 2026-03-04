// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using OpenTelemetry;
//using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry;

public class TelemetryClient : ITelemetryClient
{
    private static FrozenDictionary<string, string?> s_commonProperties = [];
    private Task? _trackEventTask;

    private static readonly MeterProvider s_metricsProvider;
    private static readonly TracerProvider s_tracerProvider;
    private static readonly List<Activity> s_activities = [];
    private static readonly string s_connectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
    private static readonly string s_defaultStorageDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, "TelemetryStorageService");
    // TODO: TelemetryInstance takes in an environment provider. These fields don't use that currently.
    private static readonly string? s_environmentStoragePath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_STORAGE_PATH);
    private static readonly string? s_diskLogPath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_LOG_PATH);
    private static readonly int s_flushTimeoutMs = 200;

    public static string? CurrentSessionId { get; private set; } = null;
    public static bool DisabledForTests
    {
        get => field;
        set
        {
            field = value;
            if (field)
            {
                CurrentSessionId = null;
            }
        }
    } = false;

    public bool Enabled { get; }

    static TelemetryClient()
    {
        // Create a new OpenTelemetry meter provider and add the Azure Monitor metric exporter and the OTLP metric exporter.
        // It is important to keep the MetricsProvider instance active throughout the process lifetime.
        s_metricsProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddMeter(Activities.Source.Name)
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter()
            .Build();

        // Create a new OpenTelemetry tracer provider and add the Azure Monitor trace exporter and the OTLP trace exporter.
        // It is important to keep the TracerProvider instance active throughout the process lifetime.
        s_tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddSource(Activities.Source.Name)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter()
            .AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = s_connectionString;
                o.EnableLiveMetrics = false;
                o.StorageDirectory = string.IsNullOrWhiteSpace(s_environmentStoragePath) ? s_defaultStorageDirectory : s_environmentStoragePath;
            })
            .AddInMemoryExporter(s_activities)
            .SetSampler(new AlwaysOnSampler())
            .Build();
    }

    public TelemetryClient() : this(null) { }

    public TelemetryClient(string? sessionId, IEnvironmentProvider? environmentProvider = null)
    {
        if (DisabledForTests)
        {
            return;
        }

        environmentProvider ??= new EnvironmentProvider();
        Enabled = !environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault);
        if (!Enabled)
        {
            return;
        }

        CurrentSessionId ??= !string.IsNullOrEmpty(sessionId) ? sessionId : Guid.NewGuid().ToString();
        s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(CurrentSessionId);
    }

    public static void FlushProviders()
    {
        s_tracerProvider?.ForceFlush(s_flushTimeoutMs);
        s_metricsProvider?.ForceFlush(s_flushTimeoutMs);
    }

    public static void WriteLogIfNecessary()
    {
        if (!string.IsNullOrWhiteSpace(s_diskLogPath))
        {
            TelemetryDiskLogger.WriteLog(s_diskLogPath, s_activities);
        }
    }

    public void TrackEvent(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled || eventName is null)
        {
            return;
        }

        // Continue the task in different threads.
        _trackEventTask = _trackEventTask == null
            ? Task.Run(() => TrackEventTask(eventName, properties, measurements))
            : _trackEventTask.ContinueWith(_ => TrackEventTask(eventName, properties, measurements));
    }

    public void ThreadBlockingTrackEvent(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled || eventName is null)
        {
            return;
        }

        TrackEventTask(eventName, properties, measurements);
    }

    private static void TrackEventTask(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        try
        {
            properties ??= new Dictionary<string, string?>();
            properties.Add("event id", Guid.NewGuid().ToString());
            measurements ??= new Dictionary<string, double>();
            var @event = new ActivityEvent($"dotnet/cli/{eventName}", tags: MakeTags(properties, measurements));
            Activity.Current?.AddEvent(@event);
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static ActivityTagsCollection MakeTags(IDictionary<string, string?> eventProperties, IDictionary<string, double> eventMeasurements)
    {
        var common = s_commonProperties
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value));
        var properties = eventProperties
            .Where(p => p.Value is not null)
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .OrderBy(p => p.Key);
        var measurements = eventMeasurements
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .OrderBy(p => p.Key);
        return [.. common, .. properties, .. measurements];
    }
}
