// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

#if MICROSOFT_ENABLE_TELEMETRY_AZURE_MONITOR
using Azure.Monitor.OpenTelemetry.Exporter;
#endif
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Cli.Telemetry;

public class TelemetryClient : ITelemetryClient
{
    private static FrozenDictionary<string, string?> s_commonProperties = [];
    private Task? _trackEventTask;

    private static readonly MeterProviderBuilder s_metricsProviderBuilder;
    private static MeterProvider? s_metricsProvider;
    private static readonly TracerProviderBuilder s_tracerProviderBuilder;
    private static TracerProvider? s_tracerProvider;
    private static readonly List<Activity> s_activities = [];

#if MICROSOFT_ENABLE_TELEMETRY_AZURE_MONITOR
    private static readonly string s_connectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254;IngestionEndpoint=https://southcentralus-0.in.applicationinsights.azure.com/;LiveEndpoint=https://southcentralus.livediagnostics.monitor.azure.com/;ApplicationId=c5108c2c-b0c5-43c6-a703-424eae223a75";
    private static readonly string s_defaultStorageDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, "TelemetryStorageService");
    // Note: The TelemetryClient instance constructor takes in an environment provider. These fields don't use that currently.
    private static readonly string? s_environmentStoragePath = Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_STORAGE_PATH);
#endif
    private static readonly string? s_diskLogPath = Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_LOG_PATH);
    private static readonly bool s_disableTraceExport = Env.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT);
    // The OTLP exporter is enabled when:
    //   1. The SDK-specific DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER env var is true, or
    //   2. Any of the standard OpenTelemetry OTLP exporter env vars are set (per
    //      https://opentelemetry.io/docs/specs/otel/protocol/exporter/), signaling that
    //      the user has configured an OTLP endpoint/protocol/headers and intends to export.
    // When enabled, AddOtlpExporter() is called without an inline configuration callback,
    // which lets the OpenTelemetry SDK's OtlpExporterOptions read the standard env vars
    // itself to determine endpoint, protocol, headers, timeout, etc.
    private static readonly bool s_enableOtlpExporter =
        Env.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER)
        || (!Env.GetEnvironmentVariableAsBool(EnvironmentVariableNames.OTEL_SDK_DISABLED) && IsOtlpExporterConfiguredByStandardEnvVars());
    private static readonly int s_flushTimeoutMs = 10;

    /// <summary>
    /// Returns true if any of the standard OpenTelemetry OTLP exporter environment variables
    /// are set, signaling that the user has configured the OTLP exporter and expects it to be used.
    /// See https://opentelemetry.io/docs/specs/otel/protocol/exporter/.
    /// </summary>
    private static bool IsOtlpExporterConfiguredByStandardEnvVars() => Env.AnyEnvironmentVariablesSet(EnvironmentVariableNames.OtlpExporterEnvVars);

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
        s_metricsProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddMeter(Activities.Source.Name)
            .AddRuntimeInstrumentation();

        if (s_enableOtlpExporter)
        {
            s_metricsProviderBuilder.AddOtlpExporter();
        }

        s_tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: Product.Version); })
            .AddSource(Activities.Source.Name)
            .SetSampler(new AlwaysOnSampler());

        if (s_enableOtlpExporter)
        {
            s_tracerProviderBuilder.AddOtlpExporter();
        }

        if (!string.IsNullOrWhiteSpace(s_diskLogPath))
        {
            s_tracerProviderBuilder.AddInMemoryExporter(s_activities);
        }

#if MICROSOFT_ENABLE_TELEMETRY_AZURE_MONITOR
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

        if (s_metricsProvider is null || s_tracerProvider is null)
        {
            // Create a new OTel meter and tracer provider.
            // It is important to keep the provider instances active throughout the process lifetime.
            s_metricsProvider ??= s_metricsProviderBuilder.Build();
            s_tracerProvider ??= s_tracerProviderBuilder.Build();
        }

        CurrentSessionId ??= !string.IsNullOrEmpty(sessionId) ? sessionId : Guid.NewGuid().ToString();
        s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(CurrentSessionId);
    }

    /// <summary>
    /// Derives the parent activity context. Checks runtime properties first (set by the AOT
    /// bridge via <c>hostfxr_set_runtime_property_value</c>), then falls back to the
    /// <c>TRACEPARENT</c> / <c>TRACESTATE</c> environment variables.
    /// </summary>
    private static ActivityContext? GetParentActivityContext()
    {
        // Runtime properties take precedence — they are set by the AOT bridge when it
        // falls back to the managed CLI so that the managed spans become children of the
        // AOT-side main activity.
        var traceParent = AppContext.GetData(Activities.TRACEPARENT) as string;

        // Fall back to environment variables for external callers.
        if (string.IsNullOrEmpty(traceParent))
        {
            traceParent = Env.GetEnvironmentVariable(Activities.TRACEPARENT);
        }

        if (string.IsNullOrEmpty(traceParent))
        {
            return null;
        }

        var carrierMap = new Dictionary<string, IEnumerable<string>?> { { "traceparent", [traceParent] } };

        var traceState = AppContext.GetData(Activities.TRACESTATE) as string;
        if (string.IsNullOrEmpty(traceState))
        {
            traceState = Env.GetEnvironmentVariable(Activities.TRACESTATE);
        }

        if (!string.IsNullOrEmpty(traceState))
        {
            carrierMap.Add("tracestate", [traceState]);
        }

        ActivityContext? parentContext = null;
        // Use the propagator to extract the parent activity context and kind.
        // For some reason, this isn't set by the OTel SDK like docs say it should be.
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        parentContext = Propagators.DefaultTextMapPropagator.Extract(default, carrierMap, GetValueFromCarrier).ActivityContext;
        return parentContext;

        static IEnumerable<string>? GetValueFromCarrier(Dictionary<string, IEnumerable<string>?> carrier, string key) =>
            carrier.TryGetValue(key, out var value) ? value : null;
    }

    private static ActivityKind GetActivityKind(ActivityContext? parentActivityContext) =>
        parentActivityContext is ActivityContext { IsRemote: true } ? ActivityKind.Server : ActivityKind.Internal;

    public static void FlushProviders()
    {
        s_tracerProvider?.ForceFlush(s_flushTimeoutMs);
        s_metricsProvider?.ForceFlush(s_flushTimeoutMs);
    }

    public static void WriteLogIfNecessary()
    {
        if (!string.IsNullOrWhiteSpace(s_diskLogPath) && s_activities.Any())
        {
            TelemetryDiskLogger.WriteLog(s_diskLogPath, s_activities);
        }
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
