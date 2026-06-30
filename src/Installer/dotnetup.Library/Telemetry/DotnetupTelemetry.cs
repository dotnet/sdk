// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.Extensions.DependencyInjection;
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
        Constants.Telemetry.BootstrapperSourceName,
        GetVersion());

    private static readonly string s_defaultStorageDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "dotnetup",
            "TelemetryStorageService");

    private static readonly string? s_diskLogPath = GetDiskLogPath();

    private static string? GetDiskLogPath()
    {
        var path = Environment.GetEnvironmentVariable(Constants.Telemetry.DiskLogPathEnvVar);
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
    private readonly ServiceProvider? _services;
    private readonly LoggerProvider? _loggerProvider;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;
    private readonly List<Activity> _activities = [];
    /// <summary>
    /// Snapshot of process-level common properties (os.type, device.id,
    /// session.id, dev.build, ...). These also live on the OTel
    /// <see cref="Resource"/>, but the AzMonitor log exporter only maps a
    /// fixed subset of Resource attrs to AppInsights envelope fields and
    /// drops the rest — so we re-stamp them on every LogRecord state in
    /// <c>BuildCompletionState</c> to ensure they reach the
    /// <c>traces</c> table (the data-x signal).
    /// </summary>
    private readonly KeyValuePair<string, object?>[] _commonProperties = [];

    /// <summary>
    /// Gets whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// True when this process is running in a CI environment, as detected by
    /// <see cref="TelemetryCommonProperties.IsCIEnvironment"/>. CI runs are
    /// one-and-done — there's no follow-up invocation to drain the
    /// AzMonitor offline store — so callers can use this to allocate a
    /// larger flush budget on exit. Interactive (non-CI) runs stay on the
    /// default budget so user exit performance is unaffected.
    /// </summary>
    public bool IsOneAndDoneEnvironment { get; }

    /// <summary>
    /// The telemetry name of the subcommand the current invocation is running
    /// (e.g. <c>sdk/install</c>, <c>print-env-script</c>), captured when
    /// <see cref="StartTrackedCommand"/> is called. Null until a command starts
    /// (parse-only / root-only invocations). Used to fast-path the flush budget
    /// for latency-critical shell-startup commands.
    /// </summary>
    internal string? CurrentCommandName { get; private set; }

    private DotnetupTelemetry()
    {
        SessionId = Guid.NewGuid().ToString();
        IsOneAndDoneEnvironment = TelemetryCommonProperties.IsCIEnvironment;

        // Check opt-out (same env var as SDK).
        // Unlike the SDK, dotnetup sends telemetry from dev/test builds too —
        // distinguished by the dev.build=true tag in common properties.
        Enabled = !IsTruthy(Environment.GetEnvironmentVariable(Constants.Telemetry.TelemetryOptOutEnvVar));

        if (!Enabled)
        {
            return;
        }

        // Register with the installation library so its telemetry flows through TrackEvent.
        Metrics.OnTrackEvent = TrackEvent;

        try
        {
            var disableExport = IsTruthy(Environment.GetEnvironmentVariable(Constants.Telemetry.DisableTraceExportEnvVar));
            var enablePerfTrace = IsTruthy(Environment.GetEnvironmentVariable(Constants.Telemetry.EnablePerfTraceEnvVar));
            var enableOtlpExporter = IsOtlpExporterEnabled(disableExport);
            var debugConsole = Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_DEBUG") == "1";
            var storageDirectory = ResolveStorageDirectory();
            var commonAttrs = BuildCommonAttributes();
            _commonProperties = ToLogStateProperties(commonAttrs);
            var resource = BuildResource(commonAttrs);

            _tracerProvider = BuildTracerProvider(resource, enablePerfTrace, enableOtlpExporter, disableExport, debugConsole, storageDirectory);
            _services = BuildLoggingServices(resource, enableOtlpExporter, disableExport, debugConsole, storageDirectory);
            _loggerProvider = _services.GetService<LoggerProvider>();
            _loggerFactory = _services.GetRequiredService<ILoggerFactory>();
            _logger = _loggerFactory.CreateLogger(Constants.Telemetry.BootstrapperSourceName);
        }
        catch (Exception)
        {
            // Telemetry should never crash the app
            Enabled = false;
        }
    }

    private static string ResolveStorageDirectory()
    {
        var environmentStoragePath = Environment.GetEnvironmentVariable(Constants.Telemetry.StoragePathEnvVar);
        return string.IsNullOrWhiteSpace(environmentStoragePath)
            ? s_defaultStorageDirectory
            : environmentStoragePath;
    }

    /// <summary>
    /// Builds the list of common process-level attributes (caller, os.type,
    /// device.id, session.id, dev.build, ...). Used both as OTel
    /// <see cref="Resource"/> attributes (for spans / opt-in perf trace
    /// export) and as per-LogRecord state stamps (for the AppInsights
    /// <c>traces</c> table that data-x ingests).
    /// </summary>
    private List<KeyValuePair<string, object>> BuildCommonAttributes()
    {
        var commonAttrs = new List<KeyValuePair<string, object>>
        {
            new(TelemetryTagNames.Caller, "dotnetup"),
        };
        foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
        {
            commonAttrs.Add(attr);
        }
        return commonAttrs;
    }

    private static KeyValuePair<string, object?>[] ToLogStateProperties(List<KeyValuePair<string, object>> commonAttrs) =>
        commonAttrs.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)).ToArray();

    /// <summary>
    /// Builds the OTel <see cref="Resource"/> shared by tracer and logger.
    /// Note: the AzMonitor log exporter only maps a fixed subset of Resource
    /// attrs (<c>service.name</c>, <c>service.version</c>,
    /// <c>service.instance.id</c>) to AppInsights envelope fields and drops
    /// the rest — so common attrs are also stamped per-LogRecord in
    /// <c>BuildCompletionState</c> to reach the <c>traces</c> table.
    /// On spans (opt-in perf trace) Resource attrs auto-stamp normally.
    /// </summary>
    private static ResourceBuilder BuildResource(List<KeyValuePair<string, object>> commonAttrs)
    {
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
    private TracerProvider BuildTracerProvider(ResourceBuilder resource, bool enablePerfTrace, bool enableOtlpExporter, bool disableExport, bool debugConsole, string storageDirectory)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(Constants.Telemetry.BootstrapperSourceName)
            .AddSource(Metrics.SourceName)
            .SetSampler(new AlwaysOnSampler());

        if (!string.IsNullOrWhiteSpace(s_diskLogPath))
        {
            builder.AddInMemoryExporter(_activities);
        }

        // Span network export is off by default because data-x ingests logs,
        // not spans. AzMonitor spans are gated on the perf-trace opt-in;
        // OTLP spans additionally require the SDK-style OTLP exporter opt-in.
        if (enablePerfTrace && !disableExport)
        {
            if (enableOtlpExporter)
            {
                builder.AddOtlpExporter();
            }
            builder.AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = Constants.Telemetry.ConnectionString;
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
    /// <remarks>
    /// Built via <see cref="ServiceCollection"/> (rather than
    /// <c>LoggerFactory.Create</c>) so the underlying
    /// <see cref="OpenTelemetry.Logs.LoggerProvider"/> is resolvable via DI.
    /// We need a direct handle on the provider so <see cref="Flush"/> can
    /// call <c>ForceFlush</c> on it — without that, the
    /// <c>BatchLogRecordExportProcessor</c> only drains on
    /// <see cref="Dispose"/>, and CI runs (one-and-done — no follow-up
    /// invocation to retry from the AzMonitor offline store) need a
    /// pre-shutdown drain with an extended budget.
    /// </remarks>
    private static ServiceProvider BuildLoggingServices(ResourceBuilder resource, bool enableOtlpExporter, bool disableExport, bool debugConsole, string storageDirectory)
    {
        var services = new ServiceCollection();
        services.AddLogging(lb =>
        {
            lb.AddOpenTelemetry(o =>
            {
                o.IncludeScopes = true;
                o.IncludeFormattedMessage = true;
                o.ParseStateValues = true;
                o.SetResourceBuilder(resource);

                // AzMonitor is the prod sink for the AppInsights `traces`
                // table. OTLP is only for explicitly enabled local or
                // collector scenarios.
                if (!disableExport)
                {
                    o.AddAzureMonitorLogExporter(amo =>
                    {
                        amo.ConnectionString = Constants.Telemetry.ConnectionString;
                        amo.EnableLiveMetrics = false;
                        amo.StorageDirectory = storageDirectory;
                    });
                    if (enableOtlpExporter)
                    {
                        o.AddOtlpExporter();
                    }
                }

                if (debugConsole)
                {
                    o.AddConsoleExporter();
                }
            });
        });
        return services.BuildServiceProvider();
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
        CurrentCommandName = commandName;
        var activity = Enabled
            ? CommandSource.StartActivity($"command/{commandName}", ActivityKind.Internal)
            : null;

        var op = new TrackedOperation(activity, "command", TrackEvent);
        op.Tag(TelemetryTagNames.CommandName, commandName);
        return op;
    }

    /// <summary>
    /// Starts the root <see cref="TrackedOperation"/> for the entire
    /// dotnetup invocation. Disposed in <c>Program.Main</c>'s
    /// <c>finally</c>; emits the <c>dotnetup/root</c> LogRecord that
    /// carries the wall-clock <c>operation.duration_ms</c> for the whole
    /// process and any <c>error.*</c> tags propagated up from a failing
    /// command (or attached directly when an exception escapes
    /// <see cref="CommandBase"/>'s catch). Paired with the per-command
    /// <c>dotnetup/command</c> rows: "root" = whole process, "command" =
    /// one subcommand.
    /// </summary>
    internal TrackedOperation StartTrackedProcess(string name)
    {
        var activity = Enabled
            ? CommandSource.StartActivity(name, ActivityKind.Internal)
            : null;

        return new TrackedOperation(activity, "root", TrackEvent);
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
    internal void RecordException(TrackedOperation operation, Exception ex, string? errorCode = null)
    {
        if (!Enabled)
        {
            return;
        }

        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
        // Single hand-off into the propagation/application pipeline:
        // primary error.* tags spread up the ancestor chain (first-failure-wins
        // per ancestor), additional failures land only on the failing activity
        // (the command row) so the root row stays uncluttered with batch detail.
        PropagateErrorToActivityChain(operation.Activity, errorInfo, errorCode);
        // Use a non-PII description (the classified error type) rather than
        // ex.Message — the latter can leak user paths / values into exported
        // span status when the perf-trace exporter is enabled.
        operation.SetStatus(ActivityStatusCode.Error, errorInfo.ErrorType);
    }

    /// <summary>
    /// Tags the failing activity and walks up <see cref="Activity.Parent"/>,
    /// applying the same error.* tags to each ancestor that doesn't already
    /// carry an <c>error.type</c> (first-failure-wins per ancestor).
    /// Additional-failure tags (a JSON list of sibling failures attached via
    /// <see cref="ExceptionExtensions.AttachAdditionalFailure"/>) land only
    /// on the failing activity — they describe a batch loop on this command
    /// row, not the whole invocation, so duplicating onto ancestors would
    /// just inflate payload size without adding signal.
    /// </summary>
    private static void PropagateErrorToActivityChain(Activity? failingActivity, ExceptionErrorInfo errorInfo, string? errorCode)
    {
        if (failingActivity is null)
        {
            return;
        }

        ErrorCodeMapper.ApplyErrorTags(failingActivity, errorInfo, errorCode);
        ErrorCodeMapper.ApplyAdditionalFailureTags(failingActivity, errorInfo);
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
    /// Default flush budget (ms) for interactive / shell-startup exits. Enough
    /// to hand the (possibly cold) AppInsights POST to the batch processor so it
    /// can spill to the offline store for a later run, without noticeably
    /// delaying exit. Matches Aspire's CLI shutdown budget
    /// (TelemetryManager.ShutDownTimeoutMilliseconds = 200).
    /// </summary>
    private const int DefaultFlushTimeoutMs = 200;

    /// <summary>
    /// Durable flush budget (ms) for one-and-done environments (CI / piped /
    /// non-interactive) that have no guaranteed next run to drain the offline
    /// store, and for the failure path. A ceiling, not a fixed wait: ForceFlush
    /// returns as soon as the queue drains, so happy-path runs rarely pay it.
    /// 5000 covers the high end of observed cold AppInsights POST latency.
    /// </summary>
    private const int DurableFlushTimeoutMs = 5000;

    /// <summary>
    /// True when the current invocation is the latency-critical shell-startup
    /// command (<c>print-env-script</c>), which is sourced from the user's shell
    /// profile on every new shell. Its output is redirected (eval/source), which
    /// would otherwise route it to the durable budget — so it is fast-pathed to
    /// keep shell startup snappy even on a slow network.
    /// </summary>
    private bool IsShellStartupCommand =>
        string.Equals(CurrentCommandName, "print-env-script", StringComparison.Ordinal);

    /// <summary>
    /// Returns the on-exit flush budget (ms) for the current run. Precedence:
    /// <list type="number">
    /// <item>A non-negative <c>DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS</c> override (validation knob).</item>
    /// <item>Failure (<paramref name="exitCode"/> != 0): the durable budget, so error rows reach AppInsights even on shell-init / interactive paths (failures there should be rare).</item>
    /// <item>Shell-startup command (print-env-script): the default budget — even though its output is redirected — so a slow network can't delay shell startup; frequent invocations drain any miss from the offline store next run.</item>
    /// <item>Interactive foreground (not CI, not redirected): the default budget; the persistent offline store drains on the user's next command.</item>
    /// <item>Otherwise (CI / piped / non-interactive — effectively one-and-done with no guaranteed next run): the durable budget, to deliver the row live.</item>
    /// </list>
    /// </summary>
    internal int GetFlushTimeoutMs(int exitCode)
    {
        var overrideValue = Environment.GetEnvironmentVariable(Constants.Telemetry.FlushTimeoutOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overrideValue)
            && int.TryParse(overrideValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var forced)
            && forced >= 0)
        {
            return forced;
        }

        // Failure always spends the durable budget so the error row is delivered
        // live; ForceFlush still returns early once the queue drains.
        if (exitCode != 0)
        {
            return DurableFlushTimeoutMs;
        }

        // Shell-startup hot path: keep new-shell latency bounded even though the
        // command's output is redirected.
        if (IsShellStartupCommand)
        {
            return DefaultFlushTimeoutMs;
        }

        // Interactive foreground use: keep exit snappy; the persistent offline
        // store drains on the user's next command.
        if (!IsOneAndDoneEnvironment && !Console.IsOutputRedirected)
        {
            return DefaultFlushTimeoutMs;
        }

        // CI / piped / non-interactive: one-and-done with no guaranteed next run
        // to drain the offline store, so spend the durable budget.
        return DurableFlushTimeoutMs;
    }

    /// <summary>
    /// Production flush entrypoint. Computes the budget from the process exit
    /// code and the current environment (see <see cref="GetFlushTimeoutMs(int)"/>),
    /// then drains the providers. Returns as soon as the queues are empty — the
    /// budget is just a ceiling — so a larger budget never adds latency on
    /// happy-path runs. Whatever doesn't drain in time falls back to the
    /// AzMonitor exporter's <c>StorageDirectory</c> retry queue. Call once, on
    /// process exit.
    /// </summary>
    /// <param name="exitCode">The process exit code; a non-zero value selects the durable budget.</param>
    public void Flush(int exitCode)
    {
        FlushCore(GetFlushTimeoutMs(exitCode));
    }

    /// <summary>
    /// Flushes with an explicit budget, bypassing environment classification.
    /// For tests and diagnostics only.
    /// </summary>
    internal void FlushWithTimeout(int timeoutMilliseconds)
    {
        FlushCore(timeoutMilliseconds);
    }

    /// <summary>
    /// Flushes the providers against a single shared deadline. ForceFlush is
    /// synchronous and blocking, so flushing the logger and tracer with the
    /// full budget each would let a dead network cost up to 2× the budget.
    /// Instead the logger goes first with the full budget — the completion
    /// LogRecord is the data-x signal (AppInsights <c>traces</c>) — and the
    /// tracer gets only the time left over (spans are secondary: in-process by
    /// default, network-exported only under the perf-trace opt-in).
    /// </summary>
    private void FlushCore(int timeoutMilliseconds)
    {
        var budget = Math.Max(0, timeoutMilliseconds);
        var deadline = Environment.TickCount64 + budget;

        try
        {
            // Logger first: the primary data-x signal. Each LogRecord is fully
            // decorated by BuildCompletionState at _logger.Log(...) time, so
            // ForceFlush only has to drain an already-self-described queue.
            _loggerProvider?.ForceFlush(budget);
        }
        catch
        {
            // Never let telemetry flush failures crash the app.
        }

        try
        {
            // Tracer gets the leftover budget so the two flushes share one
            // deadline rather than summing to 2× on a slow network.
            var remaining = (int)Math.Clamp(deadline - Environment.TickCount64, 0, budget);
            _tracerProvider?.ForceFlush(remaining);
        }
        catch
        {
            // Never let telemetry flush failures crash the app.
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
    private void TrackEvent(string eventName, Activity? activity)
    {
        if (!Enabled)
        {
            activity?.Stop();
            return;
        }

        try
        {
            EmitCompletionLog(eventName, activity);
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
    private void EmitCompletionLog(string eventName, Activity? activity)
    {
        if (_logger is null || activity is null)
        {
            return;
        }

        var elapsedMs = (DateTime.UtcNow - activity.StartTimeUtc).TotalMilliseconds;
        var state = BuildCompletionState(eventName, activity, elapsedMs);
        var formattedMessage = $"dotnetup/{eventName}";

        var eventIdInt = DeriveEventIdFromSpanId(activity);

        _logger.Log(
            LogLevel.Information,
            // each event must have a unique id otherwise the new 'router' lens service added by devdiv data on 5/15/2026 will try to dedupe it and 'drop' the data
            new EventId(eventIdInt, formattedMessage),
            state,
            exception: null,
            formatter: (_, _) => formattedMessage);
    }

    /// <summary>
    /// Derives a unique <see cref="EventId"/> integer from the lower 32 bits of an
    /// <see cref="Activity"/>'s <see cref="Activity.SpanId"/>.
    /// </summary>
    internal static int DeriveEventIdFromSpanId(Activity activity)
    {
        Span<byte> spanIdBytes = stackalloc byte[8];
        activity.SpanId.CopyTo(spanIdBytes);
        return BitConverter.ToInt32(spanIdBytes);
    }

    /// <summary>
    /// Builds the structured state for one <c>traces</c> row. See
    /// <see cref="BuildCompletionState(string, Activity, double, IReadOnlyList{KeyValuePair{string, object?}})"/>.
    /// </summary>
    private List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        double elapsedMs)
        => BuildCompletionState(eventName, activity, elapsedMs, _commonProperties);

    /// <summary>
    /// Builds the structured state for one <c>traces</c> row. Stamps the
    /// process-level common properties first (so per-event Activity tags can
    /// override on collision), walks ancestor activities (root first) to
    /// inherit <c>command.*</c> tags, then overlays the current activity's
    /// own tags. Last writer wins; computed fields
    /// (<c>operation.name</c>, <c>operation.duration_ms</c>,
    /// <c>operation.parent_name</c>) are added last.
    /// </summary>
    /// <remarks>
    /// Common properties (caller, os.type, device.id, session.id, dev.build,
    /// ...) also live on the OTel <see cref="Resource"/>, but the AzMonitor
    /// log exporter only maps a fixed subset of Resource attrs to
    /// AppInsights envelope fields and drops the rest from
    /// <c>customDimensions</c>. We re-stamp them on each LogRecord state so
    /// they reach the <c>traces</c> table that data-x ingests.
    /// Exposed as <c>internal</c> for tests so they can verify common
    /// properties land on every event without spinning up an exporter.
    /// </remarks>
    internal static List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        double elapsedMs,
        IReadOnlyList<KeyValuePair<string, object?>> commonProperties)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Process-level common properties first — Activity tags below may
        // override on key collision (currently no overlap by design).
        foreach (var kv in commonProperties)
        {
            state[kv.Key] = kv.Value;
        }

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

        state["operation.name"] = $"dotnetup/{eventName}";
        state["operation.duration_ms"] = elapsedMs.ToString(CultureInfo.InvariantCulture);
        // Distinguishes library-emitted events (Microsoft.Dotnet.Installation,
        // routed through Metrics.OnTrackEvent) from dotnetup-internal events
        // (Microsoft.DotNet.Tools.Bootstrapper) on the AppInsights traces row.
        state["telemetry.source"] = activity.Source.Name;
        if (activity.Parent is { } parent)
        {
            state["operation.parent_name"] = parent.OperationName;
        }

        var list = new List<KeyValuePair<string, object?>>(state);
        return list;
    }

    /// <summary>
    /// Test-only teardown. Production never calls this — <c>Program.Main</c>
    /// drains via <c>Flush</c> and lets process exit reclaim the
    /// providers. It exists so unit tests that construct telemetry don't leak
    /// background batch-export threads or the hosting <see cref="ServiceProvider"/>
    /// between cases.
    /// </summary>
    /// <remarks>
    /// Mirrors Aspire's <c>TelemetryManager.Dispose</c>: shut the providers
    /// down with a <c>0</c> (non-blocking) budget rather than relying on the
    /// providers' own <see cref="IDisposable.Dispose"/>, whose OTel
    /// <see cref="LoggerProvider"/> implementation calls
    /// <c>Shutdown(Timeout.Infinite)</c> and can hang a test run on a slow or
    /// sentinel network. <c>Shutdown(0)</c> tears down immediately without
    /// waiting for a network drain.
    /// </remarks>
    public void Dispose()
    {
        // Shut the providers down first with a zero (non-blocking) budget so
        // the ServiceProvider.Dispose() cascade below — which would otherwise
        // invoke the OTel LoggerProvider's unbounded Shutdown(Timeout.Infinite)
        // — has nothing left to drain and returns immediately.
        try { _loggerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }
        try { _tracerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }

        // Release owned objects so tests don't leak. _services transitively
        // owns the ILoggerFactory and the OTel LoggerProvider; the explicit
        // field disposes satisfy CA2213 and are idempotent.
        try { _loggerFactory?.Dispose(); } catch { /* never crash on telemetry */ }
        try { _loggerProvider?.Dispose(); } catch { /* never crash on telemetry */ }
        try { _services?.Dispose(); } catch { /* never crash on telemetry */ }
        _tracerProvider?.Dispose();
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

    internal static bool IsOtlpExporterEnabled(bool disableExport) =>
        !disableExport && IsTruthy(Environment.GetEnvironmentVariable(Constants.Telemetry.EnableOtlpExporterEnvVar));
}
