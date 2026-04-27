// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Telemetry;
using OpenTelemetry;
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
            var environmentStoragePath = Environment.GetEnvironmentVariable(StoragePathEnvVar);

            var builder = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(r => { r.AddService("dotnetup", serviceVersion: GetVersion()); })
                .AddSource("Microsoft.Dotnet.Bootstrapper")
                .AddSource("Microsoft.Dotnet.Installation")
                .AddOtlpExporter()
                .AddInMemoryExporter(_activities)
                .SetSampler(new AlwaysOnSampler());

            if (!disableExport)
            {
                var storageDirectory = string.IsNullOrWhiteSpace(environmentStoragePath)
                    ? s_defaultStorageDirectory
                    : environmentStoragePath;

                builder.AddAzureMonitorTraceExporter(o =>
                {
                    o.ConnectionString = ConnectionString;
                    o.EnableLiveMetrics = false;
                    o.StorageDirectory = storageDirectory;
                });
            }

            // Console exporter for local debugging / E2E test verification.
            // Set DOTNETUP_TELEMETRY_DEBUG=1 to enable.
            if (Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_DEBUG") == "1")
            {
                builder.AddConsoleExporter();
            }

            _tracerProvider = builder.Build();
        }
        catch (Exception)
        {
            // Telemetry should never crash the app
            Enabled = false;
        }
    }

    /// <summary>
    /// Starts a command activity with the given name.
    /// </summary>
    /// <param name="commandName">The name of the command (e.g., "sdk/install").</param>
    /// <returns>The started Activity, or null if telemetry is disabled.</returns>
    public Activity? StartCommand(string commandName)
    {
        if (!Enabled)
        {
            return null;
        }

        var activity = CommandSource.StartActivity($"command/{commandName}", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag(TelemetryTagNames.CommandName, commandName);
            foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
            {
                activity.SetTag(attr.Key, attr.Value?.ToString());
            }
        }
        activity?.SetTag(TelemetryTagNames.Caller, "dotnetup");
        activity?.SetTag(TelemetryTagNames.SessionId, SessionId);
        return activity;
    }

    /// <summary>
    /// Starts a tracked command operation that emits a telemetry event on dispose.
    /// Common properties and caller/session tags are pre-populated on the span.
    /// </summary>
    internal TrackedOperation StartTrackedCommand(string commandName)
    {
        var activity = Enabled
            ? CommandSource.StartActivity($"command/{commandName}", ActivityKind.Internal)
            : null;

        var op = new TrackedOperation(activity, $"command/{commandName}", TrackEvent);
        op.Tag(TelemetryTagNames.CommandName, commandName);
        op.Tag(TelemetryTagNames.Caller, "dotnetup");
        op.Tag(TelemetryTagNames.SessionId, SessionId);

        if (activity != null)
        {
            foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
            {
                activity.SetTag(attr.Key, attr.Value?.ToString());
            }
        }

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
    /// Records an exception on the given operation and its underlying activity.
    /// Tags the TrackedOperation directly so error properties appear in the
    /// command event's Properties (the data-x pipeline only carries tags set
    /// via TrackedOperation.Tag(), not raw Activity.SetTag()).
    /// </summary>
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

        // Tag the operation so error data flows into the command event.
        operation.Tag(TelemetryTagNames.ErrorType, errorInfo.ErrorType);
        operation.Tag(TelemetryTagNames.ErrorCategory, errorInfo.Category.ToString().ToLowerInvariant());
        if (errorCode is not null)
        {
            operation.Tag(TelemetryTagNames.ErrorCode, errorCode);
        }
        if (errorInfo.StatusCode is { } sc)
        {
            operation.Tag(TelemetryTagNames.ErrorHttpStatus, sc.ToString(CultureInfo.InvariantCulture));
        }
        if (errorInfo.HResult is { } hr)
        {
            operation.Tag(TelemetryTagNames.ErrorHResult, hr.ToString(CultureInfo.InvariantCulture));
        }
        if (errorInfo.Details is { } details)
        {
            operation.Tag(TelemetryTagNames.ErrorDetails, details);
        }
        if (errorInfo.StackTrace is { } stackTrace)
        {
            operation.Tag(TelemetryTagNames.ErrorStackTrace, stackTrace);
        }

        // Also apply to the raw Activity for OpenTelemetry span-level visibility.
        var activity = operation.Activity;
        if (activity is not null)
        {
            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo, errorCode);
        }

        // Emit error as a separate event for data-x-platform (traces table).
        // Pass stopActivity=false because the owning TrackedOperation is still
        // alive and will be disposed later (in CommandBase.finally or Program.finally).
        var errorEventProps = ErrorCodeMapper.ToEventProperties(errorInfo);
        if (errorCode is not null)
        {
            errorEventProps[TelemetryTagNames.ErrorCode] = errorCode;
        }
        TrackEventCore("error", operation.Activity, errorEventProps, stopActivity: false);
    }

    /// <summary>
    /// Flushes any pending telemetry.
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
    /// Builds the merged properties dict from an activity's span tags, the stored
    /// tags accumulated by <see cref="TrackedOperation.Tag"/>, and enrichment
    /// fields (event ID, session ID, duration).
    /// </summary>
    private static Dictionary<string, string?> BuildProperties(Activity? activity, IDictionary<string, string?> storedTags, string sessionId)
    {
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (activity is not null)
        {
            foreach (var tag in activity.TagObjects)
            {
                properties[tag.Key] = tag.Value?.ToString();
            }
        }

        foreach (var tag in storedTags)
        {
            properties[tag.Key] = tag.Value;
        }

        properties["event id"] = Guid.NewGuid().ToString();
        properties["SessionId"] = sessionId;

        if (activity is not null)
        {
            var durationMs = (DateTimeOffset.UtcNow - new DateTimeOffset(activity.StartTimeUtc)).TotalMilliseconds;
            properties["operation.duration_ms"] = durationMs.ToString(CultureInfo.InvariantCulture);
        }

        return properties;
    }

    /// <summary>
    /// Emits an <see cref="ActivityEvent"/> on the given activity with all
    /// properties from the span, stored tags, and common attributes.
    /// </summary>
    /// <remarks>
    /// This is <c>internal static</c> so that unit tests can use the real emission
    /// logic instead of duplicating it in test callbacks.
    /// </remarks>
    /// <param name="eventName">The event name suffix (e.g., "command/sdk/install").</param>
    /// <param name="activity">The activity to emit the event on. May be null if telemetry is disabled.</param>
    /// <param name="storedTags">Tags accumulated by <see cref="TrackedOperation.Tag"/>.</param>
    /// <param name="sessionId">The telemetry session ID.</param>
    /// <param name="stopActivity">Whether to stop the activity after emitting the event.
    /// <c>true</c> for final events (TrackedOperation.Dispose); <c>false</c> for
    /// mid-flight events (RecordException error events) where the activity is still alive.</param>
    internal static void EmitActivityEvent(string eventName, Activity? activity, IDictionary<string, string?> storedTags, string sessionId, bool stopActivity = true)
    {
        var properties = BuildProperties(activity, storedTags, sessionId);

        // Set each property as a span tag for the dependencies table.
        if (activity is not null)
        {
            foreach (var prop in properties)
            {
                activity.SetTag(prop.Key, prop.Value);
            }
        }

        // Build ActivityTagsCollection for the event (traces table).
        // Use indexer assignment (not Add) because `properties` may already
        // include the common attributes — `StartTrackedCommand` stamps them
        // onto the activity tags, and `BuildProperties` echoes
        // `activity.TagObjects` back into the dict. `ActivityTagsCollection.Add`
        // throws `InvalidOperationException` on duplicate keys, which
        // `TrackEventCore`'s `try/catch` then swallows — silently dropping
        // every `command/<name>` event. Only `process/complete` survived in
        // production because `StartTrackedProcess` doesn't pre-populate
        // common attrs.
        var tags = new ActivityTagsCollection();
        foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(sessionId))
        {
            tags[attr.Key] = attr.Value?.ToString();
        }
        foreach (var prop in properties.Where(p => p.Value is not null).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            tags[prop.Key] = prop.Value;
        }

        // Emit ActivityEvent on the activity reference BEFORE stopping it.
        var @event = new ActivityEvent($"dotnetup/{eventName}", tags: tags);
        activity?.AddEvent(@event);

        if (stopActivity)
        {
            activity?.Stop();
        }
    }

    /// <summary>
    /// Callback matching the <see cref="TrackedOperation"/> delegate signature.
    /// Invoked on dispose — always stops the activity.
    /// </summary>
    private void TrackEvent(string eventName, Activity? activity, IDictionary<string, string?> storedTags)
    {
        TrackEventCore(eventName, activity, storedTags, stopActivity: true);
    }

    /// <summary>
    /// Core event emission with control over whether to stop the activity.
    /// </summary>
    private void TrackEventCore(string eventName, Activity? activity, IDictionary<string, string?> storedTags, bool stopActivity)
    {
        if (!Enabled)
        {
            if (stopActivity)
            {
                activity?.Stop();
            }

            return;
        }

        try
        {
            EmitActivityEvent(eventName, activity, storedTags, SessionId, stopActivity);
        }
        catch
        {
            // Telemetry should never crash the app
        }
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
