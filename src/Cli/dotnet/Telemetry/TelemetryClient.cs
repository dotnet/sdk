// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

#if TARGET_WINDOWS
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
#endif

namespace Microsoft.DotNet.Cli.Telemetry;

public class TelemetryClient : ITelemetryClient
{
    private static FrozenDictionary<string, string?> s_commonProperties = [];
    private Task? _trackEventTask;

#if TARGET_WINDOWS
    private static readonly MeterProviderBuilder s_metricsProviderBuilder;
    private static MeterProvider? s_metricsProvider;
    private static readonly TracerProviderBuilder s_tracerProviderBuilder;
    private static TracerProvider? s_tracerProvider;
    private static readonly List<Activity> s_activities = [];

    private static readonly string s_connectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
    private static readonly string s_defaultStorageDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, "TelemetryStorageService");
    // Note: The TelemetryClient instance constructor takes in an environment provider. These fields don't use that currently.
    private static readonly string? s_environmentStoragePath = Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_STORAGE_PATH);
    private static readonly string? s_diskLogPath = Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_LOG_PATH);
    private static readonly bool s_disableTraceExport = Env.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT);
    private static readonly int s_flushTimeoutMs = 200;
#endif

    public static string? CurrentSessionId { get; private set; } = null;
    public static bool DisabledForTests
    {
        get => field;
        set
        {
            field = value;
            // When disabled, clear the session ID.
            if (field)
            {
                CurrentSessionId = null;
            }
        }
    } = false;
    public static ActivityContext ParentActivityContext { get; private set; }
    public static ActivityKind ActivityKind { get; private set; }

    public bool Enabled { get; }

    static TelemetryClient()
    {
#if TARGET_WINDOWS
        s_metricsProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddMeter(Activities.Source.Name)
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();

        s_tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddSource(Activities.Source.Name)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter()
            .AddInMemoryExporter(s_activities)
            .SetSampler(new AlwaysOnSampler());

        if (!s_disableTraceExport)
        {
            var storageDirectory = string.IsNullOrWhiteSpace(s_environmentStoragePath) ? s_defaultStorageDirectory : s_environmentStoragePath;
            s_tracerProviderBuilder.AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = s_connectionString;
                o.EnableLiveMetrics = false;
                o.StorageDirectory = storageDirectory;
            });
        }
#endif

        var parentActivityContext = GetParentActivityContext();
        ActivityKind = GetActivityKind(parentActivityContext);
        ParentActivityContext = parentActivityContext ?? default;
    }

    public TelemetryClient() : this(null) { }

    public TelemetryClient(string? sessionId, IEnvironmentProvider? environmentProvider = null)
    {
        // This is some kind of special condition for MSBuild-related tests.
        if (DisabledForTests)
        {
            return;
        }

        environmentProvider ??= new EnvironmentProvider();
        Enabled = !environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT,
            // When building in the official CI pipeline, this makes the complier enable telemetry by default. Otherwise, it is disabled.
            // It is the reason tests don't send telemetry, because we don't run tests in the official CI pipeline.
            defaultValue: CompileOptions.TelemetryOptOutDefault);
        if (!Enabled)
        {
            return;
        }

#if TARGET_WINDOWS
        if (s_metricsProvider is null || s_tracerProvider is null)
        {
            // Create a new OTel meter and tracer provider.
            // It is important to keep the provider instances active throughout the process lifetime.
            s_metricsProvider ??= s_metricsProviderBuilder.Build();
            s_tracerProvider ??= s_tracerProviderBuilder.Build();
        }
#endif

        CurrentSessionId ??= !string.IsNullOrEmpty(sessionId) ? sessionId : Guid.NewGuid().ToString();
        s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(CurrentSessionId);
    }

    /// <summary>
    /// Uses the OpenTelemetry SDK's Propagation API to derive the parent activity context from the DOTNET_CLI_TRACEPARENT and DOTNET_CLI_TRACESTATE environment variables.
    /// </summary>
    private static ActivityContext? GetParentActivityContext()
    {
        var traceParent = Env.GetEnvironmentVariable(Activities.TRACEPARENT);
        if (string.IsNullOrEmpty(traceParent))
        {
            return null;
        }

        var carrierMap = new Dictionary<string, IEnumerable<string>?> { { "traceparent", [traceParent] } };
        var traceState = Env.GetEnvironmentVariable(Activities.TRACESTATE);
        if (!string.IsNullOrEmpty(traceState))
        {
            carrierMap.Add("tracestate", [traceState]);
        }

        ActivityContext? parentContext = null;
#if TARGET_WINDOWS
        // Use the propagator to extract the parent activity context and kind.
        // For some reason, this isn't set by the OTel SDK like docs say it should be.
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        parentContext = Propagators.DefaultTextMapPropagator.Extract(default, carrierMap, GetValueFromCarrier).ActivityContext;
#endif
        return parentContext;

#if TARGET_WINDOWS
        static IEnumerable<string>? GetValueFromCarrier(Dictionary<string, IEnumerable<string>?> carrier, string key) =>
            carrier.TryGetValue(key, out var value) ? value : null;
#endif
    }

    private static ActivityKind GetActivityKind(ActivityContext? parentActivityContext) =>
        parentActivityContext is ActivityContext { IsRemote: true } ? ActivityKind.Server : ActivityKind.Internal;

    public static void FlushProviders()
    {
#if TARGET_WINDOWS
        s_tracerProvider?.ForceFlush(s_flushTimeoutMs);
        s_metricsProvider?.ForceFlush(s_flushTimeoutMs);
#endif
    }

    public static void WriteLogIfNecessary()
    {
#if TARGET_WINDOWS
        if (!string.IsNullOrWhiteSpace(s_diskLogPath) && s_activities.Any())
        {
            TelemetryDiskLogger.WriteLog(s_diskLogPath, s_activities);
        }
#endif
    }

    public void TrackEvent(string eventName, IDictionary<string, string?>? properties)
    {
        if (!Enabled)
        {
            return;
        }

        // Continue the task in different threads.
        _trackEventTask = _trackEventTask == null
            ? Task.Run(() => TrackEventTask(eventName, properties))
            : _trackEventTask.ContinueWith(_ => TrackEventTask(eventName, properties));
    }

    public void ThreadBlockingTrackEvent(string eventName, IDictionary<string, string?>? properties)
    {
        if (!Enabled)
        {
            return;
        }

        TrackEventTask(eventName, properties);
    }

    private static void TrackEventTask(string eventName, IDictionary<string, string?>? properties)
    {
        try
        {
            properties ??= new Dictionary<string, string?>();
            properties.Add("event id", Guid.NewGuid().ToString());
            var @event = new ActivityEvent($"dotnet/cli/{eventName}", tags: MakeTags(properties));
            Activity.Current?.AddEvent(@event);
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static ActivityTagsCollection MakeTags(IDictionary<string, string?> eventProperties)
    {
        var common = s_commonProperties
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value));
        var properties = eventProperties
            .Where(p => p.Value is not null)
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .OrderBy(p => p.Key);
        return [.. common, .. properties];
    }
}
