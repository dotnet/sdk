// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control development tools and debugging features.
/// </summary>
public sealed class DevelopmentConfiguration
{
    /// <summary>
    /// Gets or sets whether performance logging is enabled.
    /// Mapped from DOTNET_CLI_PERF_LOG environment variable.
    /// </summary>
    public bool PerfLogEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of performance log entries to collect.
    /// Mapped from DOTNET_PERF_LOG_COUNT environment variable.
    /// </summary>
    public string? PerfLogCount { get; set; }

    /// <summary>
    /// Gets or sets the CLI home directory for configuration and data.
    /// Mapped from DOTNET_CLI_HOME environment variable.
    /// </summary>
    public string? CliHome { get; set; }

    /// <summary>
    /// Gets or sets whether to enable verbose context logging.
    /// Mapped from DOTNET_CLI_CONTEXT_VERBOSE environment variable.
    /// </summary>
    public bool ContextVerbose { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to allow targeting pack caching.
    /// Mapped from DOTNETSDK_ALLOW_TARGETING_PACK_CACHING environment variable.
    /// </summary>
    public bool AllowTargetingPackCaching { get; set; } = false;
}
