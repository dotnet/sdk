// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Singleton telemetry manager for dotnetup.
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
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Owned and disposed transitively by _services (resolved from that ServiceProvider). Disposed via _services.Dispose() in Dispose(); shut down non-blocking via Shutdown(0) first.")]
    private readonly LoggerProvider? _loggerProvider;
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Owned and disposed transitively by _services (resolved from that ServiceProvider). Disposed via _services.Dispose() in Dispose().")]
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
    /// <see cref="TelemetryCommonProperties.IsCIEnvironment"/>.
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
    /// device.id, session.id, dev.build, ...).
    ///
    /// Used both as:
    /// OTel Resource Attributes (for spans / opt-in perf trace export)
    /// Per-LogRecord state stamps (for actual telemetry)
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
    /// </summary>
    private static ResourceBuilder BuildResource(List<KeyValuePair<string, object>> commonAttrs)
    {
        return ResourceBuilder.CreateDefault()
            .AddService("dotnetup", serviceVersion: GetVersion())
            .AddAttributes(commonAttrs);
    }

    /// <summary>
    /// Builds the <see cref="TracerProvider"/>.
    /// Traces should be opt-in via <c>DOTNETUP_CLI_GET_PERF_TRACE=1</c> because data-x does not ingest spans.
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

        // Span network export is off by default because data-x ingests logs, not spans.
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
    /// Builds the <see cref="ILoggerFactory"/> in a way that exposes forceFlush via a ServiceProvider.
    ///
    /// The AzMonitor log exporter routes data through the AppInsights <c>traces</c> table which is the only table data-x-platform ingests.
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

                // OTLP is only for explicitly enabled local scenarios.
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
    /// Starts a per-command <see cref="TrackedOperation"/> operation.
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
    /// Starts the root <see cref="TrackedOperation"/> for the entire dotnetup invocation.
    /// </summary>
    internal TrackedOperation StartTrackedProcess(string name)
    {
        var activity = Enabled
            ? CommandSource.StartActivity(name, ActivityKind.Internal)
            : null;

        return new TrackedOperation(activity, "root", TrackEvent);
    }

    /// <summary>
    /// Records an exception on the failing operation by classifying it and
    /// stamping <c>error.*</c> tags onto the failing activity, then
    /// propagating those same tags up the activity ancestor chain so each
    /// parent's completion log carries the failure signal.
    /// </summary>
    /// <remarks>
    /// This does NOT emit a separate LogRecord. The error metadata is folded
    /// into the operation's single completion row, which is emitted later when
    /// the <see cref="TrackedOperation"/> is disposed (see
    /// <see cref="EmitCompletionLog"/>). Keeping the failure on one atomic row
    /// — rather than a second severity=Error record — means a truncated flush
    /// can only lose the whole row, never split a failure into a "success" row
    /// plus a lost error row.
    /// <para>
    /// Ancestor propagation is first-failure-wins: once an ancestor has
    /// <c>error.type</c> set we don't overwrite, preserving the true root
    /// cause when later siblings fail.
    /// </para>
    /// <para>
    /// Wrapped in a catch-all: telemetry classification (which does stack-trace
    /// string processing and reflection over exception types) must never be
    /// able to throw out of the failure path and corrupt or drop the
    /// completion row. On classification failure the row is still emitted at
    /// dispose; <see cref="TrackedOperation.EnsureErrorTypeTagged"/> guarantees
    /// it carries an explicit (possibly empty) <c>error.type</c>.
    /// </para>
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

        try
        {
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
        catch (Exception classificationEx)
        {
            // Never let telemetry classification crash the app or corrupt the
            // failure row. Fall back to a coarse, always-safe tag so the
            // completion row still records that *a* failure occurred; the
            // classifier's own bug becomes the error.type rather than silently
            // dropping the failure signal.
            try
            {
                operation.Tag(TelemetryTagNames.ErrorType, "TelemetryClassificationError");
                operation.Tag(TelemetryTagNames.ErrorCategory, "product");
                operation.SetStatus(ActivityStatusCode.Error, "TelemetryClassificationError");
            }
            catch
            {
                // Tagging itself failed (null activity, disposed, etc.) — nothing
                // more we can safely do. The completion row still emits at dispose.
            }

            Debug.Fail($"RecordException classification threw: {classificationEx}");
        }
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
    /// Default flush budget (ms) for interactive / shell-startup exits.
    /// Goal: Should not cause perceptible delay for humans.
    /// Matches Aspire's CLI shutdown budget (TelemetryManager.ShutDownTimeoutMilliseconds = 200).
    /// </summary>
    private const int DefaultFlushTimeoutMs = 200;

    /// <summary>
    /// Durable flush budget (ms) for one-and-done environments (CI / piped /
    /// non-interactive) that have no guaranteed next run to drain the offline store.
    /// </summary>
    private const int DurableFlushTimeoutMs = 5000;

    /// <summary>
    /// True when the current invocation is the latency-critical shell-startup command (<c>print-env-script</c>)
    /// </summary>
    private bool IsShellStartupCommand =>
        string.Equals(CurrentCommandName, "print-env-script", StringComparison.Ordinal);

    /// <summary>
    /// Returns the ideal on-exit flush budget (ms) for the current run.
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

        // Failures are essential to record and not expected in a typical use case.
        if (exitCode != 0)
        {
            return DurableFlushTimeoutMs;
        }

        // Shell-startup hot path (don't slow down users shells)
        if (IsShellStartupCommand)
        {
            return DefaultFlushTimeoutMs;
        }

        // Interactive foreground use: keep exit snappy
        if (!IsOneAndDoneEnvironment && !Console.IsOutputRedirected)
        {
            return DefaultFlushTimeoutMs;
        }

        // CI / piped / non-interactive: one-and-done with no guaranteed next run to drain the offline store
        return DurableFlushTimeoutMs;
    }

    /// <summary>
    /// Production flush entrypoint. Computes the budget from the process exit
    /// code and the current environment (see <see cref="GetFlushTimeoutMs(int)"/>),
    /// then drains the providers. Returns as soon as the queues are empty.
    ///
    /// Whatever doesn't drain in time falls back to the AzMonitor exporter's <c>StorageDirectory</c> retry queue.
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
    /// Flushes the providers against a single shared deadline.
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
    /// Builds the structured state for one <c>traces</c> row based on span tags.
    /// </summary>
    private List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        double elapsedMs)
        => BuildCompletionState(eventName, activity, elapsedMs, _commonProperties);

    /// <summary>
    /// Builds a structured state for one <c>traces</c> row.
    /// Stamps the / process-level common properties first (so per-event Activity tags can override on collision)
    /// Walks ancestor activities (root first) to inherit <c>command.*</c> tags
    /// Overlays the current activity's own tags.
    /// </summary>
    ///
    /// <remarks>
    /// Common properties (caller, os.type, device.id, session.id, dev.build,
    /// ...) also live on the OTel <see cref="Resource"/>, but the AzMonitor
    /// log exporter only maps a fixed subset of Resource attrs to
    /// AppInsights envelope fields and drops the rest from <c>customDimensions</c>
    ///
    /// We re-stamp them on each LogRecord state so they reach the <c>traces</c> table.
    /// </remarks>
    internal static List<KeyValuePair<string, object?>> BuildCompletionState(
        string eventName,
        Activity activity,
        double elapsedMs,
        IReadOnlyList<KeyValuePair<string, object?>> commonProperties)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in commonProperties)
        {
            state[kv.Key] = kv.Value;
        }

        // Stamp the higher-level command onto child activities - e.g. 'extract/complete' becomes associated with 'sdk/install'
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

        // Stamp our own tags into the state
        foreach (var tag in activity.TagObjects)
        {
            state[tag.Key] = tag.Value;
        }

        state["operation.name"] = $"dotnetup/{eventName}";
        state["operation.duration_ms"] = elapsedMs.ToString(CultureInfo.InvariantCulture);
        state["telemetry.source"] = activity.Source.Name; // Distinguishes library-emitted events from dotnetup-internal events
        if (activity.Parent is { } parent)
        {
            state["operation.parent_name"] = parent.OperationName;
        }

        var list = new List<KeyValuePair<string, object?>>(state);
        return list;
    }

    /// <summary>
    /// Test-only teardown.
    /// </summary>
    /// <remarks>
    /// Mirrors Aspire's <c>TelemetryManager.Dispose</c>
    /// <c>Shutdown(0)</c> tears down immediately without waiting for a network drain.
    /// </remarks>
    public void Dispose()
    {
        try { _loggerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }
        try { _tracerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }

        // One release call: the ServiceProvider owns _loggerFactory and the OTel
        // _loggerProvider (both resolved from it) and disposes them transitively,
        // so we don't dispose those two fields explicitly (see the CA2213
        // suppression on their declarations).
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
