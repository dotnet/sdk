// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    private static readonly string? s_diskLogPath = DotnetupPaths.TelemetryDiskLogPath;

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
    /// True when telemetry needs to be drained from storage externally VS CI environments which wait to exit until a POST is complete
    /// </summary>
    private readonly bool _isLocalPersistDelivery;

    /// <summary>
    /// Snapshot of process-level common properties (os.type, device.id,
    /// session.id, dev.build, ...). These also live on the OTel
    /// <see cref="Resource"/>, but the AzMonitor log exporter only maps a
    /// fixed subset of Resource attrs to AppInsights envelope fields and
    /// drops the rest, so we re-stamp them on every LogRecord state in
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
    /// The telemetry name of the subcommand the current invocation is running.
    /// Write-once per process: the first command to start wins, so if one
    /// command is implemented in terms of another the outer name is preserved
    /// rather than being clobbered by the nested command.
    /// </summary>
    internal string? CurrentCommandName { get; private set; }

    private DotnetupTelemetry()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    // Internal seam: tests pass a fake env-var lookup to force the
    // telemetry-disabled (opt-out) path deterministically, without mutating
    // process-wide environment variables or depending on the Lazy singleton's
    // one-shot, construction-time env read.
    internal DotnetupTelemetry(Func<string, string?> getEnvironmentVariable, bool? isCIEnvironment = null)
    {
        SessionId = Guid.NewGuid().ToString();
        IsOneAndDoneEnvironment = !IsTruthy(getEnvironmentVariable(Constants.Telemetry.ForceLocalDeliveryEnvVar))
            && (isCIEnvironment ?? TelemetryCommonProperties.IsCIEnvironment);

        Enabled = !IsTelemetryOptedOut(getEnvironmentVariable);

        if (!Enabled)
        {
            return;
        }

        // Register with the installation library so its telemetry flows through TrackEvent.
        Metrics.OnTrackEvent = TrackEvent;

        try
        {
            var disableExport = IsTruthy(getEnvironmentVariable(Constants.Telemetry.DisableTraceExportEnvVar));
            var enablePerfTrace = IsTruthy(getEnvironmentVariable(Constants.Telemetry.EnablePerfTraceEnvVar));
            var enableOtlpExporter = IsOtlpExporterEnabled(disableExport, getEnvironmentVariable);
            var debugConsole = getEnvironmentVariable("DOTNETUP_TELEMETRY_DEBUG") == "1";
            var connectionString = ResolveConnectionString(getEnvironmentVariable);

            var storageDirectory = IsOneAndDoneEnvironment
                ? ResolveStorageDirectory(getEnvironmentVariable)
                : DotnetupPaths.ResolveLocalTelemetryStorageDirectory(getEnvironmentVariable);

            var commonAttrs = BuildCommonAttributes();
            _commonProperties = ToLogStateProperties(commonAttrs);
            var resource = BuildResource(commonAttrs);

            _tracerProvider = BuildTracerProvider(resource, IsOneAndDoneEnvironment, enablePerfTrace, enableOtlpExporter, disableExport, debugConsole, storageDirectory, connectionString);
            _services = BuildLoggingServices(resource, IsOneAndDoneEnvironment, enableOtlpExporter, disableExport, debugConsole, storageDirectory, connectionString);
            _loggerProvider = _services.GetService<LoggerProvider>();
            _loggerFactory = _services.GetRequiredService<ILoggerFactory>();
            _logger = _loggerFactory.CreateLogger(Constants.Telemetry.BootstrapperSourceName);

            // Local runs persist synchronously and deliver via a detached drainer on exit.
            _isLocalPersistDelivery = !IsOneAndDoneEnvironment && !disableExport && !string.IsNullOrWhiteSpace(storageDirectory);
        }
        catch (Exception)
        {
            // Telemetry should never crash the app
            Enabled = false;
        }
    }

    private static string ResolveStorageDirectory(Func<string, string?> getEnvironmentVariable)
    {
        var environmentStoragePath = getEnvironmentVariable(Constants.Telemetry.StoragePathEnvVar);
        return string.IsNullOrWhiteSpace(environmentStoragePath)
            ? DotnetupPaths.TelemetryStorageDirectory
            : environmentStoragePath;
    }

    internal static string ResolveConnectionString(Func<string, string?> getEnvironmentVariable)
    {
        var overrideValue = getEnvironmentVariable(Constants.Telemetry.E2EConnectionStringEnvVar);
        return string.IsNullOrWhiteSpace(overrideValue)
            ? Constants.Telemetry.ConnectionString
            : overrideValue;
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
    private TracerProvider BuildTracerProvider(ResourceBuilder resource, bool isOneAndDone, bool enablePerfTrace, bool enableOtlpExporter, bool disableExport, bool debugConsole, string storageDirectory, string connectionString)
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

            if (isOneAndDone)
            {
                // CI: Deliver telemetry before the program can fully exit as it will not rerun.
                builder.AddAzureMonitorTraceExporter(o =>
                {
                    o.ConnectionString = connectionString;
                    o.EnableLiveMetrics = false;
                    o.StorageDirectory = storageDirectory;
                });
            }
            else
            {
                // Local: persist synchronously and let the detached drainer POST out of band.
                builder.AddPersistentStorageExporter(o =>
                {
                    o.ConnectionString = connectionString;
                    o.StorageDirectory = storageDirectory;
                    o.StartBackgroundDrain = false;
                });
            }
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
    /// </summary>
    private static ServiceProvider BuildLoggingServices(ResourceBuilder resource, bool isOneAndDone, bool enableOtlpExporter, bool disableExport, bool debugConsole, string storageDirectory, string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(lb =>
        {
            lb.AddOpenTelemetry(o =>
            {
                o.IncludeScopes = false; // Never used currently as we don't use _logger.log() with BeginScope
                o.IncludeFormattedMessage = true;
                o.ParseStateValues = true; // On by default in OTel 1.5v, but might as well be explicit
                o.SetResourceBuilder(resource);

                // OTLP is only for explicitly enabled local scenarios.
                if (!disableExport)
                {
                    if (isOneAndDone)
                    {
                        o.AddAzureMonitorLogExporter(amo =>
                        {
                            amo.ConnectionString = connectionString;
                            amo.EnableLiveMetrics = false;
                            amo.StorageDirectory = storageDirectory;
                        });
                    }
                    else
                    {
                        o.AddPersistentStorageExporter(pso =>
                        {
                            pso.ConnectionString = connectionString;
                            pso.StorageDirectory = storageDirectory;
                            pso.StartBackgroundDrain = false;
                        });
                    }

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
        CurrentCommandName ??= commandName;
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
    /// stamping <c>error.*</c> tags onto the failing activity and all of its ancestors.
    /// </summary>
    ///
    /// <remarks>
    /// This does NOT emit a LogRecord.
    /// <para>
    /// Ancestor propagation is first-failure-wins: once an ancestor has
    /// <c>error.type</c> set we don't overwrite, preserving the true root
    /// cause when later siblings fail.
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
            PropagateErrorToActivityChain(operation.Activity, errorInfo, errorCode);

            // Set the status as a failure. Uses a non-PII description (the classified error type) rather than ex.Message
            operation.SetStatus(ActivityStatusCode.Error, errorInfo.ErrorType);
        }
        catch (Exception classificationEx)
        {
            // Never let a telemetry bug crash the app, but stamp a synthetic error.type so the
            // failed span stays identifiable in the backend (SetStatus would otherwise leave it
            // empty). A classification failure is our own bug, so the Product category is correct
            // here — sourced from the ErrorCategory enum rather than a hand-typed literal to stay
            // consistent with ErrorCodeMapper.ApplyErrorTags.
            const string classificationErrorType = "TelemetryClassificationError";
            try
            {
                operation.Tag(TelemetryTagNames.ErrorType, classificationErrorType);
                operation.Tag(TelemetryTagNames.ErrorCategory, ErrorCategory.Product.ToString().ToLowerInvariant());
                operation.SetStatus(ActivityStatusCode.Error, classificationErrorType);
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
    /// Default CI shutdown budget (ms) used when no override is set. One-and-done runs (CI /
    /// piped / non-interactive) have no guaranteed next invocation to drain an offline store, so
    /// they deliver inline: the tracer / logger provider <c>Shutdown</c> drains the Azure Monitor
    /// batch exporter and waits for the in-flight HTTP POST, bounded by this budget. Matches the
    /// dotnet CLI default (<c>DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS</c>).
    /// </summary>
    private const int DefaultCiShutdownBudgetMs = 20000;

    /// <summary>
    /// Teardown budget (ms) for local runs. Telemetry is already persisted synchronously as it is emitted, so shutdown only has to release the providers.
    /// Based on existing flush delays such as in the Aspire CLI that have not caused detectable UX degradation
    /// </summary>
    private const int LocalShutdownBudgetMs = 200;

    /// <summary>
    /// Teardown budget (ms) for a successful shell-startup command. This command runs in the shell's startup path, where latency is especially visible.
    /// </summary>
    private const int ShellStartupShutdownBudgetMs = 10;

    /// <summary>
    /// Teardown budget (ms) after a failed command. Allow extra time to persist the diagnostic telemetry before the process exits.
    /// </summary>
    private const int FailureShutdownBudgetMs = 400;

    /// <summary>
    /// True when the current invocation is the latency-critical shell-startup command
    /// (<c>env script</c>, including its hidden <c>print-env-script</c> alias).
    /// </summary>
    private bool IsShellStartupCommand =>
        string.Equals(CurrentCommandName, "env script", StringComparison.Ordinal);

    /// <summary>
    /// Returns the CI shutdown budget (ms): the <c>DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS</c>
    /// override (with the legacy <c>DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS</c> as a fallback), else
    /// <see cref="DefaultCiShutdownBudgetMs"/>.
    /// </summary>
    internal static int GetCiShutdownBudgetMs()
    {
        foreach (var name in new[] { Constants.Telemetry.ShutdownTimeoutOverrideEnvVar, Constants.Telemetry.FlushTimeoutOverrideEnvVar })
        {
            var overrideValue = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(overrideValue)
                && int.TryParse(overrideValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var forced)
                && forced >= 0)
            {
                return forced;
            }
        }

        return DefaultCiShutdownBudgetMs;
    }

    /// <summary>
    /// Returns the local shutdown budget (ms). Failures take precedence over the shell-startup fast path so their diagnostic telemetry has time to persist.
    /// </summary>
    internal int GetLocalShutdownBudgetMs(int exitCode) =>
        exitCode != 0 ? FailureShutdownBudgetMs :
        IsShellStartupCommand ? ShellStartupShutdownBudgetMs :
        LocalShutdownBudgetMs;

    /// <summary>
    /// Production exit entrypoint.
    ///
    /// CI (one-and-done): shuts the providers down against the CI budget, which drains the Azure
    /// Monitor batch exporter and awaits the in-flight HTTP POST so telemetry is delivered before
    /// the process exits (see <see cref="GetCiShutdownBudgetMs"/>).
    ///
    /// Local: telemetry is already persisted synchronously, so shutdown just releases the providers.
    /// A short-lived detached drainer is spawned to POST the persisted blobs.
    /// </summary>
    /// <param name="exitCode">
    /// The process exit code of dotnetup, to determine if it was a success or failure.
    /// </param>
    public void Flush(int exitCode)
    {
        if (_isLocalPersistDelivery)
        {
            ShutdownProviders(GetLocalShutdownBudgetMs(exitCode));

            // Skip the out-of-band drainer on the latency-critical shell-startup hot path; those
            // blobs are delivered by the next dotnetup run or the SDK CLI sharing the store.
            if (!IsShellStartupCommand)
            {
                DotnetupTelemetryDrainProcess.SpawnDetachedDrainer();
            }

            return;
        }

        ShutdownProviders(GetCiShutdownBudgetMs());
    }

    /// <summary>
    /// Shuts the providers down with an explicit budget, bypassing environment classification.
    /// For tests and diagnostics only.
    /// </summary>
    internal void FlushWithTimeout(int timeoutMilliseconds)
    {
        ShutdownProviders(timeoutMilliseconds);
    }

    /// <summary>
    /// Shuts the logger and tracer providers down against a single shared deadline.
    /// </summary>
    private void ShutdownProviders(int timeoutMilliseconds)
    {
        var budget = Math.Max(0, timeoutMilliseconds);
        TelemetryTestHooks.TryWriteFile(
            Constants.Telemetry.TestShutdownBudgetPathEnvVar,
            $"ShutdownBudgetMs={budget}");
        var deadline = Environment.TickCount64 + budget;

        try
        {
            // Logger first: the primary data-x signal. Each LogRecord is fully
            // decorated by BuildCompletionState at _logger.Log(...) time, so
            // Shutdown only has to drain an already-self-described queue.
            _loggerProvider?.Shutdown(budget);
        }
        catch
        {
            // Never let telemetry shutdown failures crash the app.
        }

        try
        {
            // Tracer gets the leftover budget so the two shutdowns share one
            // deadline rather than summing to 2× on a slow network.
            var remaining = (int)Math.Clamp(deadline - Environment.TickCount64, 0, budget);
            _tracerProvider?.Shutdown(remaining);
        }
        catch
        {
            // Never let telemetry shutdown failures crash the app.
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
    /// Dispose callback for <see cref="TrackedOperation"/>. Emits the completion LogRecord, then stops the activity.
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
    /// Stamps the process-level common properties first (so per-event Activity tags can override on collision).
    /// Walks ancestor activities (root first) to inherit <c>command.*</c> tags.
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
    /// Test-only teardown. Implemented explicitly on <see cref="IDisposable"/>
    /// so production code cannot call <c>DotnetupTelemetry.Instance.Dispose()</c>
    /// and silently lose telemetry — production must flush via <see cref="Flush"/>.
    /// Reachable only through a <c>using</c> block or an explicit
    /// <c>IDisposable</c> cast, which is confined to test teardown.
    /// </summary>
    /// <remarks>
    /// Mirrors Aspire's <c>TelemetryManager.Dispose</c>
    /// <c>Shutdown(0)</c> tears down immediately without waiting for a network drain.
    /// </remarks>
    void IDisposable.Dispose()
    {
        try { _loggerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }
        try { _tracerProvider?.Shutdown(0); } catch { /* never crash on telemetry */ }

        // One release call: the ServiceProvider owns _loggerFactory and the OTel
        // _loggerProvider (both resolved from it) and disposes them transitively,
        // so we don't dispose those two fields explicitly (see the CA2213
        // suppression on their declarations).
        try { _services?.Dispose(); } catch { /* never crash on telemetry */ }
        try { _tracerProvider?.Dispose(); } catch { /* never crash on telemetry */ }
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

    internal static bool IsTelemetryOptedOut(Func<string, string?> getEnvironmentVariable) =>
        IsTruthy(getEnvironmentVariable(Constants.Telemetry.TelemetryOptOutEnvVar));

    internal static bool IsOtlpExporterEnabled(bool disableExport) =>
        IsOtlpExporterEnabled(disableExport, Environment.GetEnvironmentVariable);

    internal static bool IsOtlpExporterEnabled(bool disableExport, Func<string, string?> getEnvironmentVariable) =>
        !disableExport && IsTruthy(getEnvironmentVariable(Constants.Telemetry.EnableOtlpExporterEnvVar));
}
