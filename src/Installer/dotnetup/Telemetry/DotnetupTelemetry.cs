// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.DotNet.Cli;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Exporter;
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

        // Check opt-out using the same logic as the SDK:
        // - MICROSOFT_ENABLE_TELEMETRY compile flag controls the default (official builds = on, dev/test = off)
        // - DOTNET_CLI_TELEMETRY_OPTOUT env var overrides explicitly
        var optOutValue = Environment.GetEnvironmentVariable(TelemetryOptOutEnvVar);
        bool explicitOptOut = string.Equals(optOutValue, "1", StringComparison.Ordinal) ||
                              string.Equals(optOutValue, "true", StringComparison.OrdinalIgnoreCase);
        Enabled = !explicitOptOut && !CompileOptions.TelemetryOptOutDefault;

        if (!Enabled)
        {
            return;
        }

        // Register with the installation library so its telemetry flows through TrackEvent.
        InstallationActivitySource.OnTrackEvent = TrackEvent;

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

        var op = new TrackedOperation(activity, $"command/{commandName}");
        op.SetTag(TelemetryTagNames.CommandName, commandName);
        op.SetTag(TelemetryTagNames.Caller, "dotnetup");
        op.SetTag(TelemetryTagNames.SessionId, SessionId);

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

        return new TrackedOperation(activity, "process/complete");
    }

    /// <summary>
    /// Records an exception on the given activity.
    /// </summary>
    /// <param name="activity">The activity to record the exception on.</param>
    /// <param name="ex">The exception to record.</param>
    /// <param name="errorCode">Optional error code override.</param>
    public void RecordException(Activity? activity, Exception ex, string? errorCode = null)
    {
        if (activity == null || !Enabled)
        {
            return;
        }

        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
        ErrorCodeMapper.ApplyErrorTags(activity, errorInfo, errorCode);

        // Walk up to the root activity and apply the same error tags there,
        // so workbook queries on either the command span or the root span
        // see error information.
        var root = activity;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        if (root != activity)
        {
            ErrorCodeMapper.ApplyErrorTags(root, errorInfo);
        }

        // Emit error as an event for data-x-platform (traces table).
        var errorEventProps = ErrorCodeMapper.ToEventProperties(errorInfo);
        if (errorCode is not null)
        {
            errorEventProps[TelemetryTagNames.ErrorCode] = errorCode;
        }
        TrackEvent("error", errorEventProps);
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
    /// Emits an ActivityEvent on Activity.Current AND sets each property as a
    /// tag on Activity.Current. This is the single entry point for telemetry
    /// data — callers should NOT separately call Activity.Current?.SetTag().
    /// </summary>
    public void TrackEvent(string eventName, IDictionary<string, string?>? properties)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            properties ??= new Dictionary<string, string?>();
            properties["event id"] = Guid.NewGuid().ToString();
            properties["SessionId"] = SessionId;

            // Set each property as a span tag so it appears in the span's
            // customDimensions (dependencies/requests tables) as well.
            var current = Activity.Current;
            if (current != null)
            {
                foreach (var prop in properties)
                {
                    current.SetTag(prop.Key, prop.Value);
                }
            }

            // Merge common properties as tags (same as SDK's MakeTags)
            var tags = new ActivityTagsCollection();
            foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
            {
                tags.Add(new KeyValuePair<string, object?>(attr.Key, attr.Value?.ToString()));
            }
            foreach (var prop in properties.Where(p => p.Value is not null).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(new KeyValuePair<string, object?>(prop.Key, prop.Value));
            }

            var @event = new ActivityEvent($"dotnetup/{eventName}", tags: tags);
            current?.AddEvent(@event);
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
