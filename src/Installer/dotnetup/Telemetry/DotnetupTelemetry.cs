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
    /// Opt-in env var to enable network export of OTel spans (AzureMonitor /
    /// OTLP trace exporters). The data-x ingestion pipeline reads only the
    /// AppInsights <c>traces</c> table (fed by ILogger logs), never the
    /// <c>dependencies</c> / <c>requests</c> / <c>exceptions</c> tables that
    /// the trace exporter feeds. Default-off saves bandwidth and storage retry
    /// blobs; opt-in via <c>DOTNETUP_CLI_GET_PERF_TRACE=1</c> for Aspire-style
    /// perf debugging.
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
    /// Builds the single OTel <see cref="Resource"/> shared by the tracer and
    /// logger. Common process-level attributes (caller, OS, arch, machine id,
    /// session id, dev.build, ...) live here so every span and log record
    /// carries them automatically — no per-span loop.
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
    /// (Activity.Current correlation + in-memory test seam) but network
    /// export is gated behind <c>DOTNETUP_CLI_GET_PERF_TRACE=1</c> because
    /// data-x reads only the <c>traces</c> table (logs), not
    /// <c>dependencies</c> (spans). Default-off saves bandwidth, batch-
    /// processor overhead, and storage retry blobs on offline machines.
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
    /// completion (and every explicit error in <see cref="RecordException"/>)
    /// emits one LogRecord through this factory. The Azure Monitor log
    /// exporter routes records to the AppInsights <c>traces</c> table — the
    /// only table data-x ingests. The OTel logger automatically stamps
    /// Activity.Current's TraceId/SpanId onto each LogRecord, which
    /// AzMonitor maps to operation_Id/operation_ParentId for cross-row
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
    /// Starts a tracked command operation that emits a completion log on dispose.
    /// </summary>
    /// <remarks>
    /// Common process-level attributes (caller, OS, arch, machine id, session id)
    /// live on the OTel Resource (set in the ctor) so they auto-stamp every
    /// span and log record — no per-span loop needed here.
    /// </remarks>
    internal TrackedOperation StartTrackedCommand(string commandName)
    {
        var activity = Enabled
            ? CommandSource.StartActivity($"command/{commandName}", ActivityKind.Internal)
            : null;

        var op = new TrackedOperation(activity, $"command/{commandName}", TrackEvent);
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
    /// Records an exception on the given operation and propagates the categorical
    /// failure signal up the activity ancestor chain so every parent's eventual
    /// completion log carries it. Emits one explicit error log row in `traces`
    /// at the failure site.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ancestor propagation is "first-failure-wins per ancestor": once an
    /// ancestor has <c>error.type</c> set we don't overwrite, so a later
    /// sibling failure within the same parent doesn't clobber the true root
    /// cause. The failing op's own tags are always overwritten (last writer
    /// wins on the failing op itself).
    /// </para>
    /// <para>
    /// Pass <c>exception: null</c> to <c>ILogger.Log</c> deliberately: the
    /// Azure Monitor log exporter routes any LogRecord with a non-null
    /// <c>Exception</c> into the AppInsights <c>exceptions</c> table, which
    /// data-x doesn't ingest. Stack trace and exception type ride as
    /// structured props in <c>state</c> so the failure is fully described
    /// in <c>traces</c> alone.
    /// </para>
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
        // ErrorCodeMapper.ApplyErrorTags is the single source of truth for the
        // shape of the error.* tag bag. PropagateErrorToActivityChain calls it
        // on the failing activity AND every ancestor, so the failing op's
        // completion log (which folds Activity.TagObjects into the LogRecord
        // state via BuildCompletionState) automatically carries every
        // error.* tag — no separate "mirror onto TrackedOperation" step needed.
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
    /// Emits one explicit <c>dotnetup/error</c> LogRecord at severity
    /// <see cref="LogLevel.Error"/>. The failing op's completion log carries
    /// the same tags but at <see cref="LogLevel.Information"/>; this row gives
    /// data-x a discoverable severity=Error signal.
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
    /// Flushes any pending telemetry. The OTel <see cref="ILoggerFactory"/>'s
    /// <c>BatchLogRecordExportProcessor</c> is only flushed on
    /// <see cref="Dispose"/> (its shutdown path), so callers using this method
    /// mid-process rely on the BatchProcessor's <c>ScheduledDelay</c> + the
    /// AzMonitor exporter's <c>StorageDirectory</c> retry queue to cover
    /// untimely crashes between flushes.
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
    /// Dispose callback for <see cref="TrackedOperation"/>. Emits the operation's
    /// completion log into the AppInsights <c>traces</c> table via ILogger,
    /// then stops the activity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Critical ordering:</strong> we Log() <em>before</em> Stop() because:
    /// </para>
    /// <list type="number">
    ///   <item><description><see cref="Activity.Current"/> is this span only until <see cref="Activity.Stop"/> pops it to the parent — the OTel logger needs it to stamp TraceId/SpanId on the LogRecord for correlation.</description></item>
    ///   <item><description><see cref="Activity.Duration"/> is <see cref="TimeSpan.Zero"/> until <c>Stop()</c>, so we capture <c>elapsedMs</c> manually from <see cref="Activity.StartTimeUtc"/> before logging.</description></item>
    /// </list>
    /// <para>
    /// The manually-captured <c>elapsedMs</c> feeds the <c>operation.duration_ms</c>
    /// custom dimension on the <c>traces</c> row (data-x signal). The post-Stop
    /// <see cref="Activity.Duration"/> feeds the OTLP/AzMonitor span envelope
    /// (Aspire / opt-in <c>DOTNETUP_CLI_GET_PERF_TRACE</c> signal). Different sinks,
    /// same wall-clock value.
    /// </para>
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
    /// Single source of truth for what goes into a <c>traces</c> row's structured
    /// state. Walks the activity ancestor chain so child operations inherit
    /// <c>command.*</c> / <c>command.args.*</c> tags from the root command, then
    /// overlays the activity's own tags and the op's stored tags.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolution order (last writer wins):
    /// </para>
    /// <list type="number">
    ///   <item><description>Ancestor activities (root → ... → immediate parent) — closest-to-root applied first so <c>command.*</c> from the root survives unless explicitly overridden.</description></item>
    ///   <item><description>The current activity's own <see cref="Activity.TagObjects"/>.</description></item>
    ///   <item><description>The op's <c>storedTags</c> (the bag accumulated via <see cref="TrackedOperation.Tag"/>).</description></item>
    ///   <item><description>Computed fields: <c>operation.duration_ms</c>, <c>operation.name</c>, <c>operation.parent_name</c>.</description></item>
    /// </list>
    /// <para>
    /// Common process-level attrs (caller, OS, dev.build, machine id, session id)
    /// are <em>not</em> added here — they live on the OTel <see cref="Resource"/>
    /// and are auto-stamped by the logger provider.
    /// </para>
    /// </remarks>
    private static List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        IDictionary<string, string?> storedTags,
        double elapsedMs)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Walk root → ... → immediate parent. Closest-to-root applied first
        // so the root command's command.args.* survive unless an inner
        // activity intentionally overrides them.
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

        // Stored tags accumulated via TrackedOperation.Tag — already mirrored
        // onto Activity.SetTag in production, but this covers tests and any
        // path that bypasses the mirror.
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

        // Dispose the logger first so its BatchLogRecordExportProcessor
        // shuts down and flushes any queued log records to AzMonitor.
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
