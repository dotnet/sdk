// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Shared constants for the dotnetup application.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// Mutex names used for synchronization.
    /// </summary>
    public static class MutexNames
    {
        /// <summary>
        /// Mutex used during the final installation phase to protect the manifest file and extracting folder(s).
        /// Mutex names MUST be valid file names on Unix. https://learn.microsoft.com/dotnet/api/system.threading.mutex.openexisting?view=net-9.0
        /// </summary>
        public const string ModifyInstallationStates = "Global\\DotnetupManifest";
    }

    /// <summary>
    /// Unicode symbols used in console output.
    /// </summary>
    public static class Symbols
    {
        public const string RightArrow = "\u2192"; // →
        public const string UpArrow = "\u2191"; // ↑
        public const string DownArrow = "\u2193"; // ↓
        public const string UpTriangle = "\u25B2"; // ▲
        public const string DownTriangle = "\u25BC"; // ▼
        public const string Bullet = "\u2022"; // •
    }

    /// <summary>
    /// Telemetry constants: ActivitySource names, Application Insights
    /// connection string, and environment variable names. Centralized so
    /// any rename happens in one place.
    /// </summary>
    public static class Telemetry
    {
        /// <summary>
        /// Name of the bootstrapper (dotnetup) <see cref="System.Diagnostics.ActivitySource"/>.
        /// Shared between the command <c>ActivitySource</c>, the OTel logger
        /// category, and the tracer's <c>AddSource</c> registration.
        /// </summary>
        public const string BootstrapperSourceName = "Microsoft.Dotnet.Bootstrapper";

        /// <summary>
        /// Connection string for Application Insights.
        /// </summary>
        public const string ConnectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";

        public const string TelemetryOptOutEnvVar = "DOTNET_CLI_TELEMETRY_OPTOUT";
        public const string StoragePathEnvVar = "DOTNET_CLI_TELEMETRY_STORAGE_PATH";
        public const string DisableTraceExportEnvVar = "DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT";
        public const string DiskLogPathEnvVar = "DOTNET_CLI_TELEMETRY_LOG_PATH";

        /// <summary>
        /// Opt-in env var that enables network export of OTel spans via the
        /// AzMonitor + OTLP trace exporters (Aspire-style perf debugging).
        /// Default-off because data-x ingests only the AppInsights <c>traces</c>
        /// table (fed by ILogger), not the span-fed tables; keeping spans
        /// in-process saves bandwidth, batch overhead, and offline retry blobs.
        /// </summary>
        public const string EnablePerfTraceEnvVar = "DOTNETUP_CLI_GET_PERF_TRACE";
    }
}
