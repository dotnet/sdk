// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
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

    private static readonly string s_defaultStorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotnet", "TelemetryStorageService");

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

        // Check opt-out (same env var as SDK)
        var optOutValue = Environment.GetEnvironmentVariable(TelemetryOptOutEnvVar);
        Enabled = !string.Equals(optOutValue, "1", StringComparison.Ordinal) &&
                  !string.Equals(optOutValue, "true", StringComparison.OrdinalIgnoreCase);

        if (!Enabled)
        {
            return;
        }

        try
        {
            var disableExport = IsTruthy(Environment.GetEnvironmentVariable(DisableTraceExportEnvVar));
            var environmentStoragePath = Environment.GetEnvironmentVariable(StoragePathEnvVar);

            // Mirror the SDK TelemetryClient's TracerProvider setup exactly.
            // Using "dotnet-cli" as service name to test data-x-platform ingestion.
            // TODO: Change to "dotnetup" once we confirm the platform accepts it.
            var builder = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(r => { r.AddService("dotnet-cli", serviceVersion: GetVersion()); })
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
            // Add common properties to each span for App Insights customDimensions
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
    /// Emits an ActivityEvent on Activity.Current using the same format as the
    /// .NET SDK TelemetryClient (lands in customEvents table in App Insights).
    /// Uses "dotnet/cli/" prefix to test data-x-platform ingestion.
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

            var @event = new ActivityEvent($"dotnet/cli/{eventName}", tags: tags);
            Activity.Current?.AddEvent(@event);
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
