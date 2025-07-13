// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control workload management and behavior.
/// </summary>
public sealed class WorkloadConfiguration
{
    /// <summary>
    /// Gets or sets whether to disable workload update notifications.
    /// Mapped from DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE environment variable.
    /// </summary>
    public bool UpdateNotifyDisable { get; set; } = false;

    /// <summary>
    /// Gets or sets the interval in hours between workload update notifications.
    /// Mapped from DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS environment variable.
    /// </summary>
    public int UpdateNotifyIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether to disable workload pack groups.
    /// Mapped from DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS environment variable.
    /// </summary>
    public bool DisablePackGroups { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to skip workload integrity checks.
    /// Mapped from DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK environment variable.
    /// </summary>
    public bool SkipIntegrityCheck { get; set; } = false;

    /// <summary>
    /// Gets or sets the manifest root directories for workloads.
    /// Mapped from DOTNETSDK_WORKLOAD_MANIFEST_ROOTS environment variable.
    /// </summary>
    public string[]? ManifestRoots { get; set; }

    /// <summary>
    /// Gets or sets the pack root directories for workloads.
    /// Mapped from DOTNETSDK_WORKLOAD_PACK_ROOTS environment variable.
    /// </summary>
    public string[]? PackRoots { get; set; }

    /// <summary>
    /// Gets or sets whether to ignore default manifest roots.
    /// Mapped from DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS environment variable.
    /// </summary>
    public bool ManifestIgnoreDefaultRoots { get; set; } = false;
}
