// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
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

    /// <summary>
    /// Environment variable to opt out of telemetry.
    /// </summary>
    private const string TelemetryOptOutEnvVar = "DOTNET_CLI_TELEMETRY_OPTOUT";

    private readonly TracerProvider? _tracerProvider;
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
            var builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(
                        serviceName: "dotnetup",
                        serviceVersion: GetVersion())
                    .AddAttributes(TelemetryCommonProperties.GetCommonAttributes(SessionId)))
                .AddSource("Microsoft.Dotnet.Bootstrapper")
                .AddSource("Microsoft.Dotnet.Installation");  // Library's ActivitySource

            // IMPORTANT: Do NOT add auto-instrumentation (e.g. AddHttpClientInstrumentation)
            // without reviewing PII implications.

            // Add Azure Monitor exporter
            builder.AddAzureMonitorTraceExporter(o =>
            {
                o.ConnectionString = ConnectionString;
            });

#if DEBUG
            // Console exporter for local debugging
            if (Environment.GetEnvironmentVariable("DOTNETUP_TELEMETRY_DEBUG") == "1")
            {
                builder.AddConsoleExporter();
            }
#endif

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
    }

    /// <summary>
    /// Posts a custom telemetry event.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="properties">Optional string properties.</param>
    /// <param name="measurements">Optional numeric measurements.</param>
    public void PostEvent(
        string eventName,
        Dictionary<string, string>? properties = null,
        Dictionary<string, double>? measurements = null)
    {
        if (!Enabled)
        {
            return;
        }

        using var activity = CommandSource.StartActivity(eventName, ActivityKind.Internal);
        if (activity == null)
        {
            return;
        }

        // Add common properties to each span for App Insights customDimensions
        foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(SessionId))
        {
            activity.SetTag(attr.Key, attr.Value?.ToString());
        }
        activity.SetTag(TelemetryTagNames.Caller, "dotnetup");

        if (properties != null)
        {
            foreach (var (key, value) in properties)
            {
                activity.SetTag(key, value);
            }
        }

        if (measurements != null)
        {
            foreach (var (key, value) in measurements)
            {
                activity.SetTag(key, value);
            }
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
}
