// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control the CLI's user interface and interaction behavior.
/// </summary>
public sealed class CliUserExperienceConfiguration
{
    /// <summary>
    /// Gets or sets whether telemetry collection is disabled.
    /// Mapped from DOTNET_CLI_TELEMETRY_OPTOUT environment variable.
    /// </summary>
    public bool TelemetryOptOut { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to suppress the .NET logo on startup.
    /// Mapped from DOTNET_NOLOGO environment variable.
    /// </summary>
    public bool NoLogo { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to force UTF-8 encoding for console output.
    /// Mapped from DOTNET_CLI_FORCE_UTF8_ENCODING environment variable.
    /// </summary>
    public bool ForceUtf8Encoding { get; set; } = false;

    /// <summary>
    /// Gets or sets the UI language for the CLI.
    /// Mapped from DOTNET_CLI_UI_LANGUAGE environment variable.
    /// </summary>
    public string? UILanguage { get; set; }

    /// <summary>
    /// Gets or sets the telemetry profile for data collection.
    /// Mapped from DOTNET_CLI_TELEMETRY_PROFILE environment variable.
    /// </summary>
    public string? TelemetryProfile { get; set; }
}
