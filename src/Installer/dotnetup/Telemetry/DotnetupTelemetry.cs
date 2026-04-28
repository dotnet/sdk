// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Singleton telemetry manager for dotnetup.
/// Uses OpenTelemetry with Azure Monitor exporter (AOT compatible).
/// </summary>
public sealed class DotnetupTelemetry : IDisposable
{
    private static readonly Lazy<DotnetupTelemetry> s_instance = new(() => new DotnetupTelemetry());

    /// <summary>
    /// Gets the singleton instance of DotnetupTelemetry.
    /// </summary>
    public static DotnetupTelemetry Instance => s_instance.Value;

    /// <summary>
    /// ActivitySource for command-level telemetry.
    /// </summary>
    public static readonly ActivitySource CommandSource = new(
        "Microsoft.Dotnet.Bootstrapper",
        GetVersion());

    /// <summary>
    /// Connection string for Application Insights.
    /// </summary>
    private const string ConnectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";

    private const string TelemetryOptOutEnvVar = "DOTNET_CLI_TELEMETRY_OPTOUT";
    private const string StoragePathEnvVar = "DOTNET_CLI_TELEMETRY_STORAGE_PATH";
    private const string DisableTraceExportEnvVar = "DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT";
    private const string DiskLogPathEnvVar = "DOTNET_CLI_TELEMETRY_LOG_PATH";

    /// <summary>
    /// Opt-in env var that enables network export of OTel spans via the
    /// AzMonitor + OTLP trace exporters (Aspire-style perf debugging).
    /// Default-off because data-x ingests only the AppInsights <c>traces</c>
    /// table (fed by ILogger), not the span-fed tables; keeping spans
    /// in-process saves bandwidth, batch overhead, and offline retry blobs.
    /// </summary>
    private const string EnablePerfTraceEnvVar = "DOTNETUP_CLI_GET_PERF_TRACE";

    private static readonly string s_defaultStorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet", "TelemetryStorageService");

    private static readonly string? s_diskLogPath = GetDiskLogPath();

    private static string? GetDiskLogPath()
    {
        var path = Environment.GetEnvironmentVariable(DiskLogPathEnvVar);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Write to the same directory as the SDK log but with a distinct filename
        // to avoid read-modify-write conflicts between dotnet CLI and dotnetup.
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir ?? string.Empty, $"{name}-dotnetup{ext}");
    }

    private readonly TracerProvider? _tracerProvider;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;
    private readonly List<Activity> _activities = [];
    private bool _disposed;

    /// <summary>
    /// Gets whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    public string SessionId { get; }

    private DotnetupTelemetry()
    {
        SessionId = Guid.NewGuid().ToString();

        // Check opt-out (same env var as SDK).
        // Unlike the SDK, dotnetup sends telemetry from dev/test builds too —
        // distinguished by the dev.build=true tag in common properties.
        var optOutValue = Environment.GetEnvironmentVariable(TelemetryOptOutEnvVar);
        Enabled = !string.Equals(optOutValue, "1", StringComparison.Ordinal) &&
                  !string.Equals(optOutValue, "true", StringComparison.OrdinalIgnoreCase);

        if (!Enabled)
        {
            return;
        }

        // Register with the installation library so its telemetry flows through TrackEvent.
        Metrics.OnTrackEvent = TrackEvent;

        try
        {
            var disableExport = IsTruthy(Environment.GetEnvironmentVariable(DisableTraceExportEnvVar));
            var enablePerfTrace = IsTruthy(Environment.GetEnvironmentVariable(EnablePerfTraceEnvVar));
            var debugConsole = Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_DEBUG") == "1";
            var storageDirectory = ResolveStorageDirectory();
            var resource = BuildResource();

            _tracerProvider = BuildTracerProvider(resource, enablePerfTrace, disableExport, debugConsole, storageDirectory);
            _loggerFactory = BuildLoggerFactory(resource, disableExport, debugConsole, storageDirectory);
            _logger = _loggerFactory.CreateLogger("Microsoft.Dotnet.Bootstrapper");
        }
        catch (Exception)
        {
            // Telemetry should never crash the app
            Enabled = false;
        }
    }

    private static string ResolveStorageDirectory()
    {
        var environmentStoragePath = Environment.GetEnvironmentVariable(StoragePathEnvVar);
        return string.IsNullOrWhiteSpace(environmentStoragePath)
            ? s_defaultStorageDirectory
            : environmentStoragePath;
    }

    /// <summary>
    /// Builds the OTel <see cref="Resource"/> shared by tracer and logger so
    /// common process-level attributes (caller, OS, arch, machine id, session
    /// id, dev.build, ...) auto-stamp every span and log record.
    /// </summary>
    private ResourceBuilder BuildResource()
    {
        var commonAttrs = new List<KeyValuePair<string, object>>
        {
            new(TelemetryTagNames.Caller, "dotnetup"),
        };
        foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
        {
            commonAttrs.Add(attr);
        }
        return ResourceBuilder.CreateDefault()
            .AddService("dotnetup", serviceVersion: GetVersion())
            .AddAttributes(commonAttrs);
    }

    /// <summary>
    /// Builds the <see cref="TracerProvider"/>. Spans always run in-process
    /// (Activity.Current correlation + in-memory test seam); network export
    /// is opt-in via <c>DOTNETUP_CLI_GET_PERF_TRACE=1</c> because data-x
    /// ingests logs, not spans.
    /// </summary>
    private TracerProvider BuildTracerProvider(ResourceBuilder resource, bool enablePerfTrace, bool disableExport, bool debugConsole, string storageDirectory)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource("Microsoft.Dotnet.Bootstrapper")
            .AddSource("Microsoft.Dotnet.Installation")
            .AddInMemoryExporter(_activities)
            .SetSampler(new AlwaysOnSampler());

        // Both OTLP and AzMonitor are network sinks — gate them on the
        // common `disableExport` switch so tests / CI can opt out of all
        // network traffic with a single env var, then further gate on the
        // perf-trace opt-in (data-x ingests logs, not spans).
        if (enablePerfTrace && !disableExport)
        {
            builder.AddOtlpExporter();
            builder.AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = ConnectionString;
                o.EnableLiveMetrics = false;
                o.StorageDirectory = storageDirectory;
            });
        }

        if (debugConsole)
        {
            builder.AddConsoleExporter();
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds the <see cref="ILoggerFactory"/>. Every TrackedOperation
    /// completion and every <see cref="RecordException"/> emits one LogRecord
    /// through this factory; the AzMonitor log exporter routes it to the
    /// AppInsights <c>traces</c> table — the only table data-x ingests. The
    /// OTel logger auto-stamps Activity.Current's TraceId/SpanId so AzMonitor
    /// can map them to operation_Id/operation_ParentId for cross-row
    /// correlation.
    /// </summary>
    private static ILoggerFactory BuildLoggerFactory(ResourceBuilder resource, bool disableExport, bool debugConsole, string storageDirectory)
    {
        return LoggerFactory.Create(lb =>
        {
            lb.AddOpenTelemetry(o =>
            {
                o.IncludeScopes = true;
                o.IncludeFormattedMessage = true;
                o.ParseStateValues = true;
                o.SetResourceBuilder(resource);

                // Both AzMonitor (the prod sink for the AppInsights `traces`
                // table) and OTLP (Aspire / local collector) are network
                // sinks — gate both on `disableExport` so tests/CI can opt
                // out of all network traffic with a single env var.
                if (!disableExport)
                {
                    o.AddAzureMonitorLogExporter(amo =>
                    {
                        amo.ConnectionString = ConnectionString;
                        amo.EnableLiveMetrics = false;
                        amo.StorageDirectory = storageDirectory;
                    });
                    o.AddOtlpExporter();
                }

                if (debugConsole)
                {
                    o.AddConsoleExporter();
                }
            });
        });
    }

    /// <summary>
    /// Starts the per-command operation. The Activity OperationName is
    /// <c>command/{commandName}</c> for in-process correlation; the LogRecord
    /// Message is the stable string <c>dotnetup/command</c> with the
    /// subcommand discriminator on the <c>command.name</c> property. A single
    /// stable Message keeps one GDPR-catalog entry covering every (existing
    /// and future) subcommand — per-subcommand Messages caused new
    /// subcommands to silently fall off the classified <c>RawEventsTraces</c>
    /// table until the catalog was hand-updated.
    /// </summary>
    internal TrackedOperation StartTrackedCommand(string commandName)
    {
        var activity = Enabled
            ? CommandSource.StartActivity($"command/{commandName}", ActivityKind.Internal)
            : null;

        var op = new TrackedOperation(activity, "command", TrackEvent);
        op.Tag(TelemetryTagNames.CommandName, commandName);
        return op;
    }

    /// <summary>
    /// Starts a tracked root process operation.
    /// </summary>
    internal TrackedOperation StartTrackedProcess(string name)
    {
        var activity = Enabled
            ? CommandSource.StartActivity(name, ActivityKind.Internal)
            : null;

        return new TrackedOperation(activity, "process/complete", TrackEvent);
    }

    /// <summary>
    /// Records an exception on the failing operation, propagates the same
    /// error.* tags up the activity ancestor chain so each parent's
    /// completion log carries the failure signal, and emits one explicit
    /// severity=Error LogRecord at the failure site.
    /// </summary>
    /// <remarks>
    /// Ancestor propagation is first-failure-wins: once an ancestor has
    /// <c>error.type</c> set we don't overwrite, preserving the true root
    /// cause when later siblings fail. The error LogRecord is emitted with
    /// <c>exception: null</c> on purpose — the AzMonitor log exporter routes
    /// records with a non-null <see cref="Exception"/> into the AppInsights
    /// <c>exceptions</c> table, which data-x doesn't ingest. Exception type
    /// and stack trace ride as structured props so the failure is fully
    /// described in <c>traces</c>.
    /// </remarks>
    /// <param name="operation">The tracked operation to tag.</param>
    /// <param name="ex">The exception to record.</param>
    /// <param name="errorCode">Optional error code override.</param>
    internal void RecordException(TrackedOperation? operation, Exception ex, string? errorCode = null)
    {
        if (operation == null || !Enabled)
        {
            return;
        }

        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
        // ApplyErrorTags is the single source of truth for the error.* tag
        // bag; PropagateErrorToActivityChain calls it on the failing activity
        // AND every ancestor. The completion log picks them up automatically
        // because BuildCompletionState folds Activity.TagObjects into state.
        PropagateErrorToActivityChain(operation.Activity, errorInfo, errorCode);
        operation.SetStatus(ActivityStatusCode.Error, ex.Message);
        EmitErrorLog(operation.Activity);
    }

    /// <summary>
    /// Tags the failing activity and walks up <see cref="Activity.Parent"/>,
    /// applying the same error.* tags to each ancestor that doesn't already
    /// carry an <c>error.type</c> (first-failure-wins per ancestor).
    /// </summary>
    private static void PropagateErrorToActivityChain(Activity? failingActivity, ExceptionErrorInfo errorInfo, string? errorCode)
    {
        if (failingActivity is null)
        {
            return;
        }

        ErrorCodeMapper.ApplyErrorTags(failingActivity, errorInfo, errorCode);
        failingActivity.SetTag("error.first_failure_stage", failingActivity.OperationName);

        for (var ancestor = failingActivity.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.GetTagItem("error.type") is not null)
            {
                continue;
            }

            ErrorCodeMapper.ApplyErrorTags(ancestor, errorInfo, errorCode);
            ancestor.SetTag("error.first_failure_stage", failingActivity.OperationName);
        }
    }

    /// <summary>
    /// Emits one explicit severity=Error LogRecord (<c>dotnetup/error</c>) so
    /// data-x has a discoverable Error signal in <c>traces</c>. The owning
    /// op's completion log carries the same tags at severity=Information.
    /// </summary>
    private void EmitErrorLog(Activity? failingActivity)
    {
        if (_logger is null || failingActivity is null)
        {
            return;
        }

        try
        {
            var elapsedMs = (DateTime.UtcNow - failingActivity.StartTimeUtc).TotalMilliseconds;
            var state = BuildCompletionState("error", failingActivity, s_emptyTags, elapsedMs);
            _logger.Log(
                LogLevel.Error,
                new EventId(0, "dotnetup/error"),
                state,
                exception: null,
                formatter: static (_, _) => "dotnetup/error");
        }
        catch
        {
            // Telemetry should never crash the app.
        }
    }

    private static readonly Dictionary<string, string?> s_emptyTags = [];

    /// <summary>
    /// Flushes the tracer provider. The <see cref="ILoggerFactory"/>'s
    /// <c>BatchLogRecordExportProcessor</c> only flushes on
    /// <see cref="Dispose"/>; mid-process callers rely on its
    /// <c>ScheduledDelay</c> and the AzMonitor exporter's
    /// <c>StorageDirectory</c> retry queue to cover crashes between flushes.
    /// </summary>
    /// <param name="timeoutMilliseconds">Maximum time to wait for flush (default 5 seconds).</param>
    public void Flush(int timeoutMilliseconds = 5000)
    {
        try
        {
            _tracerProvider?.ForceFlush(timeoutMilliseconds);
        }
        catch
        {
            // Never let telemetry flush failures crash the app
        }
    }

    /// <summary>
    /// Writes collected activities to disk if DOTNET_CLI_TELEMETRY_LOG_PATH is set.
    /// Same format as the SDK's TelemetryDiskLogger.
    /// </summary>
    public void WriteLogIfNecessary()
    {
        if (!string.IsNullOrWhiteSpace(s_diskLogPath) && _activities.Count > 0)
        {
            TelemetryDiskLogger.WriteLog(s_diskLogPath, _activities);
        }
    }

    /// <summary>
    /// Dispose callback for <see cref="TrackedOperation"/>. Emits the
    /// completion LogRecord, then stops the activity.
    /// </summary>
    /// <remarks>
    /// <strong>Critical ordering:</strong> Log() runs <em>before</em> Stop().
    /// <see cref="Activity.Current"/> is only this span until
    /// <see cref="Activity.Stop"/> pops it (the OTel logger needs it for
    /// TraceId/SpanId stamping), and <see cref="Activity.Duration"/> is
    /// <see cref="TimeSpan.Zero"/> until Stop — so we capture
    /// <c>elapsedMs</c> manually from <see cref="Activity.StartTimeUtc"/>.
    /// That manual value feeds <c>operation.duration_ms</c> on the
    /// <c>traces</c> row (data-x signal); the post-Stop
    /// <see cref="Activity.Duration"/> feeds the span envelope (Aspire /
    /// opt-in <c>DOTNETUP_CLI_GET_PERF_TRACE</c>).
    /// </remarks>
    private void TrackEvent(string eventName, Activity? activity, IDictionary<string, string?> storedTags)
    {
        if (!Enabled)
        {
            activity?.Stop();
            return;
        }

        try
        {
            EmitCompletionLog(eventName, activity, storedTags);
        }
        catch
        {
            // Telemetry should never crash the app.
        }
        finally
        {
            activity?.Stop();
        }
    }

    /// <summary>
    /// Builds the structured state for one <c>traces</c> row and emits it via
    /// <see cref="ILogger"/>. The Azure Monitor log exporter routes this to the
    /// AppInsights <c>traces</c> table; the OTel logger stamps
    /// <c>TraceId</c>/<c>SpanId</c> from <see cref="Activity.Current"/> for
    /// correlation.
    /// </summary>
    private void EmitCompletionLog(string eventName, Activity? activity, IDictionary<string, string?> storedTags)
    {
        if (_logger is null || activity is null)
        {
            return;
        }

        var elapsedMs = (DateTime.UtcNow - activity.StartTimeUtc).TotalMilliseconds;
        var state = BuildCompletionState(eventName, activity, storedTags, elapsedMs);
        var formattedMessage = $"dotnetup/{eventName}";
        _logger.Log(
            LogLevel.Information,
            new EventId(0, formattedMessage),
            state,
            exception: null,
            formatter: (_, _) => formattedMessage);
    }

    /// <summary>
    /// Builds the structured state for one <c>traces</c> row. Walks ancestor
    /// activities (root first) to inherit <c>command.*</c> tags, then
    /// overlays the current activity's own tags and the op's stored tags.
    /// Last writer wins; computed fields (<c>operation.name</c>,
    /// <c>operation.duration_ms</c>, <c>operation.parent_name</c>) are added
    /// last. Common process-level attrs (caller, OS, dev.build, machine id,
    /// session id) live on the OTel <see cref="Resource"/> and auto-stamp —
    /// they're not added here.
    /// </summary>
    private static List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        IDictionary<string, string?> storedTags,
        double elapsedMs)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Walk root → ... → immediate parent so the root's command.* tags
        // are written first and survive unless an inner activity explicitly
        // overrides them.
        var ancestors = new List<Activity>();
        for (var a = activity.Parent; a is not null; a = a.Parent)
        {
            ancestors.Add(a);
        }
        for (int i = ancestors.Count - 1; i >= 0; i--)
        {
            foreach (var tag in ancestors[i].TagObjects)
            {
                if (tag.Key.StartsWith("command.", StringComparison.Ordinal))
                {
                    state[tag.Key] = tag.Value;
                }
            }
        }

        // Own activity tags (override ancestor inherited values).
        foreach (var tag in activity.TagObjects)
        {
            state[tag.Key] = tag.Value;
        }

        // TrackedOperation.Tag mirrors to Activity.SetTag in production so
        // these are usually duplicates of TagObjects above; this overlay
        // covers tests and any path with a null activity.
        foreach (var tag in storedTags)
        {
            state[tag.Key] = tag.Value;
        }

        state["operation.name"] = $"dotnetup/{eventName}";
        state["operation.duration_ms"] = elapsedMs.ToString(CultureInfo.InvariantCulture);
        if (activity.Parent is { } parent)
        {
            state["operation.parent_name"] = parent.OperationName;
        }

        var list = new List<KeyValuePair<string, object?>>(state.Count);
        foreach (var kv in state)
        {
            list.Add(new KeyValuePair<string, object?>(kv.Key, kv.Value));
        }
        return list;
    }

    /// <summary>
    /// Disposes the telemetry provider.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Dispose the logger before the tracer so its
        // BatchLogRecordExportProcessor flushes queued LogRecords (the
        // primary data-x signal) before shutdown begins.
        try { _loggerFactory?.Dispose(); } catch { /* never crash on telemetry */ }

        _tracerProvider?.Dispose();
        _disposed = true;
    }

    private static string GetVersion()
    {
        return typeof(DotnetupTelemetry).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.Ordinal) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
