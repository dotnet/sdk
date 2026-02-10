// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.DotNet.Cli.Utils;
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
    /// Connection string for Application Insights (same as dotnet CLI).
    /// </summary>
    private const string ConnectionString = "InstrumentationKey=04172778-3bc9-4db6-b50f-cafe87756a47;IngestionEndpoint=https://westus2-2.in.applicationinsights.azure.com/;LiveEndpoint=https://westus2.livediagnostics.monitor.azure.com/;ApplicationId=fbd94297-7083-42b8-aaa5-1886192b4272";

    /// <summary>
    /// Environment variable to opt out of telemetry.
    /// </summary>
    private const string TelemetryOptOutEnvVar = "DOTNET_CLI_TELEMETRY_OPTOUT";

    private readonly TracerProvider? _tracerProvider;
    private readonly string _sessionId;
    private bool _disposed;

    /// <summary>
    /// Gets whether telemetry is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    public string SessionId => _sessionId;

    private DotnetupTelemetry()
    {
        _sessionId = Guid.NewGuid().ToString();

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
                    .AddAttributes(TelemetryCommonProperties.GetCommonAttributes(_sessionId)))
                .AddSource("Microsoft.Dotnet.Bootstrapper")
                .AddSource("Microsoft.Dotnet.Installation");  // Library's ActivitySource

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
            activity.SetTag("command.name", commandName);
            // Add common properties to each span for App Insights customDimensions
            foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(_sessionId))
            {
                activity.SetTag(attr.Key, attr.Value?.ToString());
            }
        }
        activity?.SetTag("caller", "dotnetup");
        activity?.SetTag("session.id", _sessionId);
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
        foreach (var attr in TelemetryCommonProperties.GetCommonAttributes(_sessionId))
        {
            activity.SetTag(attr.Key, attr.Value?.ToString());
        }
        activity.SetTag("caller", "dotnetup");

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
    /// Posts an install completed event.
    /// </summary>
    public void PostInstallEvent(InstallEventData data)
    {
        PostEvent("install/completed", new Dictionary<string, string>
        {
            ["component"] = data.Component,
            ["version"] = data.Version,
            ["previous_version"] = data.PreviousVersion ?? string.Empty,
            ["was_update"] = data.WasUpdate.ToString(),
            ["install_root_hash"] = TelemetryCommonProperties.HashPath(data.InstallRoot)
        }, new Dictionary<string, double>
        {
            ["download_ms"] = data.DownloadDuration.TotalMilliseconds,
            ["extract_ms"] = data.ExtractionDuration.TotalMilliseconds,
            ["archive_bytes"] = data.ArchiveSizeBytes
        });
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
